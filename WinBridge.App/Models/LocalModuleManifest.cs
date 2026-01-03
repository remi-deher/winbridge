using System.Collections.Generic;

namespace WinBridge.App.Models;

/// <summary>
/// Represents the manifest file content of a local module.
/// </summary>
public class LocalModuleManifest
{
    /// <summary>
    /// Gets or sets the unique identifier of the module.
    /// </summary>
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
    /// Gets or sets the publisher name.
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative path to the executable file.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of required permissions.
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = [];
}
