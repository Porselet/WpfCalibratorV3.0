using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

public sealed partial class CommunicationService : ICommunicationService, IDisposable
{
    // Сюда переносим:
    // - Метод фонового чтения (ListenAsync / ReadLoop)
    // - Метод WaitForBytesAsync (если он пока остается)
    // - Логику валидации CRC8 и вызова событий



    private async System.Threading.Tasks.Task<byte[]?> WaitForBytesAsync(int count, int timeoutMs)
    {
        byte[] buffer = new byte[count];
        int totalBytesRead = 0;

        // Засекаем системное время старта на ноутбуке
        var startTime = System.DateTime.Now;

        while (totalBytesRead < count)
        {
            // Предохранитель №1: если порт закрылся или токен отменили — мгновенно выходим
            if (_serialPort == null || !_serialPort.IsOpen || _cts.IsCancellationRequested)
            {
                return null;
            }

            // Предохранитель №2: жесткий программный таймаут транзакции
            if ((System.DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
            {
                return null;
            }

            // Смотрим, сколько байт физически лежит в буфере Windows прямо сейчас
            int available = _serialPort.BytesToRead;
            if (available > 0)
            {
                // Выгребаем из порта ровно столько, сколько привалило, но не больше, чем нам осталось дочитать
                int bytesToReadNow = System.Math.Min(available, count - totalBytesRead);

                // Вызываем прямолинейный синхронный Read, который на заполненном буфере 
                // отрабатывает за 0 наносекунд и никогда не генерирует IOException!
                int read = _serialPort.Read(buffer, totalBytesRead, bytesToReadNow);
                totalBytesRead += read;
            }
            else
            {
                // Если буфер Windows пуст — вежливо уступаем 1 мс операционной системе,
                // полностью разгружая процессор и давая USB-чипу время подгрузить байты.
                await System.Threading.Tasks.Task.Delay(1, _cts.Token);
            }
        }

        return buffer; // УСПЕХ: Ровно count байт монолитно собраны в ОЗУ компьютера!
    }

    private byte CalculateCRC8_SAE_J1850(byte[] data, int length)
    {
        byte crc = 0x00; // Начальное значение совпадает
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
            {
                // СТРОГО КАК В СИ: Сначала сдвигаем, а потом применяем XOR!
                if ((crc & 0x80) != 0)
                {
                    crc = (byte)((crc << 1) ^ 0x1D);
                }
                else
                {
                    crc = (byte)(crc << 1);
                }
            }
        }
        return crc;
    }
    /// <summary>
    /// Универсальный демаршалер: распаковывает сырые байты ответа STM32 в массив double[] 
    /// строго по правилам матлабовских типов и с учетом Column-Major структуры 2D-таблиц.
    /// </summary>
    private double[] DeserializeResponsePayload(byte varId, byte elementsCount, byte[] fullPacket, int payloadSize)
    {
        if (payloadSize == 0) return Array.Empty<double>();

        // Принимаем геометрию и размеры прямо из ОЗУ нашего сервиса связи!
        int elementSize = _expectedElementSize;
        string dataType = _expectedDataType;
        int rows = _expectedRows;
        int cols = _expectedCols;

        double[] resultPayload = new double[rows * cols];
        int byteOffset = 5; // Данные в нашем кадре fullPacket лежат строго с 5-го байта!

        // ОБРАТНЫЙ COLUMN-MAJOR РАЗБОР: Плата шлет по столбцам (Cols), внутри — по строкам (Rows)
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                int flatIndex = (r * cols) + c;

                double decodedValue = 0.0;
                switch (dataType)
                {
                    case "double": decodedValue = BitConverter.ToDouble(fullPacket, byteOffset); break;
                    case "int64": decodedValue = BitConverter.ToInt64(fullPacket, byteOffset); break;
                    case "uint64": decodedValue = BitConverter.ToUInt64(fullPacket, byteOffset); break;
                    case "int32": decodedValue = BitConverter.ToInt32(fullPacket, byteOffset); break;
                    case "uint32": decodedValue = BitConverter.ToUInt32(fullPacket, byteOffset); break;
                    case "int16": decodedValue = BitConverter.ToInt16(fullPacket, byteOffset); break;
                    case "uint16": decodedValue = BitConverter.ToUInt16(fullPacket, byteOffset); break;
                    case "int8": decodedValue = (sbyte)fullPacket[byteOffset]; break;
                    case "uint8": decodedValue = fullPacket[byteOffset]; break;
                    case "boolean":
                    case "bool": decodedValue = fullPacket[byteOffset] > 0 ? 1.0 : 0.0; break;
                    case "single":
                    default:
                        decodedValue = BitConverter.ToSingle(fullPacket, byteOffset);
                        break;
                }

                if (flatIndex >= 0 && flatIndex < resultPayload.Length)
                {
                    resultPayload[flatIndex] = decodedValue;
                }
                byteOffset += elementSize;
            }
        }

