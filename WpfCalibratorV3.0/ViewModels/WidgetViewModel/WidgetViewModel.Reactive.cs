using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Логическая вьюмодель визуального контейнера (виджета) приборной панели.
/// Наследует интерфейс INotifyPropertyChanged и связывает элементы отображения UI
/// с полиморфными объектами оперативной памяти параметров ЭБУ.
/// </summary>

public partial class WidgetViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Физический источник данных для прибора (его цифровая переменная в ОЗУ контроллера).
    /// При установке автоматически отписывается от старого объекта для предотвращения утечек памяти
    /// и подписывает реактивный диспетчер на события PropertyChanged нового датчика.
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
    /// Реактивный сетевой диспетчер прерываний: вызывается при изменении любого свойства в связанной переменной.
    /// Перехватывает обновления из потока приёма UART и маршрутизирует их по двум независимым потокам
    /// (для одиночных скаляров-датчиков и для смещения прицела радарных UniformGrid-мишеней).
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

        // ПОТОК 2: Логика обработки таблиц (2D Радар + 3D Поверхность Helix)
        if (DataSource is TableVariableViewModelBase tableVar)
        {
            // А) Если обновились координаты смещения радара в ОЗУ — двигаем мишень
            if (e.PropertyName == "RadarGridOffsetX") OnPropertyChanged(nameof(RadarGridOffsetX));
            if (e.PropertyName == "RadarGridOffsetY") OnPropertyChanged(nameof(RadarGridOffsetY));

            // Б) 🔥 ВОТ ОН — СЕТЕВОЙ ЗАПУСК 3D-ГОР:
            // Если бэкэнд сообщает, что изменился массив калибровок, 
            // и перед инженером сейчас открыта именно 3D-поверхность...
            if (e.PropertyName == "SelectedRow" || e.PropertyName == "SelectedCol" || e.PropertyName == "AnchorRow" || e.PropertyName == "AnchorCol")
            {
                if (ControlView == "Matrix3DSurface")
                {
                    // Мгновенно двигаем синие шары со скоростью 60 FPS, вообще не трогая саму гору!
                    this.Refresh3DSelectionOnly();
                }
            }
            if (e.PropertyName == "MatrixData" || e.PropertyName == "CurrentValue")
            {
                if (ControlView == "Matrix3DSurface")
                {
                    //if (IsEditing || DataSource.IsUpdatingFromNetwork) return; // Защита Helix [1.14]
                    // Вызываем наш тяжелый метод пересчета мешей и триангуляции!
                    this.Rebuild3DSurfaceMesh();
                }
            }
        }

    }

}