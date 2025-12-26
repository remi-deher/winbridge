using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WinBridge.Models.Entities;

public class ModuleAssignment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ServerId { get; set; }

    [Required]
    public Guid ExtensionSourceId { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Navigation properties (optional but recommended for EF)
    [ForeignKey("ServerId")]
    public ServerModel? Server { get; set; }

    [ForeignKey("ExtensionSourceId")]
    public ExtensionSource? ExtensionSource { get; set; }
}
