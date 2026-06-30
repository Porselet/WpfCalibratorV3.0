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


        // Назначение: true = калибровка (RAM), false = телеметрия (датчик)
        public bool IsParam { get; set; }

        // Инструментальные лимиты шкал и критические алармы (варнинги)
        public float ScaleMin { get; set; }
        public float ScaleMax { get; set; }
        public float MinLimit { get; set; }
        public float MaxLimit { get; set; }

        /// <summary>
        /// Универсальное изменение значения калибровки. 
        /// Для PageUp передаем положительный step (например, 1.0), 
        /// Для PageDown — отрицательный (например, -1.0).
        /// </summary>
        public abstract void AdjustValue(double step);

        // Глобальный механизм уведомления графики WPF об изменении параметров
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
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
