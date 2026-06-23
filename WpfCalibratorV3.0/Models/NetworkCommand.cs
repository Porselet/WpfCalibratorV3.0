using System;

namespace WpfCalibrator.Models
{
    /// <summary>
    /// Универсальный высокоуровневый объект команды/транзакции для Диспетчера обмена
    /// </summary>
    public sealed class NetworkCommand
    {
        // Идентификатор Simulink-модели МК (ModelId: 0, 1, 2...)
        public byte ModelId { get; set; }

        // Тип команды: 0x01 (Запись), 0x02 (Чтение), 0x03 (Флеш)
        // БЫЛО: public byte Cmd { get; set; }
        // СТАЛО: Строгая типизация команды обмена без магических чисел
        public LinkCommand Cmd { get; set; }


        // Уникальный ID переменной в карте памяти устройства
        public byte VarId { get; set; }

        // Строковый тип из JSON Матлаба ("single", "int32", "boolean")
        public string DataType { get; set; } = "single";

        // Разметки многомерных матриц (для скаляров Rows=1, Cols=1)
        public int Rows { get; set; } = 1;
        public int Cols { get; set; } = 1;

        // Универсальное ОЗУ-хранилище полезной нагрузки в double.
        // Для пакетов чистого чтения телеметрии (CMD 0x02) здесь будет null.
        public double[]? PayloadData { get; set; }
    }
}
