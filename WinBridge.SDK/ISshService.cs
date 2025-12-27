using System;
using System.Threading.Tasks;
using WinBridge.Models.Entities;

namespace WinBridge.SDK
{
    /// <summary>
    /// Defines specific contracts for SSH communication.
    /// Provides advanced terminal control capabilities like resizing and interactive shell management.
    /// </summary>
    public interface ISshService
    {
        /// <summary>
        /// Event raised when raw data is received from the SSH stream (stdout/stderr).
        /// </summary>
        event Action<string> DataReceived;

        /// <summary>
        /// Establishes an SSH connection to the specified server.
        /// </summary>
        /// <param name="server">The target server model.</param>
        Task ConnectAsync(ServerModel server);

        /// <summary>
        /// Starts the interactive shell/terminal session.
        /// Must be called after connection is established to enable <see cref="SendData"/>.
        /// </summary>
        void StartTerminal();

        /// <summary>
        /// Resizes the pseudo-terminal (PTY) on the remote server.
        /// Essential for keeping the backend in sync with the frontend UI dimensions.
        /// </summary>
        /// <param name="cols">Number of columns.</param>
        /// <param name="rows">Number of rows.</param>
        void ResizeTerminal(int cols, int rows);

        /// <summary>
        /// Sends raw characters to the interactive shell (stdin).
        /// </summary>
        /// <param name="data">The input string (commands, keystrokes).</param>
        void SendData(string data);

        /// <summary>
        /// Executes a single command in a non-interactive mode.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The command result output.</returns>
        Task<string> ExecuteCommandAsync(string command);
    }
}
