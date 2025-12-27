using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services
{
    public class SecurePipeService
    {
        private readonly IBroadcastLogger _logger;

        public SecurePipeService(IBroadcastLogger logger)
        {
            _logger = logger;
        }

        public async Task<string> SendSecretAsync(string secret)
        {
            var pipeName = $"WinBridgeSecret_{Guid.NewGuid()}";
            
            _ = Task.Run(async () =>
            {
                NamedPipeServerStream? pipeServer = null;
                try
                {
                    // Create secure pipe accessible only by current user
                    // Note: AccessControl is platform specific (Windows)
#if WINDOWS
                    var pipeSecurity = new PipeSecurity();
                    pipeSecurity.AddAccessRule(new PipeAccessRule(
                        WindowsIdentity.GetCurrent().Owner, 
                        PipeAccessRights.FullControl, 
                        AccessControlType.Allow));

                    pipeServer = NamedPipeServerStreamAcl.Create(
                        pipeName, 
                        PipeDirection.Out, 
                        1, 
                        PipeTransmissionMode.Byte, 
                        PipeOptions.Asynchronous, 
                        0, 
                        0, 
                        pipeSecurity);
#else
                    pipeServer = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.Out,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
#endif

                    _logger.LogInfo($"Pipe {pipeName} créé pour transmission sécurisée.", "SecurePipe");

                    // Wait for connection (timeout 10s)
                    var connectTask = pipeServer.WaitForConnectionAsync();
                    if (await Task.WhenAny(connectTask, Task.Delay(10000)) == connectTask)
                    {
                        var bytes = Encoding.UTF8.GetBytes(secret);
                        await pipeServer.WriteAsync(bytes, 0, bytes.Length);
                        await pipeServer.FlushAsync();
                        _logger.LogInfo($"Secret transmis via {pipeName}.", "SecurePipe");
                    }
                    else
                    {
                        _logger.LogWarning($"Timeout attente connexion pipe {pipeName}.", "SecurePipe");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erreur SecurePipe: {ex.Message}", "SecurePipe");
                }
                finally
                {
                    pipeServer?.Dispose();
                }
            });

            return pipeName;
        }
    }
}
