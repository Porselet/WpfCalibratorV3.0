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
    /// Источник данных прибора (его цифровая переменная в ОЗУ).
    /// Привязывается в момент создания виджета инженером.
    /// </summary>
    public VariableViewModelBase? DataSource
    {
        get => _dataSource;
        set
        {
            // Если датчик тот же самый — ничего не делаем
            if (_dataSource == value) return;

            // Отписываемся от старого (страховка для сборщика мусора при удалении виджета)
            if (_dataSource != null) _dataSource.PropertyChanged -= OnDataSourcePropertyChanged;

            _dataSource = value;

            // Намертво привязываем уши виджета к новому датчику
            if (_dataSource != null) _dataSource.PropertyChanged += OnDataSourcePropertyChanged;

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Реактивный диспетчер: срабатывает КАЖДЫЙ РАЗ, когда в недрах UART меняется цифра датчика.
    /// </summary>
    private void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ПОТОК 1: Логика обработки живого скаляра-датчика телеметрии
        if (e.PropertyName == "CurrentValue" && DataSource is ScalarVariableViewModel scalar)
        {
            // ⚡️ Аппаратно пинаем стрелки и треугольники варнингов MoTeC-прибора
            this.NotifyValueAngleChanged();
            this.RefreshAlarmTriangles();
            OnPropertyChanged(nameof(LedStates));

            // Если перед глазами инженера открыт осциллограф — плавно дописываем точку в лог
            if (ControlView == "TimePlot")
            {
                this.AppendPlotPoint(scalar.CurrentValue);
            }

            // Обновляем текстовый блок вывода строки на экран
            OnPropertyChanged(nameof(CurrentValueText));
        }

        // ПОТОК 2: Логика обработки табличного прицела MoTeC-радара (Вынесено из скалярного блока!)
        if (DataSource is TableVariableViewModelBase tableVar)
        {
            // Если обновились координаты смещения радара в ОЗУ — виджет мгновенно перерисовывает мишень!
            if (e.PropertyName == "RadarGridOffsetX") OnPropertyChanged(nameof(RadarGridOffsetX));
            if (e.PropertyName == "RadarGridOffsetY") OnPropertyChanged(nameof(RadarGridOffsetY));
        }
    }

}