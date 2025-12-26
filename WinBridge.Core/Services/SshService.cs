using Renci.SshNet;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Models.Entities;

namespace WinBridge.Core.Services;

public class SshService : IDisposable
{
    private SshClient? _client;
    private ShellStream? _stream;
    private bool _isDisposed;

    public event Action<string>? DataReceived;

    public void Connect(ServerModel server)
    {
        // On crée le client
        _client = new SshClient(server.Host, server.Port, server.Username, server.Password);
        _client.Connect();

        // On crée le flux
        _stream = _client.CreateShellStream("xterm", 80, 24, 800, 600, 1024);

        // Lecture par blocs de caractères pour plus de réactivité
        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (_client is { IsConnected: true } && !_isDisposed)
            {
                if (_stream.DataAvailable)
                {
                    int count = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (count > 0)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, count);
                        DataReceived?.Invoke(text);
                    }
                }
                else
                {
                    await Task.Delay(50); // Petite pause pour ne pas saturer le CPU
                }
            }
        });
    }

    public void SendCommand(string command)
    {
        if (_stream != null && _stream.CanWrite)
        {
            _stream.WriteLine(command);
            _stream.Flush(); // Force l'envoi
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _stream?.Dispose();
        _client?.Disconnect();
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}