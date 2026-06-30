using System;
using System.Collections.ObjectModel;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Модель одномерного вектора (Кривой / Калибровочной шкалы оси)
    /// </summary>
    public class CurveVariableViewModel : TableVariableViewModelBase
    {
        // Сырой массив физических значений калибровки в ОЗУ
        private double[] _vectorData = Array.Empty<double>();
        private int _activeIndex = -1;

        /// <summary>
        /// Живой массив данных одномерной шкалы
        /// </summary>
        public double[] VectorData
        {
            get => _vectorData;
            private set
            {
                _vectorData = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Коллекция строк для мгновенного вывода подписей оси в UniformGrid на UI
        /// </summary>
        public ObservableCollection<string> StringValues { get; } = new();

        /// <summary>
        /// Ссылка на физический входной датчик (например, RPM), который двигает режимную точку по этой оси
        /// </summary>
        public ScalarVariableViewModel? BoundInputChannel { get; set; }

        /// <summary>
        /// Текущий активный индекс режимной точки на оси (рассчитывается аппаратно)
        /// </summary>
        public int ActiveIndex
        {
            get => _activeIndex;
            set
            {
                if (_activeIndex == value) return;
                _activeIndex = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Реализация скоростного маршалинга одномерного вектора из UART
        /// </summary>
        public override void UpdateDataFromRawPayload(double[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return;

            // Сохраняем физические числа
            VectorData = rawData;

            // Обновляем строковую коллекцию для графики WPF в один проход
            StringValues.Clear();
            for (int i = 0; i < rawData.Length; i++)
            {
                StringValues.Add(rawData[i].ToString("F0")); // Оси обычно оцифрованы целыми числами
            }
        }

        public override void UpdateSelectionHighlight()
        {
            // Оставляем пока пустым {}, это наш безопасный плацдарм
        }

    }
}
