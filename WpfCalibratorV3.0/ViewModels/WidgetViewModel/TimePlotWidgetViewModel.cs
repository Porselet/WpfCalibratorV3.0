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
        private ScalarVariableViewModel? _signal1;
        public ScalarVariableViewModel? Signal1
        {
            get => _signal1;
            set
            {
                // 1. Чистоплотно снимаем старый наушник, защищая ОЗУ от мусора
                if (_signal1 != null) _signal1.PropertyChanged -= OnSignal1PropertyChanged;

                _signal1 = value;

                // 2. Уведомляем XAML-картинку на экране
                OnPropertyChanged(nameof(Signal1));

                // 3. 🎯 КРИТИЧЕСКИЙ ПАЯЛЬНИК: Намертво припаиваем провод к живому потоку UART!
                if (_signal1 != null) _signal1.PropertyChanged += OnSignal1PropertyChanged;
            }
        }

        private ScalarVariableViewModel? _signal2;
        public ScalarVariableViewModel? Signal2
        {
            get => _signal2;
            set
            {
                if (_signal2 == value) return;

                // 1. Отписываемся от старого объекта, чтобы избежать утечек памяти
                if (_signal2 != null)
                {
                    _signal2.PropertyChanged -= OnSignal2PropertyChanged;
                }

                _signal2 = value;

                // 2. Подписываемся на новый объект, если он не null
                if (_signal2 != null)
                {
                    _signal2.PropertyChanged += OnSignal2PropertyChanged;
                }

                // 3. Уведомляем UI об изменении самого свойства Signal2
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
        private double _maxDurationSeconds = 10.0; // Длина оси X в секундах
        private const double GraphHeight = 100.0;  // Визуальная высота шкалы 0..100%

        public TimePlotWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            // 1. Твоя базовая инициализация (буферы, таймеры и т.д.)
            _screenStartTime = DateTime.Now;

            if (DataSource != null)
            {
                DataSource.PropertyChanged += OnSignal1PropertyChanged;
            }
            if (Signal2 != null)
            {
                Signal2.PropertyChanged += OnSignal2PropertyChanged;
            }


        }





        /// <summary>
        /// Ловит пулеметные тики UART первого канала
        /// </summary>
        private void OnSignal1PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 🎯 ПРАВИЛЬНЫЙ ФИЛЬТР: Реагируем на изменение живого физического значения датчика!
            if (e.PropertyName == nameof(ScalarVariableViewModel.CurrentValue))
            {
                ProcessIncomingPoint((DataSource as ScalarVariableViewModel), StreamPoints1);
            }
        }

        /// <summary>
        /// Ловит пулеметные тики UART второго канала
        /// </summary>
        private void OnSignal2PropertyChanged(object sender, PropertyChangedEventArgs e)
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

            // 🎯 УМНЫЙ СБРОС: Стираем экран ТОЛЬКО по команде Канала 1, 
            // чтобы асинхронный Канал 2 не сбивал общую точку отсчета времени!
            if (signal == DataSource && elapsed >= _maxDurationSeconds)
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
                // Растягиваем проценты на 100 единиц высоты нашей сетки
                normY = ((clamped - min) / (max - min)) * 100.0;
            }
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Точный масштаб Х: при elapsed=0 получим -95, при 10 сек получим +105
                double scaledX = -95.0 + (elapsed / _maxDurationSeconds) * 200.0;
                var newPoint = new Point3D(scaledX, normY, 0);
                if (targetBuffer.Count > 0)
                {
                    var lastPoint = targetBuffer[targetBuffer.Count - 1];
                    targetBuffer.Add(lastPoint); // конец прошлого отрезка
                    targetBuffer.Add(newPoint);  // начало нового
                }
                else
                {
                    targetBuffer.Add(newPoint); // стартовый дубликат для четности
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
