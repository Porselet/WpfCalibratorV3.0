using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels.WidgetViewModel;

/// <summary>
/// Логическая вьюмодель визуального контейнера (виджета) приборной панели.
/// Наследует интерфейс INotifyPropertyChanged и связывает элементы отображения UI
/// с полиморфными объектами оперативной памяти параметров ЭБУ.
/// </summary>

public partial class BaseWidgetViewModel : INotifyPropertyChanged
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

    protected virtual void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    { 
    }


}