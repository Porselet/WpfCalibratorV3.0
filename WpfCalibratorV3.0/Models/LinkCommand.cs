namespace WpfCalibrator.Models
{
    /// <summary>
    /// Зеркальное перечисление команд протокола обмена (C# аналог сишного LinkCommands_t)
    /// </summary>
    public enum LinkCommand : byte
    {
        // Операция записи (C# -> STM32 ОЗУ)
        VarWrite = 0x01,

        // Операция чтения (STM32 ОЗУ -> C#)
        VarRead = 0x02,

        // Тяжелая операция сохранения калибровок во Flash микроконтроллера
        FlashSave = 0x03
    }
}
