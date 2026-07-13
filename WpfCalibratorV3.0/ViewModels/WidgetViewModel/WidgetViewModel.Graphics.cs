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
     /*   OnPropertyChanged(nameof(MinAlarmX));
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
        OnPropertyChanged(nameof(PlotMaxLimitY)); */
    }


    /// <summary>
    /// Реактивно уведомляет интерфейс WPF об изменении положения динамических элементов индикации.
    /// Перерисовывает углы поворота живых стрелок круглых и дуговых приборов MoTeC,
    /// а также обновляет радиальную длину заполнения цветного сектора барграфа.
    /// </summary>

    public void NotifyValueAngleChanged()
    {
     /*   OnPropertyChanged(nameof(GaugeValueAngle));
        OnPropertyChanged(nameof(ArcGaugeValueAngle));

        OnPropertyChanged(nameof(ArcBarFillLength)); */
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
        double range = (DataSource as ScalarVariableViewModel).ScaleMax - (DataSource as ScalarVariableViewModel).ScaleMin;
        double pct = (range > 0) ? (newValue - (DataSource as ScalarVariableViewModel).ScaleMin) / range : 0.5;
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



 

}