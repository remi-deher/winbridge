using System;
using System.Threading.Tasks;
using WinBridge.Models.Entities;

namespace WinBridge.SDK
{
    public interface ISshService
    {
        event Action<string> DataReceived;
        Task ConnectAsync(ServerModel server);
        void StartTerminal();
        void ResizeTerminal(int cols, int rows);
        void SendData(string data);
        Task<string> ExecuteCommandAsync(string command);
    }
}
