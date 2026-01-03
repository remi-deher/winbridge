using System.Collections.Generic;

namespace WinBridge.App.Models;

/// <summary>
/// Represents a stored UI extension definition from a module.
/// </summary>
public class StoredExtension
{
    /// <summary>
    /// Gets or sets the unique identifier of the extension.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title of the extension (e.g., tab name).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon glyph code for the extension.
    /// </summary>
    public string IconGlyph { get; set; } = "\uE74C"; 

    /// <summary>
    /// Gets or sets the entry point URL or path.
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the extension (often the module's local server).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session token required for access.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of extension (e.g., "ServerTab", "DashboardWidget").
    /// </summary>
    public string Type { get; set; } = "ServerTab";

    /// <summary>
    /// Gets or sets the list of OS filters for this extension.
    /// </summary>
    public List<string> OsFilter { get; set; } = [];
}
