using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using WpfCalibrator.Models;

namespace WpfCalibrator.ViewModels.WidgetViewModel;

class LegacyWidgetViewModel: BaseWidgetViewModel
{
    private LegacyWidgetViewModel() { }
    public LegacyWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
    {

        // 🚀 СВЯЗУЮЩИЙ МОСТ: Слушаем UART-изменения из бэкэнда данных!
        DataSource.PropertyChanged += (s, e) =>
        {
            // Если в ОЗУ изменилось физическое число, заставляем UI-текст пересчитаться! [1.14]
            if (e.PropertyName == "CurrentValue")
            {
                //OnPropertyChanged(nameof(CurrentValueText));
               
            }
        };
        // Аппаратно выставляем стрелки круглых и дуговых приборов под текущее рантайм-значение МК

    }

    protected override void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ПОТОК 1: Логика обработки живого скаляра-датчика телеметрии
        if (e.PropertyName == "CurrentValue" && DataSource is ScalarVariableViewModel scalar)
        {
            // ⚡️ Аппаратно пинаем стрелки и треугольники варнингов MoTeC-прибора
            //this.NotifyValueAngleChanged();
            //this.RefreshAlarmTriangles();
            //OnPropertyChanged(nameof(LedStates));

            // Если перед глазами инженера открыт осциллограф — плавно дописываем точку в лог
            if (ControlView == WidgetViewType.TimePlot)
            {
                //this.AppendPlotPoint(scalar.CurrentValue);
            }

            // Обновляем текстовый блок вывода строки на экран
            //OnPropertyChanged(nameof(CurrentValueText));
        }



    }

}
