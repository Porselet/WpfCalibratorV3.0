using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

public sealed partial class CommunicationService : ICommunicationService, IDisposable
{


    // Сюда переносим из основного файла:
    // - Метод ExecuteCommandAsync
    // - Метод SerializeCommand (или как у тебя называется сборка пакета)
    // - Переменные _expectedCmd, _expectedVarId, _currentTransactionTcs
    // Технические переменные для контроля: какой именно зеркальный ответ мы сейчас ждем от STM32

    /// <summary>
    /// Высокуровневый конвейер: принимает команду от Диспетчера, пакует, 
    /// отправляет в порт и асинхронно ждет зеркальный ответ от STM32 с таймаутом 50мс.
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ExecuteCommandAsync(Models.NetworkCommand cmd)
    {
        if (cmd == null) return false;

        // 1. ШЛАГБАУМ: Запрашиваем монопольный доступ к шине
        await _networkSemaphore.WaitAsync();

        try
        {
            // 2. МАРШАЛИНГ: Пакуем данные ТОЛЬКО если это реальная запись (VarWrite)!
            // Если команда идет на Чтение (VarRead) — payload ОБЯЗАН быть абсолютно пустым!
            byte[] payloadBytes = Array.Empty<byte>();
            byte elementsCount = (byte)(cmd.Rows * cmd.Cols);

            if (cmd.Cmd == Models.LinkCommand.VarWrite)
            {
                payloadBytes = SerializeCommandPayload(cmd);
            }

            // 3. НАСТРОЙКА ОЖИДАНИЯ: Запоминаем, какой именно ответ от платы мы теперь ждем.
            // Переводим перечисление enum в сырой байт (0x01 или 0x02)
            _expectedCmd = (byte)cmd.Cmd;
            _expectedVarId = cmd.VarId;
            _responseCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();


            // 4. 🔥 ЖЕСТКИЙ ФИКС ОТПРАВКИ: Передаем СТРОГО (byte)cmd.Cmd!
            // Это принудительно заставит низкоуровневый метод отправить в STM32 честный байт 0x02 (Чтение),
            // не давая ему возможности подменить команду на 0x01 из-за внутренних флагов IsParam!
            // ВНУТРИ МЕТОДА ExecuteCommandAsync ПЕРЕД ВЫЗОВОМ SendPacketAsync:
            int currentSize = 4;
            string typeLower = cmd.DataType.ToLower().Trim();
            if (typeLower == "double" || typeLower == "int64" || typeLower == "uint64") currentSize = 8;
            else if (typeLower == "int16" || typeLower == "uint16") currentSize = 2;
            else if (typeLower == "int8" || typeLower == "uint8" || typeLower == "boolean" || typeLower == "bool") currentSize = 1;

            // ЗАПОМИНАЕМ ГЕОМЕТРИЮ ДЛЯ ПРИЕМНИКА (АБСОЛЮТНАЯ ПОТОКОБЕЗОПАСНОСТЬ):
            _expectedElementSize = currentSize;
            _expectedRows = cmd.Rows;
            _expectedCols = cmd.Cols;
            _expectedDataType = typeLower;

            _expectedCmd = (byte)cmd.Cmd;
            _expectedVarId = cmd.VarId;
            _responseCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();

            await SendPacketAsync(cmd.ModelId, (byte)cmd.Cmd, cmd.VarId, elementsCount, payloadBytes);


            // 5. ДИНАМИЧЕСКИЙ ТАЙМАУТ-ПРЕДОХРАНИТЕЛЬ: 
            // Вычисляем время ожидания на основе тяжести пакета.
            // Базовые 60 мс на задержки ОС Windows + 1.5 мс на каждый элемент float/double в проводе.
            int totalElements = cmd.Rows * cmd.Cols;
            int dynamicTimeoutMs = 160 + (int)(totalElements * 1.5) + 300;

            // На всякий случай ограничиваем максимальный таймаут сверху (например, 1.5 секунды), 
            // чтобы при полном обрыве кабеля софт не зависал бесконечно.
            if (dynamicTimeoutMs > 1500) dynamicTimeoutMs = 1500;

            var timeoutTask = System.Threading.Tasks.Task.Delay(dynamicTimeoutMs);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(_responseCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Случился АППАРАТНЫЙ ТАЙМАУТ (Плата или буфер Windows не успели за dynamicTimeoutMs)
                string errDesc = $"[TIMEOUT] Отсутствует ответ от МК на команду {cmd.Cmd} (VarId: {cmd.VarId}) за {dynamicTimeoutMs}мс";

                // Выводим красную строку в наш текстовый терминал пакетов
                //WpfCalibrator.Views.UartMonitorWindow.LogPacket("ERR !", "#FF5555", errDesc, Array.Empty<byte>());
                OnLogPacket?.Invoke("ERR !", "#FF5555", errDesc, Array.Empty<byte>());
                return false; // Транзакция сорвалась
            }


            return true;
        }
        catch (Exception ex)
        {
            string critDesc = $"[CRIT ERROR] Сбой транзакции: {ex.Message}";
            //WpfCalibrator.Views.UartMonitorWindow.LogPacket("EXCP", "#FF0000", critDesc, Array.Empty<byte>());
            OnLogPacket?.Invoke("EXCP", "#FF0000", critDesc, Array.Empty<byte>());
            return false;
        }
        finally
        {
            _responseCompletionSource = null;
            _networkSemaphore.Release(); // Железно открываем шлагбаум
        }
    }

