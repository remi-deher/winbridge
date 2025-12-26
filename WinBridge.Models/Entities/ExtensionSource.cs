using System;
using System.ComponentModel.DataAnnotations;

namespace WinBridge.Models.Entities;

public class ExtensionSource
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? GitHubUrl { get; set; }
    
    public string? LocalPath { get; set; } // Path to the DLL

    public string? Version { get; set; }

    public DateTime InstalledAt { get; set; } = DateTime.Now;
}
