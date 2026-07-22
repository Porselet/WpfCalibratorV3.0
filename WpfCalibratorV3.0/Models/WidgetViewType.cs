namespace WpfCalibrator.Models
{
    /// <summary>
    /// Строгий перечислимый тип (идентификатор) визуального типа прибора на холсте. [1.14]
    /// Заменяет собой небезопасные макросы и магические строки.
    /// </summary>
    public enum WidgetViewType
    {
        /// <summary> Одиночный редактируемый параметр (ячейка ввода константы). </summary>
        SingleParam,
        /// <summary> Одиночный цифровой текстовый индикатор живой телеметрии. </summary>
        SingleDigitalIndicator,
        /// <summary> Горизонтальный линейный ползунок-барграф с алармами. </summary>
        SliderHorizontal,
        /// <summary> Вертикальный линейный ползунок-барграф с алармами. </summary>
        SliderVertical,
        /// <summary> Круглый стрелочный калибровочный прибор с разверткой 270 градусов. </summary>
        GaugeCircular270,
        /// <summary> Дуговой светодиодный индикатор (Shift Light / Тахометр) на 10 LED. </summary>
        GaugeLED,
        /// <summary> Полярный радар-трекер траектории и удержания целей. </summary>
        RadarTracker,
        /// <summary> Плоская интерактивная калибровочная сетка ячеек. </summary>
        MatrixTable,
        /// <summary> Тяжелая трехмерная калибровочная поверхность Helix Toolkit. </summary>
        Matrix3DSurface,
        /// <summary> Высокоскоростной двухканальный асинхронный осциллограф реального времени. </summary>
        TimePlot,
        
    }
}
