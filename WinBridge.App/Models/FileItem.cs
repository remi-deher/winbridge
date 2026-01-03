using System;

namespace WinBridge.App.Models;

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsDirectory { get; set; }
    public string FullPath { get; set; } = string.Empty;

    public bool IsLocal { get; set; }

    public string IconGlyph { get; set; } = "\uE7C3"; 

    public string SizeDisplay
    {
        get
        {
            if (IsDirectory) return "";
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = Size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
