using System;
using System.ComponentModel.DataAnnotations;

namespace WinBridge.Models.Entities;

public class ServerModel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Host { get; set; } = string.Empty;

    public int SshPort { get; set; } = 22;

    // Backward compatibility property mapping to SshPort
    // This allows existing code using .Port to still work for SSH defaults
    public int Port 
    { 
        get => SshPort; 
        set => SshPort = value; 
    }

    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool UseSshAgent { get; set; }
    public bool UsePrivateKey { get; set; }
    public Guid? SshKeyId { get; set; }
    
    // Config
    public WinBridge.Models.Enums.OSCategory OSFamily { get; set; } = WinBridge.Models.Enums.OSCategory.Linux;
    public WinBridge.Models.Enums.RemoteProtocol PrimaryProtocol { get; set; } = WinBridge.Models.Enums.RemoteProtocol.SSH;
    public bool IsFallbackEnabled { get; set; }
    public string? Domain { get; set; }
    public int WinRmPort { get; set; } = 5985;
    
    // Kept for backward compat / UI binding reference if needed, but logic should use OSFamily
    public WinBridge.Models.Enums.ServerOsType OperatingSystem 
    {
        get => OSFamily == WinBridge.Models.Enums.OSCategory.Linux ? WinBridge.Models.Enums.ServerOsType.Linux : WinBridge.Models.Enums.ServerOsType.Windows;
        set => OSFamily = value == WinBridge.Models.Enums.ServerOsType.Windows ? WinBridge.Models.Enums.OSCategory.Windows : WinBridge.Models.Enums.OSCategory.Linux;
    }

    // --- CACHE ---
    public string? CachedOsInfo { get; set; }
    public string? CachedKernelVersion { get; set; } // NOUVEAU
    public string? CachedIpAddress { get; set; }
    public string? CachedUptime { get; set; }
    public DateTime? LastSeen { get; set; }
}