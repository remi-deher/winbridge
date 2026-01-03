using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Core.Models;
using System.Globalization;

namespace WinBridge.App.Services.Files;

public class SftpService(VaultService vaultService, DataService dataService)
{
    private readonly VaultService _vaultService = vaultService;
    private readonly DataService _dataService = dataService;

    private readonly ConcurrentDictionary<SftpClient, (SshClient? BastionClient, ForwardedPortLocal? Tunnel)> _activeConnections = new();

    public async Task<SftpClient> GetConnectedClientAsync(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        Debug.WriteLine($"[SftpService] Tentative de connexion vers {server.Name} ({server.Host})...");

        var targetCreds = await GetFullCredentialsAsync(server.CredentialId);
        if (string.IsNullOrEmpty(targetCreds.User))
        {
            throw new InvalidOperationException($"Identifiants manquants pour le serveur {server.Name}");
        }

        SftpClient? sftpClient = null;
        SshClient? bastionClient = null;
        ForwardedPortLocal? tunnel = null;

        try
        {
            if (server.UseBastion)
            {
                
                Debug.WriteLine($"[SftpService] Connexion via Bastion pour {server.Name}...");

                var (Host, Port, CredId) = await GetBastionConfigAsync(server);
                var bastionCreds = await GetFullCredentialsAsync(CredId);

                if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(bastionCreds.User))
                {
                    throw new InvalidOperationException("Configuration Bastion incomplÃ¨te.");
                }

                var bastionConnInfo = CreateConnectionInfo(Host, Port, bastionCreds.User, bastionCreds.Secret, bastionCreds.Type);
                bastionClient = new SshClient(bastionConnInfo)
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };
                bastionClient.HostKeyReceived += (s, e) => e.CanTrust = true; 
                bastionClient.Connect();

                if (!bastionClient.IsConnected) throw new Exception("Impossible de se connecter au Bastion.");

                int localPort = GetFreeLocalPort();
                tunnel = new ForwardedPortLocal("127.0.0.1", (uint)localPort, server.Host, (uint)server.Port);
                bastionClient.AddForwardedPort(tunnel);
                tunnel.Start();

                if (!tunnel.IsStarted) throw new Exception("Impossible de dÃ©marrer le tunnel SSH.");

                Debug.WriteLine($"[SftpService] Tunnel Ã©tabli: 127.0.0.1:{localPort} -> {server.Host}:{server.Port}");

                var sftpConnInfo = CreateConnectionInfo("127.0.0.1", localPort, targetCreds.User, targetCreds.Secret, targetCreds.Type);
                sftpClient = new SftpClient(sftpConnInfo)
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };
            }
            else
            {
                
                Debug.WriteLine($"[SftpService] Connexion directe vers {server.Name}...");

                var connInfo = CreateConnectionInfo(server.Host, server.Port, targetCreds.User, targetCreds.Secret, targetCreds.Type);
                sftpClient = new SftpClient(connInfo)
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };
            }

            sftpClient.HostKeyReceived += (s, e) => e.CanTrust = true; 
            sftpClient.Connect();

            _activeConnections.TryAdd(sftpClient, (bastionClient, tunnel));

            return sftpClient;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SftpService] Erreur connexion: {ex.Message}");

            tunnel?.Dispose();
            bastionClient?.Dispose();
            sftpClient?.Dispose();

            throw; 
        }
    }

    private static ConnectionInfo CreateConnectionInfo(string host, int port, string username, string secret, CredentialType type)
    {
        if (type == CredentialType.SshKey)
        {
            
            try
            {
                
                string normalizedKey = secret.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                
                if (!normalizedKey.EndsWith("\n")) normalizedKey += "\n";

                var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(normalizedKey));
                var keyFile = new PrivateKeyFile(keyStream); 

                var authMethod = new PrivateKeyAuthenticationMethod(username, keyFile);
                return new ConnectionInfo(host, port, username, authMethod);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SftpService] Erreur chargement clÃ© privÃ©e: {ex.Message}");
                throw new InvalidOperationException($"La clÃ© privÃ©e est invalide ou chiffrÃ©e (non supportÃ© sans passphrase). DÃ©tails: {ex.Message}", ex);
            }
        }
        else
        {
            
            return new PasswordConnectionInfo(host, port, username, secret);
        }
    }

    public void Disconnect(SftpClient client)
    {
        if (client == null) return;

        try
        {
            if (client.IsConnected) client.Disconnect();
        }
        catch (Exception ex) { Debug.WriteLine($"[SftpService] Erreur dÃ©connexion SFTP: {ex.Message}"); }
        finally { client.Dispose(); }

        if (_activeConnections.TryRemove(client, out var resources))
        {
            try
            {
                resources.Tunnel?.Stop();
                resources.Tunnel?.Dispose();

                if (resources.BastionClient?.IsConnected == true) resources.BastionClient.Disconnect();
                resources.BastionClient?.Dispose();

                Debug.WriteLine("[SftpService] Tunnel et Bastion fermÃ©s.");
            }
            catch (Exception ex) { Debug.WriteLine($"[SftpService] Erreur nettoyage ressources: {ex.Message}"); }
        }
    }

    #region Metrics

    public struct ServerStatus
    {
        public double Cpu { get; set; }
        public double RamPercent { get; set; }
        public string RamText { get; set; }
        public double DiskPercent { get; set; }
        public string DiskText { get; set; }
        public string Uptime { get; set; }
        public string OSName { get; set; }
        public string KernelVersion { get; set; }
        public string IPAddress { get; set; }
        public string NetworkInterface { get; set; }
        public string CpuModel { get; set; }
        public string CpuCores { get; set; }
        public string RamTotal { get; set; }
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
        public System.Collections.Generic.List<DiskInfo> Disks { get; set; }
        public System.Collections.Generic.List<ProcessInfo> Processes { get; set; }
    }

    public struct DiskInfo
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Used { get; set; }
        public string Mount { get; set; }
    }

    public struct ProcessInfo
    {
        public string Pid { get; set; }
        public string User { get; set; }
        public string Command { get; set; }
        public string Cpu { get; set; }
        public string Mem { get; set; }
        public string Status { get; set; }
    }

    public async Task<ServerStatus> GetServerStatusAsync(SftpClient existingSftpClient)
    {
        if (existingSftpClient == null || !existingSftpClient.IsConnected) return new ServerStatus();

        try
        {
            
            using var ssh = new SshClient(existingSftpClient.ConnectionInfo);
            await Task.Run(() => ssh.Connect());

            var cmdText = "grep 'cpu ' /proc/stat | awk '{usage=($2+$4)*100/($2+$4+$5)} END {print usage}'; " +
                          "echo \"SPLIT\"; " +
                          "free -m | awk 'NR==2{printf \"%s|%s\", $3, $2}'; " +
                          "echo \"SPLIT\"; " +
                          "df -h / | awk 'NR==2 {print $5}'; " +
                          "echo \"SPLIT\"; " +
                          "uptime -p; " +
                          "echo \"SPLIT\"; " +
                          "echo \"DEPRECATED_IP_SLOT\"; " + 
                          "echo \"SPLIT\"; " +
                          "grep -E '^(PRETTY_NAME)=' /etc/os-release | cut -d= -f2 | tr -d '\"'; " +
                          "echo \"SPLIT\"; " +
                          "ps -eo pid,user,comm,%cpu,%mem,state --sort=-%cpu; " +
                          "echo \"SPLIT\"; " +
                          "grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | tr -s ' '; " +
                          "echo \"SPLIT\"; " +
                          "nproc; " +
                          "echo \"SPLIT\"; " +
                          "uname -r; " +
                          "echo \"SPLIT\"; " +
                          "ip -o -4 route get 1.1.1.1 2>/dev/null | awk '{print $7 \"|\" $5}'; " +
                          "echo \"SPLIT\"; " +
                          "df -h | grep '^/dev/' | awk '{print $1 \"|\" $2 \"|\" $5 \"|\" $6}'; " +
                          "echo \"SPLIT\"; " +
                          "IFACE=$(ip -o -4 route get 1.1.1.1 2>/dev/null | awk '{print $5}'); grep \"$IFACE\" /proc/net/dev | awk '{print $2 \"|\" $10}'";

            var command = ssh.CreateCommand(cmdText);
            string result = await Task.Run(() => command.Execute());
            ssh.Disconnect();

            return ParseStatus(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Metrics] Error: {ex.Message}");
            return new ServerStatus();
        }
    }

    private static ServerStatus ParseStatus(string output)
    {
        var s = new ServerStatus { Processes = [] };
        var sections = output.Split(["SPLIT"], StringSplitOptions.None);

        if (sections.Length > 0 && double.TryParse(sections[0].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double cpu))
            s.Cpu = cpu;

        if (sections.Length > 1)
        {
            var parts = sections[1].Trim().Split('|');
            if (parts.Length == 2 && double.TryParse(parts[0], out double used) && double.TryParse(parts[1], out double total))
            {
                s.RamPercent = (used / total) * 100;
                s.RamText = $"{used / 1024.0:F1}/{total / 1024.0:F1} GB";
                s.RamTotal = $"{total / 1024.0:F1} GB";
            }
        }

        if (sections.Length > 2 && double.TryParse(sections[2].Trim().TrimEnd('%'), out double disk))
        {
            s.DiskPercent = disk;
            s.DiskText = $"{disk}%";
        }

        if (sections.Length > 3) s.Uptime = sections[3].Trim();
        
        if (sections.Length > 5) s.OSName = sections[5].Trim();

        if (sections.Length > 6)
        {
            var lines = sections[6].Trim().Split('\n');
            foreach (var line in lines)
            {
                var cols = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length >= 6 && cols[0] != "PID")
                {
                    s.Processes.Add(new ProcessInfo
                    {
                        Pid = cols[0],
                        User = cols[1],
                        Command = cols[2],
                        Cpu = cols[3],
                        Mem = cols[4],
                        Status = cols[5]
                    });
                }
            }
        }

        if (sections.Length > 7) s.CpuModel = sections[7].Trim();
        if (sections.Length > 8) s.CpuCores = sections[8].Trim();
        if (sections.Length > 9) s.KernelVersion = sections[9].Trim();

        if (sections.Length > 10)
        {
            var parts = sections[10].Trim().Split('|');
            if (parts.Length >= 1) s.IPAddress = parts[0];
            if (parts.Length >= 2) s.NetworkInterface = parts[1];
        }

        if (sections.Length > 11)
        {
            var lines = sections[11].Trim().Split('\n');
            s.Disks = [];
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    s.Disks.Add(new DiskInfo { Name = parts[0], Size = parts[1], Used = parts[2], Mount = parts[3] });
                }
            }
        }

        if (sections.Length > 12)
        {
            var parts = sections[12].Trim().Split('|');
            if (parts.Length == 2 && long.TryParse(parts[0], out long rx) && long.TryParse(parts[1], out long tx))
            {
                s.RxBytes = rx;
                s.TxBytes = tx;
            }
        }

        return s;
    }

    public static async Task ExecuteProcessActionAsync(SftpClient existingSftpClient, string pid, string action)
    {
        if (existingSftpClient == null || !existingSftpClient.IsConnected) return;

        try
        {
            using var ssh = new SshClient(existingSftpClient.ConnectionInfo);
            await Task.Run(() => ssh.Connect());

            string cmd = "";
            switch (action.ToUpper())
            {
                case "STOP":
                    cmd = $"kill -9 {pid}";
                    break;
                case "RESTART":

                    cmd = $"echo 'Restart not fully implemented for PID {pid}'";
                    break;
            }

            if (!string.IsNullOrEmpty(cmd))
            {
                var command = ssh.CreateCommand(cmd);
                await Task.Run(() => command.Execute());
            }
            ssh.Disconnect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessAction] Error: {ex.Message}");
        }
    }

    #endregion

    #region Helpers

    private async Task<(string User, string Secret, CredentialType Type)> GetFullCredentialsAsync(int? credentialId)
    {
        if (!credentialId.HasValue) return ("", "", CredentialType.Password);

        var cred = await _dataService.GetCredentialByIdAsync(credentialId.Value);
        if (cred == null) return ("", "", CredentialType.Password);

        string secret = VaultService.RetrieveSecret($"Credential_{credentialId.Value}") ?? "";
        return (cred.UserName, secret, cred.Type);
    }

    private async Task<(string Host, int Port, int? CredId)> GetBastionConfigAsync(Server server)
    {
        if (server.BastionServerId.HasValue)
        {
            var bServer = await _dataService.GetServerByIdAsync(server.BastionServerId.Value);
            if (bServer != null)
            {
                return (bServer.Host, bServer.Port, bServer.CredentialId);
            }
        }
        return (server.BastionHost ?? "", server.BastionPort, server.BastionCredentialId);
    }

    private static int GetFreeLocalPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    #endregion

    #region File Operations (Permissions, Archives)

    public static async Task<string?> GetFilePermissionsAsync(SftpClient client, string remotePath)
    {
        
        return await Task.FromResult<string?>("755"); 
        
    }

    public static async Task SetFilePermissionsAsync(SftpClient client, string remotePath, string octalPermissions)
    {
        return; 
        
    }

    public static async Task<string?> CompressRemoteAsync(SftpClient sftpClient, string remotePath)
    {
        try
        {
            
            using var sshClient = new SshClient(sftpClient.ConnectionInfo);
            sshClient.Connect();

            string fileName = Path.GetFileName(remotePath);
            string directory = Path.GetDirectoryName(remotePath)?.Replace('\\', '/') ?? "/";
            string archiveName = $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}.tar.gz";
            string archivePath = $"{directory}/{archiveName}";

            string command = $"cd '{directory}' && tar -czf '{archiveName}' '{fileName}'";

            return await Task.Run(() =>
            {
                var cmd = sshClient.RunCommand(command);
                sshClient.Disconnect();

                if (cmd.ExitStatus == 0)
                {
                    Debug.WriteLine($"[SftpService] Archive crÃ©Ã©e: {archiveName}");
                    return archivePath;
                }
                else
                {
                    Debug.WriteLine($"[SftpService] Erreur compression: {cmd.Error}");
                    return null;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SftpService] Erreur CompressRemote: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> ExtractRemoteAsync(SftpClient sftpClient, string archivePath)
    {
        try
        {
            using var sshClient = new SshClient(sftpClient.ConnectionInfo);
            sshClient.Connect();

            string directory = Path.GetDirectoryName(archivePath)?.Replace('\\', '/') ?? "/";
            string fileName = Path.GetFileName(archivePath);
            string extension = Path.GetExtension(archivePath).ToLowerInvariant();

            string? command = extension switch
            {
                ".gz" or ".tgz" => $"cd '{directory}' && tar -xzf '{fileName}'",
                ".zip" => $"cd '{directory}' && unzip '{fileName}'",
                ".bz2" => $"cd '{directory}' && tar -xjf '{fileName}'",
                _ => null
            };

            if (command == null)
            {
                Debug.WriteLine($"[SftpService] Format d'archive non supportÃ©: {extension}");
                return false;
            }

            return await Task.Run(() =>
            {
                var cmd = sshClient.RunCommand(command);
                sshClient.Disconnect();

                if (cmd.ExitStatus == 0)
                {
                    Debug.WriteLine($"[SftpService] Archive extraite: {fileName}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[SftpService] Erreur extraction: {cmd.Error}");
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SftpService] Erreur ExtractRemote: {ex.Message}");
            return false;
        }
    }

    #endregion
    #region Standard File Operations

    public static async Task RenameRemoteAsync(SftpClient client, string oldPath, string newPath)
    {
        await Task.Run(() => client.RenameFile(oldPath, newPath));
    }

    public static async Task DeleteRemoteAsync(SftpClient client, string path, bool isDirectory)
    {
        await Task.Run(() =>
        {
            if (isDirectory) client.DeleteDirectory(path);
            else client.DeleteFile(path);
        });
    }

    public static async Task CreateRemoteDirectoryAsync(SftpClient client, string path)
    {
        await Task.Run(() => client.CreateDirectory(path));
    }

    public static async Task<string> ReadRemoteTextAsync(SftpClient client, string path)
    {
        return await Task.Run(() => client.ReadAllText(path));
    }

    public static async Task WriteRemoteTextAsync(SftpClient client, string path, string content)
    {
        await Task.Run(() => client.WriteAllText(path, content));
    }

    public static async Task SetPermissionsAsync(SftpClient client, string path, string octalMode)
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(octalMode)) return;
            try
            {
                short mode = Convert.ToInt16(octalMode, 8);
                client.ChangePermissions(path, mode);
            }
            catch (Exception ex) { Debug.WriteLine($"[SftpService] Chmod error: {ex.Message}"); throw; }
        });
    }

    public static async Task<string> ReadLogTailAsync(SftpClient client, string filePath, int lines = 500, string? sudoPassword = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var ssh = new SshClient(client.ConnectionInfo);
                ssh.Connect();

                string command;
                if (!string.IsNullOrEmpty(sudoPassword))
                {

                    string escapedPassword = sudoPassword.Replace("'", "'\\''");

                    command = $"printf '%s\\n' '{escapedPassword}' | sudo -S tail -n {lines} '{filePath}' 2>&1";
                }
                else
                {
                    command = $"tail -n {lines} '{filePath}' 2>&1";
                }

                var cmd = ssh.CreateCommand(command);
                var result = cmd.Execute();

                if (!string.IsNullOrEmpty(result))
                {
                    
                    var lines_output = result.Split('\n');
                    var filtered = new System.Collections.Generic.List<string>();

                    foreach (var line in lines_output)
                    {
                        
                        if (line.Contains("[sudo] password for") ||
                            line.Contains("[sudo] mot de passe de"))
                        {
                            continue;
                        }
                        filtered.Add(line);
                    }

                    result = string.Join("\n", filtered);
                }

                if (result.Contains("Permission denied") ||
                    result.Contains("Permission non accordÃ©e") ||
                    result.Contains("impossible d'ouvrir") ||
                    result.Contains("Sorry, try again"))
                {
                    return $"[STDERR] {result.Trim()}";
                }

                return result.Trim();
            }
            catch (Exception ex)
            {
                return $"[STDERR] Erreur lors de la lecture du fichier de log : {ex.Message}";
            }
        });
    }

    #endregion
}

