using System;
using System.Threading.Tasks;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// НИЗКОУРОВНЕВЫЙ КОНТРАКТ СВЯЗИ (Интерфейс-паспорт сетевого движка калибратора).
    /// Определяет жесткие правила игры для управления физическим COM-портам и маршалинга команд MoTeC-style.
    /// </summary>
    public interface ICommunicationService
    {
        /// <summary>
        /// Флаг аппаратного состояния: true, если виртуальный дескриптор порта успешно открыт в ОС Windows.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// ТЕКУЩАЯ КОНФИГУРАЦИЯ ПЛАТЫ: Хранит в ОЗУ карту переменных,Rows/Cols матриц и типов данных,
        /// полученную при парсинге JSON/XML. Используется десериализатором для вычисления размеров payload.
        /// </summary>
        DeviceConfig? CurrentDeviceConfig { get; set; }

        /// <summary>
        /// СОБЫТИЕ ПРИЁМА (Блок бизнес-логики): Выстреливает наверх в MainViewModel, когда фоновый поток Parser.cs 
        /// успешно вычитал монолитный пакет из меди, проверил CRC-8 и распаковал сырой payload во float/double[].
        /// </summary>
        event Action<NetworkCommand>? DataPacketReceived;

        /// <summary>
        /// НИЗКОУРОВНЕВЫЙ СНИФФЕР-ЛОГГЕР: Выстреливает в реальном времени прямо из ListenAsync,
        /// передавая сырые HEX-байты, направление (TX/RX), цвет строки и описание в окно ⚡ UART Monitor.
        /// </summary>
        event Action<string, string, string, byte[]>? OnLogPacket;

        /// <summary>
        /// АППАРАТНЫЙ ПУСК: Создает объект SerialPort, настраивает бесконечные таймауты 
        /// и запускает фоновую задачу неблокирующего вычерпывания шины UART. ListenAsync().
        /// </summary>
        /// <param name="portName">Системное имя порта в Windows (например, "COM3")</param>
        /// <param name="baudRate">Физическая скорость обмена в бодах (например, 115200)</param>
        void Connect(string portName, int baudRate);

        /// <summary>
        /// АППАРАТНЫЙ СТОП: Сигнализирует токену отмены, принудительно гасит фоновый поток чтения,
        /// закрывает системный дескриптор порта в ядре ОС и стерильно очищает ОЗУ.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// ТРАНСПОРТНЫЙ ВЫСТРЕЛ (ОЗУ Шлагбаум): Метод берет объект команды, упаковывает его в сырые байты,
        /// взводит маски ожидания для парсера, швыряет кадр в медь и блокирует вызывающий поток BusArbiter 
        /// через TaskCompletionSource до тех пор, пока плата не ответит Handshake-пакетом (или не выйдет таймаут).
        /// </summary>
        /// <param name="cmd">Объект команды калибровки или телеметрии, сформированный планировщиком</param>
        /// <returns>true — транзакция закрыта успешно (Handshake совпал); false — таймаут обрыва связи</returns>
        Task<bool> ExecuteCommandAsync(NetworkCommand cmd);
    }
}
