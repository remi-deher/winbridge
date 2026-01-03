using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinBridge.App.Models;

namespace WinBridge.App.Services.Files;

public class FileSystemManager
{
    private readonly Dictionary<string, List<FileItem>> _cache = [];
    private readonly List<string> _cacheKeys = []; 
    private const int CacheLimit = 5;

    public void InvalidateCache()
    {
        _cache.Clear();
        _cacheKeys.Clear();
    }

    public List<FileItem> GetLocalItems(string path, string sortColumn = "Name", bool ascending = true)
    {
        var items = new List<FileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists) return items;

            foreach (var info in dirInfo.GetFileSystemInfos())
            {
                bool isDir = (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory;

                items.Add(new FileItem
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = isDir,
                    IsLocal = true,
                    ModifiedDate = info.LastWriteTime,
                    Size = isDir ? 0 : ((FileInfo)info).Length,
                    IconGlyph = GetIcon(info.Name, isDir)
                });
            }
        }
        catch (UnauthorizedAccessException) {  }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemManager] Erreur lecture locale: {ex.Message}");
        }

        return SortItems(items, sortColumn, ascending);
    }

    public List<FileItem> GetRemoteItems(SftpClient client, string path, string sortColumn = "Name", bool ascending = true, bool forceRefresh = false)
    {
        var items = new List<FileItem>();

        if (client == null || !client.IsConnected) return items;

        string cacheKey = $"{client.ConnectionInfo.Host}:{client.ConnectionInfo.Port}:{path}";

        if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<FileItem>? value))
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemManager] Cache Hit: {path}");
            return SortItems(value, sortColumn, ascending);
        }

        System.Diagnostics.Debug.WriteLine($"[FileSystemManager] Cache Miss: {path}");

        try
        {
            var sftpFiles = client.ListDirectory(path);

            foreach (var file in sftpFiles)
            {
                if (file.Name == "." || file.Name == "..") continue;

                items.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = file.IsDirectory,
                    IsLocal = false,
                    ModifiedDate = file.LastWriteTime,
                    Size = file.IsDirectory ? 0 : file.Length,
                    IconGlyph = GetIcon(file.Name, file.IsDirectory)
                });
            }

            _cacheKeys.Remove(cacheKey);

            _cacheKeys.Add(cacheKey);
            _cache[cacheKey] = [.. items]; 

            if (_cacheKeys.Count > CacheLimit)
            {
                string oldest = _cacheKeys[0];
                _cacheKeys.RemoveAt(0);
                _cache.Remove(oldest);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemManager] Erreur lecture distante: {ex.Message}");
            throw; 
        }

        return SortItems(items, sortColumn, ascending);
    }

    private static List<FileItem> SortItems(List<FileItem> items, string sortColumn, bool ascending)
    {
        
        var query = items.OrderByDescending(i => i.IsDirectory);

        query = sortColumn switch
        {
            "Size" => ascending ? query.ThenBy(i => i.Size) : query.ThenByDescending(i => i.Size),
            "Date" => ascending ? query.ThenBy(i => i.ModifiedDate) : query.ThenByDescending(i => i.ModifiedDate),
            _ => ascending ? query.ThenBy(i => i.Name) : query.ThenByDescending(i => i.Name),
        };
        return [.. query];
    }

    private static string GetIcon(string fileName, bool isDirectory)
    {
        if (isDirectory) return "\uE8B7"; 

        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".cs" or ".js" or ".json" or ".xml" or ".html" or ".css" or ".py" or ".sh" => "\uE943", 
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".svg" => "\uEB9F", 
            ".exe" or ".dll" or ".bat" or ".cmd" => "\uE7BA", 
            ".zip" or ".tar" or ".gz" or ".rar" or ".7z" => "\uF012", 
            ".txt" or ".md" or ".log" => "\uE8A5", 
            ".pdf" => "\uEA90", 
            _ => "\uE7C3" 
        };
    }
}
