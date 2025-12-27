using System;
using System.Threading.Tasks;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;

namespace WinBridge.SDK
{
    /// <summary>
    /// Defines the contract for interacting with a remote server.
    /// Acts as the single entry point for command execution and data transmission.
    /// </summary>
    public interface IRemoteService
    {
        /// <summary>
        /// Gets the protocol used by this service (e.g., SSH, PowerShell).
        /// </summary>
        RemoteType Protocol { get; }
        
        /// <summary>
        /// Event raised when raw data is received from the remote server (e.g., terminal output).
        /// </summary>
        event Action<string>? DataReceived;
        
        /// <summary>
        /// Establishes a connection to the specified server.
        /// </summary>
        /// <param name="server">The target server model containing connection details.</param>
        Task ConnectAsync(ServerModel server);

        /// <summary>
        /// Executes a single command on the remote server and returns the output.
        /// This method is intended for background tasks and non-interactive execution.
        /// </summary>
        /// <param name="command">The command string to execute.</param>
        /// <returns>A task that returns the command output as a string.</returns>
        Task<string> ExecuteCommandAsync(string command);
        
        /// <summary>
        /// Gets a value indicating whether the service is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the timestamp of the last activity on this connection.
        /// </summary>
        DateTime LastActivity { get; }

        /// <summary>
        /// Closes the connection to the remote server.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Sends raw data or commands to the remote server's interactive terminal stream.
        /// Use this for interactive sessions (e.g., writing to stdin).
        /// </summary>
        /// <param name="data">The data to send.</param>
        void SendData(string data);
    }
}
