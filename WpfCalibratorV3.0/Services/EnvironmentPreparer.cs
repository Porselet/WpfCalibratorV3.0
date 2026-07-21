using System;
using System.IO;
using System.Text;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Сервис подготовки рабочей среды и автоматизации развертывания приложения.
    /// </summary>
    public static class EnvironmentPreparer
    {
        private const string DemoBatName = "demo.bat";

        /// <summary>
        /// Гарантирует наличие актуальных скриптов запуска в рабочей директории.
        /// </summary>
        public static void EnsureScriptsCreated()
        {
            try
            {
                // Точный путь к папке с исполняемым файлом
                string targetFolder = AppDomain.CurrentDomain.BaseDirectory;
                string batPath = Path.Combine(targetFolder, DemoBatName);

                // Содержимое батника в соответствии с ТЗ
                string batContent = "@echo off\r\n" +
                                    "start \"\" \"WpfCalibratorV3.0.exe\" -demo";

                // Проверяем, существует ли файл и совпадает ли его содержимое, 
                // чтобы лишний раз не дергать жесткий диск (SSD) записью
                if (File.Exists(batPath))
                {
                    string currentContent = File.ReadAllText(batPath, Encoding.Default);
                    if (currentContent == batContent)
                    {
                        return; // Файл уже актуален, уходим
                    }
                }

                // Записываем чистый скрипт запуска
                File.WriteAllText(batPath, batContent, Encoding.Default);
            }
            catch (Exception ex)
            {
                // Логируем в отладку, но не прерываем запуск основного приложения
                System.Diagnostics.Debug.WriteLine($"[EnvironmentPreparer] Критическая ошибка создания demo.bat: {ex.Message}");
            }
        }
    }
}
