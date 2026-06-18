using System;

namespace WpfCalibrator.Models
{
    /// <summary>
    /// Модель одной строки лога для UART-монитора пакетов
    /// </summary>
    public sealed class UartLogEntry
    {
        // Точное время пакета (ЧЧ:мм:сс.fff)
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss.fff");

        // Направление: "TX -->" (Отправлено) или "<-- RX" (Принято)
        public string Direction { get; set; } = "TX";

        // Цвет строки для удобства чтения (например, TX - синий, RX - зеленый)
        public string ColorHex { get; set; } = "#007ACC";

        // Текстовое описание (какая команда, какой ID переменной)
        public string Description { get; set; } = "";

        // Сырые байты пакета в шестнадцатеричном виде (например, "AA-02-01-...")
        public string RawBytes { get; set; } = "";
    }
}
