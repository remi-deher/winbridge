using System;

namespace WinBridge.Core.Models;

/// <summary>
/// Defines the type of credential used for authentication.
/// </summary>
public enum CredentialType
{
    /// <summary>
    /// Standard username and password authentication.
    /// </summary>
    Password,

    /// <summary>
    /// SSH Private Key authentication.
    /// </summary>
    SshKey
}

/// <summary>
/// Represents metadata for a stored credential.
/// </summary>
public class CredentialMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for the credential.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name for the credential.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the username associated with the credential.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the type of the credential (e.g., Password or SshKey).
    /// </summary>
    public CredentialType Type { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the module that owns this credential.
    /// </summary>
    public string OwnerModuleId { get; set; } = string.Empty;
}

