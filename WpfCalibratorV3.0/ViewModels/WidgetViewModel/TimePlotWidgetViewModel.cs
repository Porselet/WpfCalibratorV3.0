using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    /// <summary>
    /// Высокоскоростной асинхронный осциллограф реального времени (TimePlot) [1.14]
    /// </summary>
    public class TimePlotWidgetViewModel : BaseWidgetViewModel
    {
        // 1. Два независимых канала для асинхронных сигналов
        private ScalarVariableViewModel _signal1;
        public ScalarVariableViewModel Signal1
        {
            get => _signal1;
            set
            {
                if (_signal1 != null) _signal1.PropertyChanged -= OnSignal1PropertyChanged;
                _signal1 = value;
                if (_signal1 != null) _signal1.PropertyChanged += OnSignal1PropertyChanged;
                OnPropertyChanged(nameof(Signal1));
                RebuildGraphAxisLabels();
            }
        }

        private ScalarVariableViewModel _signal2;
        public ScalarVariableViewModel Signal2
        {
            get => _signal2;
            set
            {
                if (_signal2 != null) _signal2.PropertyChanged -= OnSignal2PropertyChanged;
                _signal2 = value;
                if (_signal2 != null) _signal2.PropertyChanged += OnSignal2PropertyChanged;
                OnPropertyChanged(nameof(Signal2));
                RebuildGraphAxisLabels();
            }
        }

        // 2. Буферы точек для Helix Toolkit (Z всегда 0, график плоский)
        public Point3DCollection StreamPoints1 { get; } = new Point3DCollection();
        public Point3DCollection StreamPoints2 { get; } = new Point3DCollection();

        // 3. Контейнер для 3D биллбордов шкал Y1 и Y2
        private Model3DGroup _axisLabelsContainer = new Model3DGroup();
        public Model3DGroup AxisLabelsContainer
        {
            get => _axisLabelsContainer;
            set { _axisLabelsContainer = value; OnPropertyChanged(nameof(AxisLabelsContainer)); }
        }

        // 4. Системные переменные развертки "стакана"
        private DateTime _screenStartTime = DateTime.Now;
        private double _maxDurationSeconds = 10.0; // Длина оси X в секундах
        private const double GraphHeight = 100.0;  // Визуальная высота шкалы 0..100%

        public TimePlotWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            // Если дефолтный DataSource является скаляром, автоматом вешаем его на Канал 1
            if (dataSource is ScalarVariableViewModel scalar)
            {
                Signal1 = scalar;
            }

            RebuildGraphAxisLabels();
        }

        // Обработчик пулеметного потока пакетов Канала 1
        private void OnSignal1PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScalarVariableViewModel.CurrentValue))
            {
                ProcessIncomingPoint(Signal1, StreamPoints1);
            }
        }

        // Обработчик пулеметного потока пакетов Канала 2
        private void OnSignal2PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScalarVariableViewModel.CurrentValue))
            {
                ProcessIncomingPoint(Signal2, StreamPoints2);
            }
        }

        /// <summary>
        /// Сишный асинхронный движок добавления точек с контролем переполнения времени экрана [1.14]
        /// </summary>
        private void ProcessIncomingPoint(ScalarVariableViewModel signal, Point3DCollection targetBuffer)
        {
            if (signal == null) return;

            var now = DateTime.Now;
            double elapsed = (now - _screenStartTime).TotalSeconds;

            // БАБАХ: Если любой из датчиков перешагнул край экрана (10 сек) — тотальный сброс!
            if (elapsed >= _maxDurationSeconds)
            {
                StreamPoints1.Clear();
                StreamPoints2.Clear();
                _screenStartTime = now;
                elapsed = 0;
            }

            // Нормализация физического значения (ScaleMin..ScaleMax) в проценты высоты (0..100)
            double min = signal.ScaleMin;
            double max = signal.ScaleMax;
            double normY = 0;

            if (max > min)
            {
                double clamped = Math.Clamp(signal.CurrentValue, min, max);
                normY = ((clamped - min) / (max - min)) * GraphHeight;
            }
            //double scaledX = (elapsed / _maxDurationSeconds) * 200.0-100.0;

            // Закидываем точку в конвейер Helix (атомарно для UI потока)
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Твоя рабочая формула масштабирования Х:
                double scaledX = (elapsed / _maxDurationSeconds) * 200.0 - 100.0;
                var newPoint = new Point3D(scaledX, normY, 0);

                // 🎯 ИНЖЕНЕРНЫЙ ЦЕПОЧЕЧНЫЙ АЛГОРИТМ ДЛЯ LINESVISUAL3D
                // Если в буфере уже есть точки, нам нужно связать прошлый конец с новым началом
                if (targetBuffer.Count > 0)
                {
                    // Берем точную координату самой последней точки в стакане
                    var lastPoint = targetBuffer[targetBuffer.Count - 1];

                    // Добавляем пару: сначала дублируем прошлую точку (конец отрезка),
                    // а затем добавляем новую (начало следующего отрезка).
                    // В итоге Helix нарисует сплошной сегмент от прошлой к новой!
                    targetBuffer.Add(lastPoint);
                    targetBuffer.Add(newPoint);
                }
                else
                {
                    // Если стакан пустой (самый старт или после сброса), 
                    // добавляем две одинаковые точки в качестве невидимой стартовой точки,
                    // чтобы не нарушать четность пар массива Helix
                    targetBuffer.Add(newPoint);
                    targetBuffer.Add(newPoint);
                }

                if (targetBuffer == StreamPoints1) OnPropertyChanged(nameof(StreamPoints1));
                if (targetBuffer == StreamPoints2) OnPropertyChanged(nameof(StreamPoints2));
            });

        }

        /// <summary>
        /// Генератор динамических 3D-надписей шкал без XAML-верстки
        /// </summary>
        public void RebuildGraphAxisLabels()
        {
            var group = new Model3DGroup();
            double stepY = GraphHeight / 4.0;

            // Расчет шага для левой (Y1) и правой (Y2) шкал
            double min1 = Signal1?.ScaleMin ?? 0;
            double max1 = Signal1?.ScaleMax ?? 100;
            double delta1 = (max1 - min1) / 4.0;

            double min2 = Signal2?.ScaleMin ?? 0;
            double max2 = Signal2?.ScaleMax ?? 100;
            double delta2 = (max2 - min2) / 4.0;

            for (int i = 0; i <= 4; i++)
            {
                double currentY = i * stepY;

                // Левая шкала Y1 (Синяя)
                double val1 = min1 + (delta1 * i);
                var labelLeft = new BillboardTextVisual3D
                {
                    Text = val1.ToString("F0"),
                    Position = new Point3D(-0.5, currentY, 0),
                    Foreground = Brushes.DeepSkyBlue,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                group.Children.Add(labelLeft.Content);

                // Правая шкала Y2 (Оранжевая), только если подключен 2-й сигнал
                if (Signal2 != null)
                {
                    double val2 = min2 + (delta2 * i);
                    var labelRight = new BillboardTextVisual3D
                    {
                        Text = val2.ToString("F0"),
                        Position = new Point3D(_maxDurationSeconds + 0.5, currentY, 0),
                        Foreground = Brushes.OrangeRed,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left
                    };
                    group.Children.Add(labelRight.Content);
                }
            }

            group.Freeze(); // Замораживаем для многопоточного рендеринга видеокарты
            AxisLabelsContainer = group;
        }
    }
}