        return resultPayload;
    }

    /// <summary>
    /// Универсальный маршалер: пакует массив double[] из C# в сырые байты для STM32 
    /// строго по правилам матлабовской функции GetTypeSizeInBytes и Column-Major структуры 2D-таблиц.
    /// </summary>
    public byte[] SerializeCommandPayload(Models.NetworkCommand cmd)
    {
        // ЖЕЛЕЗОБЕТОННЫЙ ФИКС: Если команда идет на Чтение (VarRead) — payload обязан быть пустым!
        if (cmd.Cmd == Models.LinkCommand.VarRead || cmd.PayloadData == null || cmd.PayloadData.Length == 0)
        {
            return Array.Empty<byte>();
        }
        // Если полезной нагрузки нет (пакет чистого чтения телеметрии VarRead) — возвращаем пустой массив
        if (cmd.PayloadData == null || cmd.PayloadData.Length == 0)
        {
            return Array.Empty<byte>();
        }

        // 1. ЖЕСТКОЕ ВЫЧИСЛЕНИЕ РАЗМЕРА ТИПА В БАЙТАХ (Зеркало функции GetTypeSizeInBytes из MATLAB)
        int elementSize = 4; // Дефолтное значение для single
        string typeLower = cmd.DataType.ToLower().Trim();

        switch (typeLower)
        {
            case "double":
            case "int64":
            case "uint64":
                elementSize = 8;
                break;

            case "single":
            case "int32":
            case "uint32":
                elementSize = 4;
                break;

            case "int16":
            case "uint16":
                elementSize = 2;
                break;

            case "int8":
            case "uint8":
            case "boolean":
            case "bool":
                elementSize = 1;
                break;

            default:
                // Безопасный откат, если прилетел неведомый тип данных
                elementSize = 4;
                break;
        }

        // 2. Выделяем буфер под итоговую полезную нагрузку пакета
        byte[] bytePayload = new byte[cmd.Rows * cmd.Cols * elementSize];
        int byteOffset = 0;

        // 3. COLUMN-MAJOR МАРШАЛИНГ: Бежим по столбцам (Cols), внутри — по строкам (Rows)
        for (int c = 0; c < cmd.Cols; c++)
        {
            for (int r = 0; r < cmd.Rows; r++)
            {
                // Переводим двухмерные координаты в линейный Row-Major индекс C#
                int flatIndex = (r * cmd.Cols) + c;

                double rawValue = (flatIndex >= 0 && flatIndex < cmd.PayloadData.Length)
                    ? cmd.PayloadData[flatIndex]
                    : 0.0;

                // 4. ПОЛИМОРФНАЯ УПАКОВКА В БАЙТЫ (Полное соответствие матлабовской сетке типов)
                byte[] elementBytes;
                switch (typeLower)
                {
                    // --- 8 БАЙТ (64 БИТА) ---
                    case "double":
                        elementBytes = BitConverter.GetBytes(rawValue);
                        break;
                    case "int64":
                        elementBytes = BitConverter.GetBytes((long)rawValue);
                        break;
                    case "uint64":
                        elementBytes = BitConverter.GetBytes((ulong)rawValue);
                        break;

                    // --- 4 БАЙТА (32 БИТА) ---
                    case "int32":
                        elementBytes = BitConverter.GetBytes((int)rawValue);
                        break;
                    case "uint32":
                        elementBytes = BitConverter.GetBytes((uint)rawValue);
                        break;
                    case "single":
                    default:
                        elementBytes = BitConverter.GetBytes((float)rawValue);
                        break;

                    // --- 2 БАЙТА (16 БИТ) ---
                    case "int16":
                        elementBytes = BitConverter.GetBytes((short)rawValue);
                        break;
                    case "uint16":
                        elementBytes = BitConverter.GetBytes((ushort)rawValue);
                        break;

                    // --- 1 БАЙТ (8 БИТ) ---
                    case "int8":
                        elementBytes = new byte[] { (byte)((sbyte)rawValue) };
                        break;
                    case "uint8":
                        elementBytes = new byte[] { (byte)rawValue };
                        break;
                    case "boolean":
                    case "bool":
                        elementBytes = new byte[] { (byte)(rawValue > 0.5 ? 1 : 0) };
                        break;
                }

                // Копируем байты упакованного элемента в общий буфер полезной нагрузки кадра
                Buffer.BlockCopy(elementBytes, 0, bytePayload, byteOffset, elementSize);
                byteOffset += elementSize;
            }
        }

        return bytePayload;
    }


}

