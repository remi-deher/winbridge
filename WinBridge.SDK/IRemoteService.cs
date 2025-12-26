using System;
using System.Threading.Tasks;
using WinBridge.Models.Enums;

namespace WinBridge.SDK
{
    public interface IRemoteService
    {
        RemoteType Protocol { get; }
        
        event Action<string>? DataReceived;
        
        Task<string> ExecuteCommandAsync(string command);
        
        // Connect/Disconnect are often protocol specific, but we might want them here ideally if we want a full abstraction.
        // However, the prompt only explicitly asked for ExecuteCommandAsync and DataReceived.
        // Also SshService has Connect(ServerModel). We'll assume the Factory handles connection or we add Connect here.
        // The prompt implies the factory "creates" the service. The module just consumes it.
        // So the module receives an ALREADY CONNECTED service? 
        // "Mets à jour IWinBridgeModule pour que Initialize reçoive un IServiceProvider permettant de récupérer ce IRemoteService".
        // Usually modules don't connect, they just execute.
        
        void SendData(string data); // Useful for terminal interaction
    }
}