    /// <summary>
    /// Универсальный полиморфный метод для отправки любых калибровочных массивов (float, int, short, byte) в STM32.
    /// </summary>

    // 2. Отправка пакетов (синхронная оболочка)

    // БЫЛО: public async Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    // СТАЛО: Жестко закрываем метод извне! Теперь отправка байт доступна ТОЛЬКО через ExecuteCommandAsync!
    private async System.Threading.Tasks.Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    {
        // ... твой оригинальный рабочий сишный код сборки кадра и записи в порт ...

        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Порт не открыт!");
        }

        // 1. Формируем заголовок пакета строго по app_link.h
        byte[] header = new byte[]
        {
        0xAA,    // Индекс 0: PKT_PREAMBLE_IDX (0xAA)
        modelId, // Индекс 1: PKT_MODEL_ID_IDX
        cmd,     // Индекс 2: PKT_CMD_IDX
        varId,   // Индекс 3: PKT_VAR_ID_IDX
        elementsCount // Индекс 4: PKT_LEN_IDX
        };

        // 2. Вычисляем CRC-8
        byte[] packet = header.Concat(payload).ToArray();
        byte crc = CalculateCRC8_SAE_J1850(packet, packet.Length);

        // 3. Добавляем CRC в конец массива
        Array.Resize(ref packet, packet.Length + 1);
        packet[packet.Length - 1] = crc;
        // ВНУТРИ МЕТОДА SendPacketAsync ПЕРЕД ЗАПИСЬЮ В ПОРТ:

