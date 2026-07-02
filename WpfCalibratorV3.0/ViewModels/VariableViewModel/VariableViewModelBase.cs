using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfCalibrator.ViewModels
{
    // 🔥 НАЧАЛО РЕФАКТОРИНГА: Абстрактный паспорт для всех типов параметров
    public abstract class VariableViewModelBase : INotifyPropertyChanged
    {
        // Метаданные Си-структуры прошивки МК BlackPill
        public byte Id { get; set; }
        public byte ModelId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "single";

        // Геометрия сетки данных
        /// <summary>
        /// Общее количество элементов типа float/single в ОЗУ переменной.
        /// Используется сетевым драйвером для расчета длины бинарного пакета.
        /// </summary>
        public virtual int TotalElements => 1; // Любой скаляр/датчик по умолчанию — это 1 элемент!
        /// <summary>
        /// Размер одного элемента переменной в байтах ОЗУ контроллера (1, 2, 4 байта)
        /// </summary>
        public int ElementSize { get; set; } = 4; // По умолчанию 4 байта (float)


        // Назначение: true = калибровка (RAM), false = телеметрия (датчик)
        public bool IsParam { get; set; }

        // Инструментальные лимиты шкал и критические алармы (варнинги)
        public float MinLimit { get; set; }
        public float MaxLimit { get; set; }

        /// <summary>
        /// Флаг-предохранитель: true блокирует отправку пакета записи обратно в UART при сетевом обновлении
        /// </summary>
        public bool IsUpdatingFromNetwork { get; set; } = false;

        /// <summary>
        /// Универсальное изменение значения калибровки. 
        /// Для PageUp передаем положительный step (например, 1.0), 
        /// Для PageDown — отрицательный (например, -1.0).
        /// </summary>
        public abstract void AdjustValue(double step);


        private double _scaleMin = 0, _scaleMax = 100;
        public double ScaleMin { get => _scaleMin; set { if (Math.Abs(_scaleMin - value) > 0.001) { _scaleMin = value; OnPropertyChanged(); OnPropertyChanged("MinAlarmPercent"); } } }
        public double ScaleMax { get => _scaleMax; set { if (Math.Abs(_scaleMax - value) > 0.001) { _scaleMax = value; OnPropertyChanged(); } } }


        // Глобальный механизм уведомления графики WPF об изменении параметров
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Абстрактный метод: каждый наследник ниже сам разберет свой массив байт из UART
        public abstract void UpdateDataFromRawPayload(double[] rawData);

        /// <summary>
        /// Полиморфный метод фиксации ручного ввода из виджета в ОЗУ калибратора.
        /// </summary>
        public abstract void CommitEditedValue(double parsedValue);



    }
}
