using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels.WidgetViewModel;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public partial class BaseWidgetViewModel : INotifyPropertyChanged
{

    /// <summary>
    /// Возвращает массив из 10 логических флагов состояния светодиодов панели Shift-Light.
    /// Вычисляет процент заполнения шкалы на основе текущего значения скаляра телеметрии
    /// и последовательно активирует индикаторы с шагом в 10%.
    /// </summary>

    public bool[] LedStates
    {
        get
        {
            var states = new bool[10];
            if (DataSource is ScalarVariableViewModel scalar && scalar.ScaleMax > scalar.ScaleMin)
            {
                // Расчет % от 0 до 100 и зажигание цепочки [1.14]
                double pct = (scalar.CurrentValue - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin) * 100.0;
                for (int i = 0; i < 10; i++) states[i] = pct >= ((i + 1) * 10.0);
            }
            return states;
        }
    }




    /// <summary>
    /// Принудительно генерирует уведомления PropertyChanged для всех визуальных маркеров аварийных лимитов.
    /// Синхронно смещает красные треугольники варнингов на горизонтальных, вертикальных,
    /// круглых и дуговых приборах, а также обновляет ограничительные линии осциллографа.
    /// </summary>

    public void RefreshAlarmTriangles()
    {
        OnPropertyChanged(nameof(MinAlarmX));
        OnPropertyChanged(nameof(MaxAlarmX));

        // НОВОЕ: Пинаем графику вертикальных треугольников
        OnPropertyChanged(nameof(MinAlarmY));
        OnPropertyChanged(nameof(MaxAlarmY));

        // НОВОЕ: Обновляем углы треугольников на круглом приборе!
        OnPropertyChanged(nameof(GaugeMinAlarmAngle));
        OnPropertyChanged(nameof(GaugeMaxAlarmAngle));

        // НОВОЕ: Пинаем треугольники алармов дугового прибора!
        OnPropertyChanged(nameof(ArcGaugeMinAlarmAngle));
        OnPropertyChanged(nameof(ArcGaugeMaxAlarmAngle));

        OnPropertyChanged(nameof(PlotMinLimitY));
        OnPropertyChanged(nameof(PlotMaxLimitY));
    }


    /// <summary>
    /// Реактивно уведомляет интерфейс WPF об изменении положения динамических элементов индикации.
    /// Перерисовывает углы поворота живых стрелок круглых и дуговых приборов MoTeC,
    /// а также обновляет радиальную длину заполнения цветного сектора барграфа.
    /// </summary>

    public void NotifyValueAngleChanged()
    {
        OnPropertyChanged(nameof(GaugeValueAngle));
        OnPropertyChanged(nameof(ArcGaugeValueAngle));

        OnPropertyChanged(nameof(ArcBarFillLength));
    }

    private System.Windows.Media.PointCollection _plotPoints = new System.Windows.Media.PointCollection();
    /// <summary>
    /// Коллекция двухмерных пиксельных координат (PointCollection) для отрисовки
    /// непрерывной лог-линии ползущего осциллографа TimePlot.
    /// </summary>

    public System.Windows.Media.PointCollection PlotPoints
    {
        get => _plotPoints;
        set { _plotPoints = value; OnPropertyChanged(); }
    }
    /// <summary>
    /// Интегрирует свежее физическое значение телеметрии в историю заезда осциллографа.
    /// Масштабирует число в инвертированную пиксельную координату Y (0..100), сдвигает весь график
    /// влево на 2 пикселя, удаляет устаревшие точки и атомарно обновляет коллекцию для XAML.
    /// </summary>

    public void AppendPlotPoint(double newValue)
    {
        if (DataSource == null) return;

        // 1. Масштабируем значение в пиксели Y (0..100). Инвертируем (1.0 - pct), так как в WPF Y=0 - это верх окна!
        double range = DataSource.ScaleMax - DataSource.ScaleMin;
        double pct = (range > 0) ? (newValue - DataSource.ScaleMin) / range : 0.5;
        if (pct < 0) pct = 0;
        if (pct > 1) pct = 1;
        double pixelY = (1.0 - pct) * 100.0;

        // 2. Локально копируем коллекцию, чтобы не вызывать мерцания UI при поштучном изменении
        var currentPoints = new System.Windows.Media.PointCollection(_plotPoints);

        if (currentPoints.Count == 0)
        {
            // Если график пустой, заполняем его стартовой линией на всю ширину экрана (100 точек с шагом 2px)
            for (int i = 0; i < 100; i++)
            {
                currentPoints.Add(new System.Windows.Point(i * 2.0, pixelY));
            }
        }
        else
        {
            // Если точки есть, сдвигаем их все влево на 2 пикселя
            for (int i = 0; i < currentPoints.Count; i++)
            {
                var p = currentPoints[i];
                currentPoints[i] = new System.Windows.Point(p.X - 2.0, p.Y);
            }

            // Удаляем самую старую точку, которая улетела за левый край экрана (X < 0)
            if (currentPoints.Count > 0 && currentPoints[0].X < 0)
            {
                currentPoints.RemoveAt(0);
            }

            // Добавляем свежую точку на самый правый край (X = 200 пикселей)
            currentPoints.Add(new System.Windows.Point(200.0, pixelY));
        }

        // 3. Аппаратно обновляем свойство для мгновенной перерисовки в XAML
        PlotPoints = currentPoints;
    }



    /// <summary>
    /// Вычисляет пиксельную координату Y (0..100) для отрисовки нижней пунктирной линии
    /// критического лимита внутри осциллографа. Уводит линию на -100 пикселей за экран,
    /// если минимальный аларм отключен в прошивке контроллера.
    /// </summary>

    public double PlotMinLimitY
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Уводим линию далеко за экран, если лимит отключен

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Инвертируем координату Y (1.0 - pct), так как Y=0 — это верх рабочей области
            return (1.0 - pct) * 100.0;
        }
    }

    /// <summary>
    /// Вычисляет пиксельную координату Y (0..100) для отрисовки верхней пунктирной линии
    /// критического лимита внутри осциллографа. Уводит линию за пределы видимости,
    /// если максимальный аларм равен положительной бесконечности.
    /// </summary>

    public double PlotMaxLimitY
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (1.0 - pct) * 100.0;
        }
    }


    /// <summary>
    /// Рассчитывает высоту Y (0..180 пикселей) для размещения аварийного треугольника
    /// на вертикальных гоночных слайдерах-барграфах. Инвертирует координату для движения снизу вверх
    /// и смещает острие на 5 пикселей для точной центровки по сетке верстки.
    /// </summary>

    public double MinAlarmY
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Прячем за экран

            // Находим процентное положение лимита на шкале
            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Инвертируем координату Y (1.0 - pct), чтобы рост значения двигал треугольник снизу вверх!
            // И вычитаем 5 пикселей для центровки острия треугольника по высоте
            return ((1.0 - pct) * 180.0) - 5.0;
        }
    }

    /// <summary>
    /// Рассчитывает высоту Y (0..180 пикселей) для размещения треугольника максимальной
    /// аварии на вертикальных слайдерах. Смещает маркер за экран, если лимит отключен.
    /// </summary>

    public double MaxAlarmY
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return ((1.0 - pct) * 180.0) - 5.0;
        }
    }


    private bool _enableVisualAlarm = false;
    /// <summary>
    /// Разрешает или запрещает динамическое окрашивание фоновой подложки виджета
    /// в критических зонах аларма (настраивается калибровщиком индивидуально для каждого прибора).
    /// </summary>

    public bool EnableVisualAlarm
    {
        get => _enableVisualAlarm;
        set
        {
            if (_enableVisualAlarm != value)
            {
                _enableVisualAlarm = value;
                OnPropertyChanged();
            }
        }
    }


    /// <summary>
    /// Вычисляет текущий угол поворота живой стрелки классического круглого будильника в градусах.
    /// Безопасно масштабирует скалярное значение в рабочий сектор 240 градусов, стартуя от базовой
    /// точки 210° (положение на 8 часов) по часовой стрелке.
    /// </summary>

    public double GaugeValueAngle
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 210;

            double pct = (DataSource is ScalarVariableViewModel scalar)
                            ? (scalar.CurrentValue - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin)
                            : 0.0; // Fallback-значение 0% для таблиц или осей
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Разворачиваем стрелку по часовой стрелке на 240 градусов от стартовых 210°
            return (pct * 240.0) + 240;
        }
    }

    /// <summary>
    /// Рассчитывает угол поворота в градусах для размещения красного маркера минимальной
    /// аварии на внешнем ободке шкалы круглого аналогового прибора.
    /// </summary>

    public double GaugeMinAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 240.0) + 240;
        }
    }

    /// <summary>
    /// Рассчитывает угол поворота в градусах для размещения маркера максимального
    /// критического лимита на ободке шкалы круглого прибора.
    /// </summary>

    public double GaugeMaxAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 240.0) + 240;
        }
    }


    /// <summary>
    /// Вычисляет горизонтальную координату X для аварийного треугольника на линейных индикаторах (длина 230px).
    /// Использует Math.Clamp для жесткой фиксации маркера в границах физического корпуса прибора
    /// при получении аномальных чисел из ОЗУ.
    /// </summary>

    public double MinAlarmX
    {
        get
        {
            if (DataSource is ScalarVariableViewModel scalar)
            {
                if (double.IsNegativeInfinity(scalar.MinLimit) || (scalar.ScaleMax <= scalar.ScaleMin)) return -100;

                double pct = (scalar.MinLimit - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin);
                pct = Math.Clamp(pct, 0.0, 1.0);

                // Вычитаем 5 пикселей для идеальной центровки острия (из твоей оригинальной верстки)
                return (pct * 230.0) - 5.0;
            }
            return -100;
        }
    }

    /// <summary>
    /// Вычисляет горизонтальную координату X для треугольника максимального лимита
    /// на линейных горизонтальных приборах с учетом смещения центра острия на 5 пикселей.
    /// </summary>

    public double MaxAlarmX
    {
        get
        {
            if (DataSource is ScalarVariableViewModel scalar)
            {
                if (double.IsPositiveInfinity(scalar.MaxLimit) || (scalar.ScaleMax <= scalar.ScaleMin)) return -100;

                double pct = (scalar.MaxLimit - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin);
                pct = Math.Clamp(pct, 0.0, 1.0);

                return (pct * 230.0) - 5.0;
            }
            return -100;
        }
    }


    /// <summary>
    /// Масштабирует положение живой стрелки современного дугового прибора MoTeC Style в градусах.
    /// Разворачивает стрелку строго в пределах 180 градусов верхнего полукруга (от 9 до 3 часов).
    /// </summary>

    public double ArcGaugeValueAngle
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 180;

            double pct = (DataSource is ScalarVariableViewModel scalar)
    ? (scalar.CurrentValue - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin)
    : 0.0; // Fallback-значение 0% для таблиц или осей
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Разворачиваем геометрию ровно на 180 градусов верхнего полукруга
            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Вычисляет угол поворота для размещения треугольника минимального критического аларма
    /// на радиусной дуге прибора MoTeC Style.
    /// </summary>

    public double ArcGaugeMinAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Вычисляет угол поворота для размещения треугольника максимального лимита
    /// на радиусной дуге прибора MoTeC Style.
    /// </summary>

    public double ArcGaugeMaxAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Рассчитывает точную радиальную длину заполнения цветного светодиодного сектора дугового барграфа.
    /// Возвращает значение в радианах: от 0.0 на минимальной границе шкалы до числа Пи (3.1415) на максимуме.
    /// </summary>

    public double ArcBarFillLength
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 0;

            double pct = (DataSource is ScalarVariableViewModel scalar)
                ? (scalar.CurrentValue - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin)
                : 0.0; // Fallback-значение 0% для таблиц или осей
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Число Пи (3.1415) — это длина полной дуги верхнего полукруга
            return pct * 3.14159;
        }
    }

}