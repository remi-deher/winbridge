namespace WinBridge.Core;

/// <summary>
/// Defines global constants and default configuration values used throughout the WinBridge solution.
/// </summary>
public static class WinBridgeConstants
{
    /// <summary>
    /// Gets the standard identifier for the Named Pipe used for local inter-process communication.
    /// </summary>
    public const string PipeName = "WinBridge_Communication_Pipe";

    /// <summary>
    /// Gets the default loopback IPv4 address (localhost) used for binding the internal server.
    /// </summary>
    public const string DefaultIp = "127.0.0.1";
}