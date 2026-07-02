using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public partial class WidgetViewModel : INotifyPropertyChanged
{


    /// <summary>
    /// Статус 10 LED Shift-Light (true = горит) [1.14]
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
    /// Метод для принудительного обновления графики треугольников извне
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
    /// Открытый метод для принудительного уведомления UI об изменении угла живой стрелки
    /// </summary>
    public void NotifyValueAngleChanged()
    {
        OnPropertyChanged(nameof(GaugeValueAngle));
        OnPropertyChanged(nameof(ArcGaugeValueAngle));

        OnPropertyChanged(nameof(ArcBarFillLength));
    }

    private System.Windows.Media.PointCollection _plotPoints = new System.Windows.Media.PointCollection();
    /// <summary>
    /// Коллекция точек для отрисовки ползущего осциллографа TimePlot
    /// </summary>
    public System.Windows.Media.PointCollection PlotPoints
    {
        get => _plotPoints;
        set { _plotPoints = value; OnPropertyChanged(); }
    }
    /// <summary>
    /// Добавляет новое значение в историю заезда и сдвигает график осциллографа влево
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
    /// Пиксельная координата Y для линии минимального аларма (0..100)
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
    /// Пиксельная координата Y для линии максимального аларма (0..100)
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
    /// Координата Y для треугольника минимального аларма на вертикальном слайдере (0..180 пикселей)
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
    /// Координата Y для треугольника максимального аларма на вертикальном слайдере (0..180 пикселей)
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
    /// Разрешение окрашивать фон этого конкретного виджета при критическом аларме
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
    /// Текущий угол поворота живой стрелки прибора в градусах (Ноль = 150° (8 часов), Макс = 60° (4 часа))
    /// </summary>
    /// <summary>
    /// Текущий угол поворота живой стрелки прибора в градусах (Возврат к проверенной логике прибавки)
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
    /// Угол поворота для красного треугольника минимального аларма (Gauge Min)
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
    /// Угол поворота для красного треугольника максимального аларма (Gauge Max)
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
    /// Координата X для треугольника минимального аларма (смещенная на центр острия) [1.14]
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
    /// Координата X для треугольника максимального аларма (смещенная на центр острия) [1.14]
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
    /// Угол поворота/заполнения для дугового прибора MoTeC Style (Ноль = 180° (9 часов), Финиш = 360° (3 часа))
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
    /// Угол поворота для красного треугольника минимального аларма на дуге MoTeC
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
    /// Угол поворота для красного треугольника максимального аларма на дуге MoTeC
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
    /// Длина заполнения гоночного барграфа MoTeC (от 0.0 на нуле до 3.14 на максимуме)
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