        string txDesc = $"TX [CMD: 0x{cmd:X2}, VarId: {varId}]";
        //WpfCalibrator.Views.UartMonitorWindow.LogPacket("TX -->", "#007ACC", txDesc, packet);
        OnLogPacket?.Invoke("TX -->", "#007ACC", txDesc, packet);
        // 4. Отправка в физический порт
        _serialPort.Write(packet, 0, packet.Length);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serialPort?.Dispose();
    }



    // 3. Прием пакетов (фоновый поток)

    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    private async System.Threading.Tasks.Task ListenAsync()
    {
        while (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                // ======================================================================
                // 1. ИЩЕМ ПРЕАМБУЛУ 0xAA (Жестко ждем 1 байт маркера старта)
                // ======================================================================
                byte[]? preambleResult = await WaitForBytesAsync(1, 300);
                if (preambleResult == null) continue;

                byte singleByte = preambleResult[0];

                if (singleByte != 0xAA)
                {
                    System.Diagnostics.Debug.WriteLine($"[UART-GARBAGE] Пропущен байт мусора: 0x{singleByte:X2}");
                    continue;
                }

                // ======================================================================
                // 2. СТЕРИЛЬНЫЙ ПЕРЕХВАТ ЗАГЛОВКА (Жестко дочитываем остальные 4 байта)
                // ======================================================================
                byte[]? headerResult = await WaitForBytesAsync(4, 100);
                if (headerResult == null)
                {
                    System.Diagnostics.Debug.WriteLine("[UART-ERROR] Обрыв кадра: заголовок не долетел.");
                    continue;
                }

                byte modelId = headerResult[0];
                byte cmd = headerResult[1];
                byte varId = headerResult[2];
                byte elementsCount = headerResult[3];

                // Рассчитываем точную геометрию кадра полезной нагрузки
                int payloadSize = elementsCount * _expectedElementSize;
                int totalPacketSize = 5 + payloadSize + 1; // 5 байт заголовка + payload + 1 байт CRC

                // Выделяем монолитный буфер под весь пакет и упаковываем туда заголовок
                byte[] fullPacket = new byte[totalPacketSize];
                fullPacket[0] = 0xAA;
                System.Array.Copy(headerResult, 0, fullPacket, 1, 4);

                // ======================================================================
                // 3. СТЕРИЛЬНЫЙ ПЕРЕХВАТ ДАННЫХ И CRC (Жестко ждем весь остаток кадра куском!)
                // ======================================================================
                byte[]? payloadResult = await WaitForBytesAsync(payloadSize + 1, 400);
                if (payloadResult == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UART-ERROR] Обрыв кадра: данные VarId {varId} не долетели по таймауту.");
                    continue;
                }

                // Допаковываем прилетевшие данные float и CRC в наш полный пакет
                System.Array.Copy(payloadResult, 0, fullPacket, 5, payloadResult.Length);

                // Извлекаем финальный байт контрольной суммы кадра
                byte receivedCrc = fullPacket[fullPacket.Length - 1];

                // ======================================================================
                // 4. МАТЕМАТИЧЕСКАЯ ВАЛИДАЦИЯ ТВОИМ РОДНЫМ МЕТОДОМ CalculateCRC8_SAE_J1850
                // ======================================================================
                byte calculatedCrc = CalculateCRC8_SAE_J1850(fullPacket, fullPacket.Length - 1);

                if (calculatedCrc == receivedCrc)
                {
                    string rxDesc = $"RX [CMD: 0x{cmd:X2}, VarId: {varId}, Len: {elementsCount}]";
                    //WpfCalibrator.Views.UartMonitorWindow.LogPacket("<-- RX", "#00FF00", rxDesc, fullPacket);
                    OnLogPacket?.Invoke("<-- RX", "#00FF00", rxDesc, fullPacket);
                    // АСИНХРОННЫЙ ТРИГГЕР: Разблокируем шлагбаум
                    var tcs = _responseCompletionSource;
                    if (tcs != null && (int)cmd == _expectedCmd && (int)varId == _expectedVarId)
                    {
                        tcs.TrySetResult(true);
                    }

                    // 1. Распаковываем сырые байты полезной нагрузки в чистый плоский double[]
                    double[] decodedData = DeserializeResponsePayload(varId, elementsCount, fullPacket, payloadSize);

                    // 2. СИММЕТРИЧНЫЙ ОТВЕТ: Собираем чистый объект команды на основе сохраненных ожиданий!
                    var responseCommand = new Models.NetworkCommand
                    {
                        ModelId = modelId,
                        Cmd = (Models.LinkCommand)cmd,
                        VarId = varId,
                        DataType = _expectedDataType,
                        Rows = _expectedRows,
                        Cols = _expectedCols,
                        PayloadData = decodedData
                    };

                    // 3. Выстреливаем объект наверх в MainViewModel.OnUartPacketReceived
                    DataPacketReceived?.Invoke(responseCommand);
                }
                else
                {
                    string crcErrDesc = $"[CRC ERROR] Заголовок VarId: {varId}, CMD: {cmd}. Ожидалось: 0x{calculatedCrc:X2}, Пришло: 0x{receivedCrc:X2}";
                    System.Diagnostics.Debug.WriteLine(crcErrDesc);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UART-CRITICAL-EXCEPTION]: {ex.Message}");
                await System.Threading.Tasks.Task.Delay(20);
            }
        }
    }

}

