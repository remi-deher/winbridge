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

    public int Port { get; set; } = 22;

    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool UseSshAgent { get; set; }
    public bool UsePrivateKey { get; set; }
    public Guid? SshKeyId { get; set; }

    // --- CACHE ---
    public string? CachedOsInfo { get; set; }
    public string? CachedKernelVersion { get; set; } // NOUVEAU
    public string? CachedIpAddress { get; set; }
    public string? CachedUptime { get; set; }
    public DateTime? LastSeen { get; set; }
}