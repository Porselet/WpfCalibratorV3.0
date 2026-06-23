using System.Threading.Tasks;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services; 

public interface ICommunicationService : IDisposable
{
    bool IsConnected { get; }
    void Connect(string portName, int baudRate);
    void Disconnect();
    Task<bool> ExecuteCommandAsync(NetworkCommand cmd);
    event Action<string, string, string, byte[]>? OnLogPacket;
    public Models.DeviceConfig? CurrentDeviceConfig { get; set; }
    public event Action<Models.NetworkCommand>? DataPacketReceived;

}

