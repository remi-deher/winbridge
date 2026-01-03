namespace WinBridge.Core.Models;

/// <summary>
/// Defines the Operating System type of a server.
/// </summary>
public enum OsType
{
    /// <summary>
    /// Linux-based operating system.
    /// </summary>
    Linux,
    /// <summary>
    /// Windows-based operating system.
    /// </summary>
    Windows,
    /// <summary>
    /// Other or unknown operating system.
    /// </summary>
    Other
}

/// <summary>
/// Defines the communication protocol used to connect to a server.
/// </summary>
public enum ServerProtocol
{
    /// <summary>
    /// Secure Shell (SSH) protocol.
    /// </summary>
    SSH,
    /// <summary>
    /// Windows Remote Management (WinRM) protocol.
    /// </summary>
    WinRM,
    /// <summary>
    /// Telnet protocol (unsecure).
    /// </summary>
    Telnet
}

/// <summary>
/// Represents a remote server configuration and its current status.
/// </summary>
public class Server
{
    /// <summary>
    /// Gets or sets the unique identifier for the server.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the server.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname or IP address of the server.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number for the connection (default is 22).
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// Gets or sets the protocol used for connection.
    /// </summary>
    public ServerProtocol Protocol { get; set; } = ServerProtocol.SSH;
    
    /// <summary>
    /// Gets or sets the operating system type of the server.
    /// </summary>
    public OsType Os { get; set; } = OsType.Linux;
    
    /// <summary>
    /// Gets or sets the group name the server belongs to.
    /// </summary>
    public string Group { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets a description of the server.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the ID of the credential used for authentication.
    /// </summary>
    public int? CredentialId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether fallback connection methods should be used.
    /// </summary>
    public bool EnableFallback { get; set; }

    /// <summary>
    /// Gets or sets additional arguments for the SSH connection.
    /// </summary>
    public string SshArguments { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a bastion server is used.
    /// </summary>
    public bool UseBastion { get; set; }

    /// <summary>
    /// Gets or sets the ID of the bastion server configuration if available.
    /// </summary>
    public int? BastionServerId { get; set; }

    /// <summary>
    /// Gets or sets the hostname of the bastion server.
    /// </summary>
    public string? BastionHost { get; set; }

    /// <summary>
    /// Gets or sets the port number of the bastion server (default is 22).
    /// </summary>
    public int BastionPort { get; set; } = 22;

    /// <summary>
    /// Gets or sets the ID of the credential used for the bastion server.
    /// </summary>
    public int? BastionCredentialId { get; set; }

    /// <summary>
    /// Gets or sets the number of CPU cores.
    /// </summary>
    public int CpuCount { get; set; }

    /// <summary>
    /// Gets or sets the total RAM size as a string (e.g., "16 GB").
    /// </summary>
    public string TotalRam { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OS version string.
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CPU model name.
    /// </summary>
    public string CpuModel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current CPU usage percentage.
    /// </summary>
    public double CpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the current RAM usage percentage.
    /// </summary>
    public double RamUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the current Disk usage percentage.
    /// </summary>
    public double DiskUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the detailed Operating System name.
    /// </summary>
    public string OperatingSystem { get; set; } = "Linux (Unknown)";
    
    /// <summary>
    /// Gets or sets the uptime string of the server.
    /// </summary>
    public string Uptime { get; set; } = "0h";
    
    /// <summary>
    /// Gets or sets the current status of the server (e.g., "Online", "Offline").
    /// </summary>
    public string Status { get; set; } = "Offline";
}

