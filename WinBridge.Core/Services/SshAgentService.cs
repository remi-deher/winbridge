using System;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services
{
    public class SshAgentService
    {
        private readonly IBroadcastLogger _logger;
        private const string DefaultPipeName = "openssh-ssh-agent";

        public SshAgentService(IBroadcastLogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsAgentAvailableAsync(string? pipePath = null)
        {
            try
            {
                var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                _logger.LogInfo($"Vérification Agent SSH (User: {currentUser})...", "Agent", null);
            }
            catch {}

            string pipeName = string.IsNullOrEmpty(pipePath) ? DefaultPipeName : pipePath.Replace(@"\\.\pipe\", "");
            
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(500); 
                return true;
            }
            catch (TimeoutException)
            {
                _logger.LogWarning($"Agent SSH: Timeout de connexion au pipe '{pipeName}'. Service non démarré ?", "Agent", null);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError($"Agent SSH: Accès refusé au pipe '{pipeName}'. Vérifiez les permissions.", "Agent", null);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Agent SSH: Non disponible ({ex.Message}).", "Agent", null);
                return false;
            }
        }
    }
}
