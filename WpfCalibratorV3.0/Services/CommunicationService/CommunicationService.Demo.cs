using System;
using System.Linq;
using System.Threading.Tasks;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services
{
    public sealed partial class CommunicationService
    {
        // 🔥 ТУМБЛЕР ДЕМО-РЕЖИМА: Выстави в true для автономного Loopback-теста
        public static bool IsDemoMode { get; set; } = false;
        //public static bool IsDemoMode { get; set; } = true;

        private static double _demoPhase = 0.0;
        // Автономные ссылки на ОЗУ переменных для безопасного межпоточного доступа
        // Динамический, потокобезопасный шлюз. 
        // Сам заглянет в UI-поток в момент опроса и заберет ЖИВЫЕ, уже загруженные данные!
        // 🔥 УЛЬТИМАТИВНЫЙ ДИНАМИЧЕСКИЙ ШЛЮЗ:
        // Больше не зависит от таймингов старта! В момент каждого запроса Арбитра
        // он безопасно заглядывает в UI и забирает ЖИВЫЕ, уже загруженные из JSON коллекции.
        public static System.Collections.Generic.IEnumerable<WpfCalibrator.ViewModels.VariableViewModelBase> DemoVariablesCache
        {
            get
            {
                System.Collections.Generic.IEnumerable<WpfCalibrator.ViewModels.VariableViewModelBase> result =
                    System.Linq.Enumerable.Empty<WpfCalibrator.ViewModels.VariableViewModelBase>();

                // Используем блокирующий Invoke, чтобы Арбитр гарантированно дождался вычитки данных из UI-потока
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as WpfCalibrator.ViewModels.MainViewModel;
                    if (mainVm != null)
                    {
                        var paramsList = mainVm.ParameterVariables?.ToList() ?? new System.Collections.Generic.List<WpfCalibrator.ViewModels.VariableViewModelBase>();
                        var telemList = mainVm.TelemetryVariables?.ToList() ?? new System.Collections.Generic.List<WpfCalibrator.ViewModels.VariableViewModelBase>();

                        // Склеиваем живые списки калибровок и датчиков телеметрии
                        result = paramsList.Concat(telemList).ToList();
                    }
                });

                return result;
            }
        }
        // Виртуальное ОЗУ микроконтроллера: хранит калибровки в обход стирания
        private static readonly System.Collections.Generic.Dictionary<byte, double[]> _virtualEcuRam = new();


        /// <summary>
        /// ПОБАЙТОВЫЙ LOOPBACK-ЭМУЛЯТОР:
        /// Имитирует ответы контроллера BlackPill на уровне сырых байт протокола.
        /// </summary>
        private Task<bool> ExecuteCommandDemoLoopbackAsync(NetworkCommand cmd)
        {
            _demoPhase += 0.03; // Шаг времени

            if (cmd.Cmd == LinkCommand.VarRead)
            {
                // 1. Сгенерировали числа в границах шкал
                double[] virtualNumbers = GenerateVirtualPayload(cmd);
                cmd.PayloadData = virtualNumbers;

                // 2. Сконвертировали их в сырые байты для лога UART
                byte[] rawBytes = ConvertPayloadToRawBytes(virtualNumbers, cmd.DataType);

                // 3. Вывели красивые цветные логи TX/RX в монитор
                LogDemoPacketsToTerminal(cmd, rawBytes);

                // 4. Выстрелили событием приёма наверх в UI
                DataPacketReceived?.Invoke(cmd);
            }
            // ⚡ СЦЕНАРИЙ Б: Конвейер ЗАПИСИ (Write) -> Наш новый понедельничный рубеж!
            else if (cmd.Cmd == LinkCommand.VarWrite)
            {
                // Вызываем изолированную подфункцию сохранения в ОЗУ
                HandleDemoWriteCommand(cmd);
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// МАТЕМАТИЧЕСКИЙ ДЕНЬГИ-МЕНЕДЖЕР (Имитация логики STM32):
        /// Главный диспетчер расчёта виртуальных чисел для Loopback-пакетов.
        /// </summary>
        private double[] GenerateVirtualPayload(NetworkCommand cmd)
        {
            int totalElements = cmd.Rows * cmd.Cols;

            // Вытаскиваем живую переменную из кэша ОЗУ по её Id
            var liveVariable = DemoVariablesCache?.FirstOrDefault(v => v.Id == cmd.VarId);

            // Жестко проверяем статус: если IsParam == true, значит это калибровочная карта/ось
            bool isParameter = liveVariable?.IsParam ?? (totalElements > 1);

            // ⚡ ДЕКОМПОЗИЦИЯ: Уходим в изолированные подфункции в зависимости от типа переменной
            if (!isParameter)
            {
                // Отлаживаем только сигналы датчиков телеметрии
                return GenerateDemoSignal(cmd, liveVariable, totalElements);
            }
            else
            {
                // Отлаживаем калибровочную память (3D-карты и 1D-оси)
                return HandleDemoParameterRam(cmd, liveVariable, totalElements);
            }
        }

        /// <summary>
        /// ПОДФУНКЦИЯ 1 (СИГНАЛЫ): Рассчитывает качающиеся во времени датчики телеметрии,
        /// универсально подстраивая размах синусоиды под физические границы связанных осей 3D-карты.
        /// </summary>
        private double[] GenerateDemoSignal(NetworkCommand cmd, WpfCalibrator.ViewModels.VariableViewModelBase liveVariable, int totalElements)
        {
            double[] signalData = new double[totalElements];

            // Базовый дефолтный откат на шкалы прибора
            double min = (liveVariable as ScalarVariableViewModel)?.ScaleMin ?? 0.0;
            double max = (liveVariable as ScalarVariableViewModel)?.ScaleMax ?? 7000.0;

            // Ищем на экране любую активную 3D-таблицу, чтобы узнать, к какой оси привязан этот датчик
            var linkedTable = DemoVariablesCache?
                .OfType<WpfCalibrator.ViewModels.Map3DVariableViewModel>()
                .FirstOrDefault(t => (t.BoundInputX != null && t.BoundInputX.Id == cmd.VarId) ||
                                     (t.BoundInputY != null && t.BoundInputY.Id == cmd.VarId));

            if (linkedTable != null)
            {
                WpfCalibrator.ViewModels.CurveVariableViewModel targetAxis = null;

                // Определяем привязку по X (горизонталь) или Y (вертикаль)
                if (linkedTable.BoundInputX != null && linkedTable.BoundInputX.Id == cmd.VarId)
                {
                    targetAxis = linkedTable.BoundAxisX;
                }
                else if (linkedTable.BoundInputY != null && linkedTable.BoundInputY.Id == cmd.VarId)
                {
                    targetAxis = linkedTable.BoundAxisY;
                }

                // Если вектор-ось для датчика найдена — вытаскиваем её физические края из памяти эмулятора!
                if (targetAxis != null && _virtualEcuRam.TryGetValue(targetAxis.Id, out double[] axisPoints) && axisPoints.Length > 1)
                {
                    double axisMin = axisPoints[0];
                    double axisMax = axisPoints[axisPoints.Length - 1];

                    // 5% зазор наружу, чтобы лазер слегка заходил за края 3D рельефа
                    double offset = (axisMax - axisMin) * 0.05;

                    min = axisMin - offset;
                    max = axisMax + offset;
                }
            }

            // Считаем плавное качание
            double sinValue = Math.Sin(_demoPhase + cmd.VarId);
            double normValue = (sinValue + 1.0) / 2.0;

            for (int i = 0; i < totalElements; i++)
            {
                signalData[i] = min + (normValue * (max - min));
            }

            return signalData;
        }

        /// <summary>
        /// ПОДФУНКЦИЯ 2 (ПАРАМЕТРЫ): Имитирует статическое ОЗУ калибровок контроллера.
        /// На холодном старте вызывает метод нарезки геометрии, а далее возвращает то, что в ней лежит.
        /// </summary>
        private double[] HandleDemoParameterRam(NetworkCommand cmd, WpfCalibrator.ViewModels.VariableViewModelBase liveVariable, int totalElements)
        {
            // Если МК впервые видит этот VarId — запускаем функцию нарезки шагов осей или 3D параболы горы!
            if (!_virtualEcuRam.TryGetValue(cmd.VarId, out double[] cachedMapData) || cachedMapData.Length != totalElements)
            {
                cachedMapData = InitializeVirtualEcuRam(cmd);
                _virtualEcuRam[cmd.VarId] = cachedMapData;
            }

            // На что пришел запрос — ровно то из ОЗУ и возвращаем обратно без изменений времени!
            return cachedMapData.ToArray();
        }

        /// <summary>
        /// БАЙТОВЫЙ МАРШАЛЕР: Принимает массив чисел double и переводит его в сырой поток байт,
        /// строго опираясь на целевой тип данных (float, double, int16 и т.д.), который ждет программа.
        /// </summary>
        private byte[] ConvertPayloadToRawBytes(double[] payload, string dataType)
        {
            if (payload == null || payload.Length == 0) return Array.Empty<byte>();

            var byteList = new System.Collections.Generic.List<byte>();
            string typeLower = dataType?.ToLower() ?? "double";

            foreach (double val in payload)
            {
                byte[] elementBytes;

                switch (typeLower)
                {
                    // Упаковываем в 4 байта Single Precision float
                    case "float":
                    case "single":
                        elementBytes = BitConverter.GetBytes((float)val);
                        break;

                    // Упаковываем в 8 байт Double Precision double
                    case "double":
                        elementBytes = BitConverter.GetBytes(val);
                        break;

                    // Упаковываем в 2 байта Signed Short int16
                    case "short":
                    case "int16":
                        elementBytes = BitConverter.GetBytes((short)Math.Round(val));
                        break;

                    // Упаковываем в 4 байта Signed Int int32
                    case "int":
                    case "int32":
                        elementBytes = BitConverter.GetBytes((int)Math.Round(val));
                        break;

                    // Откат по умолчанию на базовый double
                    default:
                        elementBytes = BitConverter.GetBytes(val);
                        break;
                }

                byteList.AddRange(elementBytes);
            }

            return byteList.ToArray();
        }
        /// <summary>
        /// ДЕМО-ЛОГГЕР: Форматирует и выводит цветные отладочные сообщения в текстовый UART Терминал приложения,
        /// полностью имитируя реальный побайтовый обмен калибратора с BlackPill.
        /// </summary>
        private void LogDemoPacketsToTerminal(NetworkCommand cmd, byte[] rawBytes)
        {
            int totalElements = cmd.Rows * cmd.Cols;

            // 1. Форматируем и отправляем синюю строчку запроса отправки (ноутбук просит данные)
            string txDesc = $"DEMO TX [CMD: 0x02, VarId: {cmd.VarId}] -> Запрос чтения";
            OnLogPacket?.Invoke("TX -->", "#007ACC", txDesc, Array.Empty<byte>());

            // 2. Форматируем и отправляем зелёную строчку ответа (виртуальный ЭБУ выплёвывает байты)
            string rxDesc = $"DEMO RX [CMD: 0x02, VarId: {cmd.VarId}, Len: {totalElements}]";
            OnLogPacket?.Invoke("<-- RX", "#00FF00", rxDesc, rawBytes);
        }


        /// <summary>
        /// АППАРАТНАЯ ИНИЦИАЛИЗАЦИЯ ФЛЭШ-ПАМЯТИ МК:
        /// Строго и без усложнений нарезает линейные возрастающие шкалы для осей-векторов 
        /// и строит красивую плоскую наклонную поверхность для 3D-таблиц (повышающуюся к дальнему краю).
        /// </summary>
        private double[] InitializeVirtualEcuRam(NetworkCommand cmd)
        {
            int totalElements = cmd.Rows * cmd.Cols;
            double[] initialMapData = new double[totalElements];

            // Вытаскиваем живую переменную из кэша, чтобы забрать её реальные ScaleMin и ScaleMax из JSON
            var liveVariable = DemoVariablesCache?.FirstOrDefault(v => v.Id == cmd.VarId);

            // Безопасные дефолты: если это таблица — углы 10-45°, если это ось — например, обороты до 7000
            double min = (liveVariable as ScalarVariableViewModel)?.ScaleMin ?? (totalElements > 1 ? 10.0 : 0.0);
            double max = (liveVariable as ScalarVariableViewModel)?.ScaleMax ?? (totalElements > 1 ? 45.0 : 7000.0);
            double delta = max - min;

            // Ищем, к какому типу относится переменная в бэкэнде
            bool isCurveAxis = liveVariable is WpfCalibrator.ViewModels.CurveVariableViewModel;

            // 📈 СЦЕНАРИЙ 1: Перед нами одномерная ось-вектор (шкала оцифровки)
            // Железно выполняем гоночное условие: Значение(N) < Значение(N+1) за счет чистой линейной нарезки!
            if (isCurveAxis || cmd.Rows == 1 || cmd.Cols == 1)
            {
                if (totalElements > 1)
                {
                    for (int i = 0; i < totalElements; i++)
                    {
                        initialMapData[i] = min + ((double)i / (totalElements - 1) * delta);
                    }
                }
                else
                {
                    initialMapData[0] = min;
                }
                return initialMapData;
            }

            // 🏔️ СЦЕНАРИЙ 2: Перед нами двумерная 3D-таблица калибровок
            // Строим идеальную плоскую поверхность, плавно повышающуюся к дальнему углу
            int idx = 0;
            for (int r = 0; r < cmd.Rows; r++)
            {
                // Нормируем текущую строку от 0.0 до 1.0
                double normRow = (double)r / (cmd.Rows - 1);

                for (int c = 0; c < cmd.Cols; c++)
                {
                    // Нормируем текущую колонку от 0.0 до 1.0
                    double normCol = (double)c / (cmd.Cols - 1);

                    // Итоговое число — это линейный наклон по обеим осям. Никаких синусов и куполов!
                    double factor = (normRow + normCol) / 2.0;

                    initialMapData[idx++] = min + (factor * delta);
                }
            }

            return initialMapData;
        }

        


        /// <summary>
        /// ПОДФУНКЦИЯ ЗАПИСИ (Имитация флэш/ОЗУ контроллера):
        /// Распаковывает прилетевшие байты новой калибровки, перезаписывает виртуальную память ЭБУ
        /// и отправляет цветные логи TX/RX подтверждения успешного Handshake.
        /// </summary>
        private bool HandleDemoWriteCommand(NetworkCommand cmd)
        {
            if (cmd.PayloadData == null || cmd.PayloadData.Length == 0) return false;

            // Обновляем виртуальную память и зеркалируем команду
            _virtualEcuRam[cmd.VarId] = cmd.PayloadData.ToArray();
            byte[] rawBytes = ConvertPayloadToRawBytes(cmd.PayloadData, cmd.DataType);

            // Логирование и зеркальный ответ (эмуляция реального МК)
            OnLogPacket?.Invoke("TX -->", "#007ACC", $"DEMO TX... VarId: {cmd.VarId}", rawBytes);
            OnLogPacket?.Invoke("<-- RX", "#00FF00", $"DEMO RX... VarId: {cmd.VarId}", rawBytes);
            DataPacketReceived?.Invoke(cmd);

            return true;
        }


    }
}
