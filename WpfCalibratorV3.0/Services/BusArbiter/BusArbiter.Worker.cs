using System;
using System.Collections.Generic;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Диспетчер обмена (Arbiter) — монопольный хозяин шины и очередей пакетов.
    /// Реализует приоритетное планирование (Калибровки > Телеметрия).
    /// </summary>
    public sealed partial class BusArbiter : IBusArbiter
    {
        /// <summary>
        /// Главный арбитражный мозг: извлекает СЛЕДУЮЩУЮ транзакцию на отправку.
        /// Калибровки имеют абсолютный приоритет над телеметрией!
        /// </summary>
        private NetworkCommand? GetNextCommand()
        {
            lock (_queueLock)
            {
                // 1. ПРИОРИТЕТ №1: Если инженер нажал кнопку — забираем калибровку без очереди!
                if (_commandQueue.Count > 0)
                {
                    return _commandQueue.Dequeue();
                }

                // 🔥 ЖЕЛЕЗОБЕТОННЫЙ ШИННЫЙ ЗАМОК:
                // Если приоритетных калибровок в очереди нет, но идет стартовая массовая вычитка 
                // параметров (IsLoadingParameters == true) — мы категорически запрещаем фоновой телеметрии 
                // лезть на шину UART! Возвращаем null, полностью освобождая шину для ответов таблиц.
                if (IsLoadingParameters)
                {
                    return null;
                }

                // 2. ПРИОРИТЕТ №2: Если калибровок нет — берем следующий датчик из кольца телеметрии
                if (_telemetryLoop.Count > 0)
                {
                    // Проверяем границы индекса (на случай, если кольцо только что перестроилось)
                    if (_currentTelemetryIndex >= _telemetryLoop.Count)
                    {
                        _currentTelemetryIndex = 0;
                    }

                    var telemetryCmd = _telemetryLoop[_currentTelemetryIndex];

                    // Смещаем указатель на следующий шаг кольца
                    _currentTelemetryIndex = (_currentTelemetryIndex + 1) % _telemetryLoop.Count;

                    return telemetryCmd;
                }

                // Если экран пустой и команд нет — на шине полная тишина
                return null;
            }
        }


        private async System.Threading.Tasks.Task WorkerLoopAsync()
        {
            while (_isRunning)
            {
                // 1. Спрашиваем у арбитражного мозга: какую команду выдать в шину следующей?
                var nextCmd = GetNextCommand();

                if (nextCmd == null)
                {
                    // Если экран пустой — спим 50 мс и проверяем заново, не забивая ЦП
                    await System.Threading.Tasks.Task.Delay(50);
                    continue;
                }

                // ======================================================================
                // 2. ЭТАП ТРАНСПОРТА: Физическая отправка и ожидание Handshake от платы
                // ======================================================================

                // ТЕСТОВЫЙ МАРКЕР ОЧЕРЕДИ: Печатаем в дебаг, кто именно летит в провод
                //System.Diagnostics.Debug.WriteLine($"[ARBITER-TX] Выстрел кадра! CMD: {nextCmd.Cmd}, VarId: {nextCmd.VarId}, Элементов: {nextCmd.Rows * nextCmd.Cols}. Время: {DateTime.Now:mm:ss.fff}");


                // Используем наш глобальный Синглтон вместо удаленного локального поля!
                bool isSuccess = await CommunicationService.AsInterface.ExecuteCommandAsync(nextCmd);

                // Если транзакция сорвалась (например, обрыв связи или таймаут), 
                // делаем микро-паузу и идем на следующий круг цикла
                // ======================================================================
                // ПРОВЕРКА УСПЕХА ТРАНСАКЦИИ С АВТО-РЕКОННЕКТОМ
                // ======================================================================
                if (!isSuccess)
                {
                    _consecutiveTimeouts++;

                    // Если плата молчит уже 3 пакета подряд — объявляем аварию на шине!
                    if (_consecutiveTimeouts == 3)
                    {
                        System.Diagnostics.Debug.WriteLine("🚨 [BUS-ALERT] Связь с МК потеряна! Переходим в режим авто-восстановления...");

                        // Стреляем событием наверх в MainViewModel (пусть перекрасит UI в желтый/красный)
                        OnConnectionStatusChanged?.Invoke(false);
                    }

                    // В режиме аварии увеличиваем паузу между попытками до 100 мс, 
                    // чтобы не насиловать процессор и дать Windows время очухаться
                    await System.Threading.Tasks.Task.Delay(100);
                    continue;
                }

                // ЕСЛИ ПАКЕТ ПРИЛЕТЕЛ УСПЕШНО:
                if (_consecutiveTimeouts >= 3)
                {
                    System.Diagnostics.Debug.WriteLine("🏁 [BUS-RECOVER] Связь с МК успешно восстановлена в рантайме!");

                    // Возвращаем статус "Все ОК" (зеленый свет в UI)
                    OnConnectionStatusChanged?.Invoke(true);
                }

                _consecutiveTimeouts = 0; // Кристально обнуляем счетчик аварий



                // ======================================================================
                // 3. МЕТРОНОМ ШИНЫ: ЖЕСТКОЕ ОКНО ТИШИНЫ (INTER-PACKET DELAY)
                // ======================================================================
                // ИСПРАВЛЕНО: Убрали несуществующий token! 
                // Делаем паузу в 20 миллисекунд ОБЯЗАТЕЛЬНОЙ после абсолютно каждого пакета.
                // Неважно, читали мы телеметрию или прогружали стартовые параметры таблиц — 
                // асинхронным триггерам C# требуется окно тишины на закрытие транзакции в ОЗУ.
                await System.Threading.Tasks.Task.Delay(20);
            }
        }

    }
}
