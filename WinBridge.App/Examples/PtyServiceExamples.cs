using System;
using System.Text;
using System.Threading.Tasks;
using WinBridge.App.Services;
using WinBridge.App.Services.Terminal;

namespace WinBridge.App.Examples;

public static class PtyServiceExamples
{

    public static async Task Example1_PowerShellSessionAsync(BridgeService bridgeService)
    {
        using var sessionManager = new TerminalSessionManager(bridgeService);

        sessionManager.DataReceived += (sender, e) =>
        {
            string output = Encoding.UTF8.GetString(e.Data);
            Console.Write(output);
        };

        sessionManager.SessionClosed += (sender, e) =>
        {
            Console.WriteLine("\n[INFO] Session fermée");
        };

        Console.WriteLine("[INFO] Démarrage de PowerShell...");
        string sessionId = sessionManager.StartLocalSession("powershell.exe", "-NoLogo", rows: 30, cols: 120);
        Console.WriteLine($"[INFO] Session {sessionId} créée");

        await Task.Delay(1000); 
        await sessionManager.SendInputAsync("Get-Process | Select-Object -First 5\r\n");

        await Task.Delay(2000);
        await sessionManager.SendInputAsync("exit\r\n");

        await Task.Delay(1000);
    }

    public static async Task Example2_CmdSessionWithResizeAsync(BridgeService bridgeService)
    {
        using var sessionManager = new TerminalSessionManager(bridgeService);

        sessionManager.DataReceived += (sender, e) =>
        {
            Console.Write(Encoding.UTF8.GetString(e.Data));
        };

        string sessionId = sessionManager.StartLocalSession("cmd.exe", "", rows: 24, cols: 80);
        Console.WriteLine($"[INFO] Session CMD {sessionId} démarrée (24x80)");

        await Task.Delay(500);
        await sessionManager.SendInputAsync("echo Hello from CMD\r\n");

        await Task.Delay(1000);
        Console.WriteLine("\n[INFO] Redimensionnement à 40x120...");
        await sessionManager.ResizeAsync(40, 120);

        await Task.Delay(1000);
        await sessionManager.SendInputAsync("exit\r\n");
    }

    public static async Task Example3_SshSessionAsync(BridgeService bridgeService, string sshHost, string sshUser)
    {
        using var sessionManager = new TerminalSessionManager(bridgeService);

        int dataReceivedCount = 0;
        sessionManager.DataReceived += (sender, e) =>
        {
            dataReceivedCount++;
            Console.Write(Encoding.UTF8.GetString(e.Data));
        };

        string sshArgs = $"{sshUser}@{sshHost} -o StrictHostKeyChecking=no";

        Console.WriteLine($"[INFO] Connexion SSH : ssh.exe {sshArgs}");
        string sessionId = sessionManager.StartLocalSession("ssh.exe", sshArgs);

        await Task.Delay(3000);

        await sessionManager.SendInputAsync("uname -a\r\n");
        await Task.Delay(2000);

        await sessionManager.SendInputAsync("exit\r\n");
        await Task.Delay(1000);

        Console.WriteLine($"\n[INFO] Total de paquets reçus : {dataReceivedCount}");
    }

    public static async Task Example4_LowLevelPtyAsync()
    {
        using var ptyService = new PtyService();

        int totalBytesReceived = 0;
        ptyService.DataReceived += (sender, data) =>
        {
            totalBytesReceived += data.Length;
            Console.Write(Encoding.UTF8.GetString(data));
        };

        ptyService.ProcessExited += (sender, e) =>
        {
            Console.WriteLine($"\n[INFO] Processus terminé. Total reçu : {totalBytesReceived} octets");
        };

        Console.WriteLine("[INFO] Démarrage de cmd.exe via PtyService...");
        var result = ptyService.StartProcessInPty("cmd.exe");
        Console.WriteLine($"[INFO] Process ID: {result.Process.Id}");

        await Task.Delay(500);
        await ptyService.WriteAsync(Encoding.UTF8.GetBytes("echo Test PtyService\r\n"));

        await Task.Delay(1000);
        await ptyService.WriteAsync(Encoding.UTF8.GetBytes("dir\r\n"));

        await Task.Delay(2000);
        await ptyService.WriteAsync(Encoding.UTF8.GetBytes("exit\r\n"));

        await Task.Delay(500);
    }

    public static async Task Example5_PerformanceTestAsync(BridgeService bridgeService)
    {
        using var sessionManager = new TerminalSessionManager(bridgeService);

        long totalBytes = 0;
        int packetCount = 0;
        var startTime = DateTime.UtcNow;

        sessionManager.DataReceived += (sender, e) =>
        {
            totalBytes += e.Data.Length;
            packetCount++;
        };

        Console.WriteLine("[INFO] Test de performance : listage récursif de C:\\Windows");
        string sessionId = sessionManager.StartLocalSession("cmd.exe");

        await Task.Delay(500);
        await sessionManager.SendInputAsync("dir C:\\Windows /s\r\n");

        await Task.Delay(15000);

        await sessionManager.SendInputAsync("exit\r\n");
        await Task.Delay(1000);

        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"\n[RÉSULTATS]");
        Console.WriteLine($"  Durée : {elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Octets reçus : {totalBytes:N0}");
        Console.WriteLine($"  Paquets : {packetCount:N0}");
        Console.WriteLine($"  Débit moyen : {totalBytes / elapsed.TotalSeconds / 1024:F2} KB/s");
    }
}
