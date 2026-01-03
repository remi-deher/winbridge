using System;

namespace WinBridge.App.Models;

/// <summary>
/// Represents a registered module in the application.
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// Gets or sets the unique identifier of the module.
    /// </summary>
    [SQLite.PrimaryKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the module.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version string of the module.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the module is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module is authorized by the user.
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets or sets the security token used for module communication.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw permissions string requested by the module.
    /// </summary>
    public string PermissionsRaw { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the module was last seen or loaded.
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of registered UI extensions.
    /// </summary>
    public string? ExtensionsJson { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of supported operating systems.
    /// </summary>
    public string? SupportedOsJson { get; set; }
}
