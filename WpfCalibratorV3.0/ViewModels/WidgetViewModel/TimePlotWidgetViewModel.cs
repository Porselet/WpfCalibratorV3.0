using HelixToolkit.Wpf;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    /// <summary>
    /// Высокоскоростной асинхронный осциллограф реального времени (TimePlot) [1.14]
    /// </summary>
    public class TimePlotWidgetViewModel : BaseWidgetViewModel
    {


        private ScalarVariableViewModel? _signal2;
        public ScalarVariableViewModel? Signal2
        {
            get => _signal2;
            set
            {
                if (_signal2 == value) return;
                if (_signal2 != null) _signal2.PropertyChanged -= OnSignal2PropertyChanged;
                _signal2 = value;
                if (_signal2 != null) _signal2.PropertyChanged += OnSignal2PropertyChanged;
                OnPropertyChanged(nameof(Signal2));
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
        private double _durationSeconds = 10.0;
        public double DurationSeconds
        {
            get => _durationSeconds;
            set
            {
                if (Math.Abs(_durationSeconds - value) > 0.01)
                {
                    _durationSeconds = Math.Max(1.0, value); // Минимум 1 секунда
                    OnPropertyChanged();
                    // Сбрасываем экран при изменении развёртки
                    _screenStartTime = DateTime.Now;
                    StreamPoints1.Clear();
                    StreamPoints2.Clear();
                }
            }
        }
        private const double GraphHeight = 100.0;  // Визуальная высота шкалы 0..100%

        public TimePlotWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            // 1. Твоя базовая инициализация (буферы, таймеры и т.д.)
            _screenStartTime = DateTime.Now;

            if (Signal2 != null)
            {
                Signal2.PropertyChanged += OnSignal2PropertyChanged;
            }
            // 👇 ВЫЗЫВАЕМ ПОСТРОЕНИЕ СТАТИЧЕСКОЙ РАЗМЕТКИ
            BuildStaticAxesAndAlarms();

        }





        // Обработчик для DataSource (канал 1)
        protected override void OnDataSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScalarVariableViewModel.CurrentValue))
            {
                ProcessIncomingPoint(DataSource as ScalarVariableViewModel, StreamPoints1);
            }
        }

        /// <summary>
        /// Ловит пулеметные тики UART второго канала
        /// </summary>
        public void OnSignal2PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Реагируем на живое значение второго датчика
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
            if (signal == null || targetBuffer == null) return;

            var now = DateTime.Now;
            double elapsed = (now - _screenStartTime).TotalSeconds;

            if (signal == DataSource && elapsed >= DurationSeconds)
            {
                StreamPoints1.Clear();
                StreamPoints2.Clear();
                _screenStartTime = now;
                elapsed = 0;
            }

            double min = signal.ScaleMin;
            double max = signal.ScaleMax;
            double normY = 0;

            if (max > min)
            {
                double clamped = Math.Clamp(signal.CurrentValue, min, max);
                normY = ((clamped - min) / (max - min)) * GraphHeight;
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                double scaledX = -95.0 + (elapsed / DurationSeconds) * 200.0;
                var newPoint = new Point3D(scaledX, normY, 0);

                if (targetBuffer.Count > 0)
                {
                    var lastPoint = targetBuffer[targetBuffer.Count - 1];
                    targetBuffer.Add(lastPoint);
                    targetBuffer.Add(newPoint);
                }
                else
                {
                    targetBuffer.Add(newPoint);
                    targetBuffer.Add(newPoint);
                }

                if (targetBuffer == StreamPoints1) OnPropertyChanged(nameof(StreamPoints1));
                if (targetBuffer == StreamPoints2) OnPropertyChanged(nameof(StreamPoints2));
            });
        }
        private void BuildAlarmLines()
        {
            System.Diagnostics.Debug.WriteLine(">> BuildAlarmLines() called");
            var Signal1 = DataSource as ScalarVariableViewModel;
            if (Signal1 == null)
            {
                System.Diagnostics.Debug.WriteLine("   Signal1 is NULL");
                return;
            }

            double min = Signal1.ScaleMin;
            double max = Signal1.ScaleMax;
            double range = max - min;

            System.Diagnostics.Debug.WriteLine($"   ScaleMin={min}, ScaleMax={max}, range={range}");

            if (range <= 0)
            {
                System.Diagnostics.Debug.WriteLine("   Range <= 0, skipping");
                return;
            }

            double normMin = (Signal1.AlarmMin - min) / range * 100.0;
            double normMax = (Signal1.AlarmMax - min) / range * 100.0;

            System.Diagnostics.Debug.WriteLine($"   normMin={normMin}, normMax={normMax}");

            double xMin = -5.0;
            double xMax = DurationSeconds + 5.0;

            AlarmMinPoints = new Point3DCollection
    {
        new Point3D(xMin, normMin, 0),
        new Point3D(xMax, normMin, 0)
    };

            AlarmMaxPoints = new Point3DCollection
    {
        new Point3D(xMin, normMax, 0),
        new Point3D(xMax, normMax, 0)
    };

            System.Diagnostics.Debug.WriteLine($"   AlarmMinPoints count = {AlarmMinPoints.Count}");
            System.Diagnostics.Debug.WriteLine($"   AlarmMaxPoints count = {AlarmMaxPoints.Count}");
        }

        /// <summary>
        /// Генератор динамических 3D-надписей шкал без XAML-верстки
        /// </summary>
        public void BuildAxisLabels()
        {
            var group = new Model3DGroup();
            double stepY = GraphHeight / 4.0;
            var Signal1 = DataSource as ScalarVariableViewModel;
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
                        Position = new Point3D(DurationSeconds + 0.5, currentY, 0),
                        Foreground = Brushes.OrangeRed,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left
                    };
                    group.Children.Add(labelRight.Content);
                }
            }

            group.Freeze(); // Замораживаем для многопоточного рендеринга видеокарты
            AxisLabelsContainer = group;
        }

        // 👇 НОВЫЕ КОЛЛЕКЦИИ ДЛЯ ЛИНИЙ АЛАРМОВ
        private Point3DCollection _alarmMinPoints = new Point3DCollection();
        public Point3DCollection AlarmMinPoints
        {
            get => _alarmMinPoints;
            set { _alarmMinPoints = value; OnPropertyChanged(nameof(AlarmMinPoints)); }
        }

        private Point3DCollection _alarmMaxPoints = new Point3DCollection();
        public Point3DCollection AlarmMaxPoints
        {
            get => _alarmMaxPoints;
            set { _alarmMaxPoints = value; OnPropertyChanged(nameof(AlarmMaxPoints)); }
        }

        // Конструктор


        /// <summary>
        /// Строит статическую разметку графика: оси, шкалы, линии алармов.
        /// Вызывается один раз в конструкторе.
        /// </summary>
        public void BuildStaticAxesAndAlarms()
        {
            // 1. Строим подписи шкал Y1 и Y2 (твой существующий метод)
            BuildAxisLabels(); // Переименуем RebuildGraphAxisLabels в BuildAxisLabels


                BuildAlarmLines();
        }

        


    }
}
