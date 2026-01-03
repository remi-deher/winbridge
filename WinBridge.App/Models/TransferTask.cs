using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinBridge.App.Models;

/// <summary>
/// Defines the status of a file transfer operation.
/// </summary>
public enum TransferStatus
{
    /// <summary>
    /// Task is queued and waiting to start.
    /// </summary>
    Pending,
    /// <summary>
    /// Task is currently running.
    /// </summary>
    InProgress,
    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed,
    /// <summary>
    /// Task failed with an error.
    /// </summary>
    Failed,
    /// <summary>
    /// Task was manually cancelled by the user.
    /// </summary>
    Cancelled
}

/// <summary>
/// Defines the direction of the file transfer.
/// </summary>
public enum TransferDirection
{
    /// <summary>
    /// Upload from local to remote.
    /// </summary>
    Upload,
    /// <summary>
    /// Download from remote to local.
    /// </summary>
    Download,
    /// <summary>
    /// Transfer between two remote servers.
    /// </summary>
    ServerToServer
}

/// <summary>
/// Represents a file transfer operation managed by the application.
/// </summary>
public partial class TransferTask : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the unique task identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the destination file path.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the file being transferred.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the direction of the transfer.
    /// </summary>
    public TransferDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file in bytes.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the time the task was queued.
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the time the task started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the time the task completed (success or failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    private long _bytesTransferred;
    /// <summary>
    /// Gets or sets the number of bytes transferred so far.
    /// </summary>
    public long BytesTransferred
    {
        get => _bytesTransferred;
        set
        {
            if (_bytesTransferred != value)
            {
                _bytesTransferred = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public double ProgressPercent => TotalBytes > 0 ? (BytesTransferred * 100.0 / TotalBytes) : 0;

    private TransferStatus _status = TransferStatus.Pending;
    public TransferStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string ErrorMessage { get; set; } = string.Empty;

    public string StatusText => Status switch
    {
        TransferStatus.Pending => "En attente...",
        TransferStatus.InProgress => $"{ProgressPercent:F1}% ({FormatBytes(BytesTransferred)}/{FormatBytes(TotalBytes)})",
        TransferStatus.Completed => $"✓ Terminé ({FormatBytes(TotalBytes)})",
        TransferStatus.Failed => $"✗ Échec: {ErrorMessage}",
        TransferStatus.Cancelled => "Annulé",
        _ => "Inconnu"
    };

    public string DirectionIcon => Direction switch
    {
        TransferDirection.Upload => "↑",
        TransferDirection.Download => "↓",
        TransferDirection.ServerToServer => "↔",
        _ => "?"
    };

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
