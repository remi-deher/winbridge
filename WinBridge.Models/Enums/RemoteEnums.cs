namespace WinBridge.Models.Enums;

public enum RemoteProtocol
{
    SSH,
    WinRM
}

public enum OSCategory
{
    Linux,
    Windows
}

// Keeping old enums if needed for backward compatibility or refactoring steps, 
// but prompt implies replacement or update. 
// RemoteType and ServerOsType seem to be exactly what we need just named differently.
// I will keep existing ones to minimize breakage unless I do a full refactor.
// Actually, prompt asked for "OSCategory OSFamily".
// I'll add them to the file.

public enum RemoteType
{
    SSH,
    WinRM
}

public enum ServerOsType
{
    Unknown,
    Linux,
    Windows
}
