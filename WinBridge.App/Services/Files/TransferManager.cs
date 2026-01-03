using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinBridge.App.Models;

namespace WinBridge.App.Services.Files;

public class TransferProgressReport
{
    public string CurrentItemName { get; set; } = string.Empty;
    public int ItemsProcessed { get; set; }
    public int TotalItems { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TransferManager
{
    private class ProgressCounter { public int Value; }

    public ObservableCollection<TransferTask> Tasks { get; } = [];
    private readonly SemaphoreSlim _parallelLimit = new(3); 
    private readonly CancellationTokenSource _workerCts = new();
    private bool _workerStarted;

    public TransferManager()
    {
        StartQueueWorker();
    }

    public void AddTask(TransferTask task)
    {

        Tasks.Add(task);
        
    }

    public static Task StartTransferAsync(TransferTask task, SftpClient client)
    {

        return Task.Run(async () =>
        {
            task.Status = TransferStatus.InProgress;
            task.StartedAt = DateTime.Now;
            try
            {
                if (task.Direction == TransferDirection.Download)
                {
                    
                    using var fs = File.Create(task.DestinationPath);
                    var fileSize = client.GetAttributes(task.SourcePath).Size; 

                    await Task.Factory.FromAsync(
                        client.BeginDownloadFile(task.SourcePath, fs),
                        client.EndDownloadFile);

                    task.Status = TransferStatus.Completed;
                    task.CompletedAt = DateTime.Now;
                }
                else
                {
                    
                }
            }
            catch (Exception ex)
            {
                task.Status = TransferStatus.Failed;
                task.ErrorMessage = ex.Message;
            }
        });
    }

    #region Queue & Worker

    private void StartQueueWorker()
    {
        if (_workerStarted) return;
        _workerStarted = true;

        _ = Task.Run(async () =>
        {
            while (!_workerCts.Token.IsCancellationRequested)
            {
                try
                {
                    var pending = Tasks.FirstOrDefault(t => t.Status == TransferStatus.Pending);
                    if (pending != null)
                    {
                        await _parallelLimit.WaitAsync(_workerCts.Token);
                        _ = ProcessTransferTask(pending).ContinueWith(_ => _parallelLimit.Release());
                    }
                    else
                    {
                        await Task.Delay(200, _workerCts.Token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(500); }
            }
        });
    }

    private static async Task ProcessTransferTask(TransferTask task)
    {
        task.Status = TransferStatus.InProgress;
        task.StartedAt = DateTime.Now;

        try
        {

            task.Status = TransferStatus.Completed;
            task.CompletedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            task.Status = TransferStatus.Failed;
            task.ErrorMessage = ex.Message;
        }
    }

    #endregion

    #region Upload with Resume & Checksum

    public async Task UploadAsync(SftpClient client, List<string> sourcePaths, string destinationRemotePath, IProgress<TransferProgressReport>? progress, CancellationToken ct = default)
    {
        if (client == null || !client.IsConnected) throw new ArgumentException("Client SFTP non connecté.");

        var fileList = new List<(string LocalPath, string RemoteRelativePath)>();

        await Task.Run(() =>
        {
            foreach (var path in sourcePaths)
            {
                if (File.Exists(path))
                {
                    fileList.Add((path, Path.GetFileName(path)));
                }
                else if (Directory.Exists(path))
                {
                    string dirName = new DirectoryInfo(path).Name;
                    ScanLocalDirectory(path, dirName, fileList);
                }
            }
        }, ct);

        int total = fileList.Count;
        int current = 0;

        foreach (var (LocalPath, RemoteRelativePath) in fileList)
        {
            if (ct.IsCancellationRequested) break;

            current++;
            string remoteFilePath = (destinationRemotePath.TrimEnd('/') + "/" + RemoteRelativePath.Replace('\\', '/'));
            string remoteDir = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/') ?? "";

            progress?.Report(new TransferProgressReport
            {
                CurrentItemName = Path.GetFileName(LocalPath),
                ItemsProcessed = current,
                TotalItems = total,
                Message = $"Envoi de {Path.GetFileName(LocalPath)}..."
            });

            await UploadFileWithResumeAsync(client, LocalPath, remoteFilePath, ct);
        }
    }

    private async Task UploadFileWithResumeAsync(SftpClient client, string localPath, string remotePath, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                string remoteDir = Path.GetDirectoryName(remotePath)?.Replace('\\', '/') ?? "";
                EnsureRemoteDirectoryExists(client, remoteDir);

                var localInfo = new FileInfo(localPath);
                long localSize = localInfo.Length;
                long remoteSize = 0;

                try
                {
                    var remoteAttrs = client.GetAttributes(remotePath);
                    remoteSize = remoteAttrs.Size;
                }
                catch (SftpPathNotFoundException) {  }

                using (var fs = File.OpenRead(localPath))
                {
                    if (remoteSize > 0 && remoteSize < localSize)
                    {
                        
                        fs.Seek(remoteSize, SeekOrigin.Begin);
                        using var remoteStream = client.OpenWrite(remotePath);
                        remoteStream.Seek(remoteSize, SeekOrigin.Begin);
                        fs.CopyTo(remoteStream, 81920);
                    }
                    else
                    {
                        
                        client.UploadFile(fs, remotePath, true);
                    }
                }

                if (localSize > 1024 * 1024)
                {
                    VerifyFileIntegrity(client, localPath, remotePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] Erreur upload {localPath}: {ex.Message}");
                throw;
            }
        }, ct);
    }

    #endregion

    #region Download with Resume & Checksum

    public async Task DownloadAsync(SftpClient client, List<FileItem> remoteItems, string destinationLocalPath, IProgress<TransferProgressReport>? progress, CancellationToken ct = default)
    {
        if (client == null || !client.IsConnected) throw new ArgumentException("Client SFTP non connecté.");

        var counter = new ProgressCounter();

        foreach (var item in remoteItems)
        {
            if (ct.IsCancellationRequested) break;
            await DownloadRecursiveAsync(client, item, destinationLocalPath, progress, counter, ct);
        }
    }

    private async Task DownloadRecursiveAsync(SftpClient client, FileItem item, string localDestDir, IProgress<TransferProgressReport>? progress, ProgressCounter counter, CancellationToken ct)
    {
        string localTarget = Path.Combine(localDestDir, item.Name);

        if (item.IsDirectory)
        {
            Directory.CreateDirectory(localTarget);

            IEnumerable<Renci.SshNet.Sftp.ISftpFile>? content = null;
            try
            {
                content = await Task.Run(() => client.ListDirectory(item.FullPath));
            }
            catch (SftpPermissionDeniedException)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] Accès refusé dossier {item.FullPath}");
                return;
            }

            if (content != null)
            {
                foreach (var subFile in content)
                {
                    if (subFile.Name == "." || subFile.Name == "..") continue;

                    if (ct.IsCancellationRequested) return;

                    var subItem = new FileItem
                    {
                        Name = subFile.Name,
                        FullPath = subFile.FullName,
                        IsDirectory = subFile.IsDirectory,
                        IsLocal = false,
                        Size = subFile.Attributes.Size
                    };

                    await DownloadRecursiveAsync(client, subItem, localTarget, progress, counter, ct);
                }
            }
        }
        else
        {
            counter.Value++;
            progress?.Report(new TransferProgressReport
            {
                CurrentItemName = item.Name,
                ItemsProcessed = counter.Value,
                TotalItems = -1,
                Message = $"Réception de {item.Name}..."
            });

            await DownloadFileWithResumeAsync(client, item, localTarget, ct);
        }
    }

    private async Task DownloadFileWithResumeAsync(SftpClient client, FileItem remoteItem, string localPath, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                long remoteSize = remoteItem.Size;
                long localSize = 0;

                if (File.Exists(localPath))
                {
                    localSize = new FileInfo(localPath).Length;
                }

                if (localSize > 0 && localSize < remoteSize)
                {
                    
                    using var remoteStream = client.OpenRead(remoteItem.FullPath);
                    using var localStream = File.Open(localPath, FileMode.Append);
                    remoteStream.Seek(localSize, SeekOrigin.Begin);
                    remoteStream.CopyTo(localStream, 81920);
                }
                else
                {
                    
                    using var fs = File.Create(localPath);
                    client.DownloadFile(remoteItem.FullPath, fs);
                }

                if (remoteSize > 1024 * 1024)
                {
                    VerifyFileIntegrity(client, localPath, remoteItem.FullPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] Erreur download {remoteItem.FullPath}: {ex.Message}");
                throw;
            }
        }, ct);
    }

    #endregion

    #region Server-to-Server Transfer

    public async Task TransferBetweenServersAsync(SftpClient sourceClient, SftpClient destClient, List<FileItem> sourceItems, string destDirPath, IProgress<TransferProgressReport>? progress, CancellationToken ct = default)
    {
        if (sourceClient == null || !sourceClient.IsConnected) throw new ArgumentException("Source SFTP non connectée.");
        if (destClient == null || !destClient.IsConnected) throw new ArgumentException("Destination SFTP non connectée.");

        var counter = new ProgressCounter();

        foreach (var item in sourceItems)
        {
            if (ct.IsCancellationRequested) break;
            await TransferRecursiveAsync(sourceClient, destClient, item, destDirPath, progress, counter, ct);
        }
    }

    private static async Task TransferRecursiveAsync(SftpClient source, SftpClient dest, FileItem item, string destDir, IProgress<TransferProgressReport>? progress, ProgressCounter counter, CancellationToken ct)
    {
        string destPath = (destDir.TrimEnd('/') + "/" + item.Name);

        if (item.IsDirectory)
        {
            try
            {
                dest.CreateDirectory(destPath);
            }
            catch {  }

            IEnumerable<Renci.SshNet.Sftp.ISftpFile>? content = null;
            try
            {
                content = await Task.Run(() => source.ListDirectory(item.FullPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] Erreur listing source {item.FullPath}: {ex.Message}");
                return;
            }

            if (content != null)
            {
                foreach (var subFile in content)
                {
                    if (subFile.Name == "." || subFile.Name == "..") continue;
                    if (ct.IsCancellationRequested) return;

                    var subItem = new FileItem
                    {
                        Name = subFile.Name,
                        FullPath = subFile.FullName,
                        IsDirectory = subFile.IsDirectory,
                        IsLocal = false,
                        Size = subFile.Attributes.Size
                    };

                    await TransferRecursiveAsync(source, dest, subItem, destPath, progress, counter, ct);
                }
            }
        }
        else
        {
            counter.Value++;
            progress?.Report(new TransferProgressReport
            {
                CurrentItemName = item.Name,
                ItemsProcessed = counter.Value,
                TotalItems = -1,
                Message = $"Transfert de {item.Name}..."
            });

            await Task.Run(async () =>
            {
                try
                {
                    using var sourceStream = source.OpenRead(item.FullPath);
                    using var destStream = dest.OpenWrite(destPath);
                    await sourceStream.CopyToAsync(destStream, 81920, ct);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TransferManager] Erreur transfert {item.Name}: {ex.Message}");
                }
            }, ct);
        }
    }

    #endregion

    #region Integrity Verification

    private void VerifyFileIntegrity(SftpClient client, string localPath, string remotePath)
    {
        try
        {
            
            string localMd5 = ComputeLocalMd5(localPath);

            string remoteMd5 = ComputeRemoteMd5(client, remotePath);

            if (!string.Equals(localMd5, remoteMd5, StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] ⚠️ Checksum mismatch: {localPath}");
                System.Diagnostics.Debug.WriteLine($"  Local:  {localMd5}");
                System.Diagnostics.Debug.WriteLine($"  Remote: {remoteMd5}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[TransferManager] ✓ Checksum OK: {Path.GetFileName(localPath)}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TransferManager] Erreur vérification intégrité: {ex.Message}");
        }
    }

    private static string ComputeLocalMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = md5.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }

    private static string ComputeRemoteMd5(SftpClient client, string remotePath)
    {
        try
        {
            
            using var sshClient = new SshClient(client.ConnectionInfo);
            sshClient.Connect();
            var cmd = sshClient.RunCommand($"md5sum '{remotePath}'");
            sshClient.Disconnect();

            if (cmd.ExitStatus == 0 && !string.IsNullOrEmpty(cmd.Result))
            {
                
                string[] parts = cmd.Result.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    return parts[0].ToLowerInvariant();
                }
            }
        }
        catch { }

        return "";
    }

    #endregion

    #region Helpers

    private static void ScanLocalDirectory(string dirPath, string relativeBase, List<(string Local, string RemoteRel)> list)
    {
        try
        {
            var dirInfo = new DirectoryInfo(dirPath);
            foreach (var file in dirInfo.GetFiles())
            {
                list.Add((file.FullName, Path.Combine(relativeBase, file.Name)));
            }
            foreach (var subDir in dirInfo.GetDirectories())
            {
                ScanLocalDirectory(subDir.FullName, Path.Combine(relativeBase, subDir.Name), list);
            }
        }
        catch (UnauthorizedAccessException) {  }
    }

    private static void EnsureRemoteDirectoryExists(SftpClient client, string remoteDir)
    {
        if (string.IsNullOrWhiteSpace(remoteDir) || remoteDir == "." || remoteDir == "/") return;

        string path = remoteDir.Replace("\\", "/");
        if (path.EndsWith("/")) path = path[..^1];

        try
        {
            client.ChangeDirectory(path);
            client.ChangeDirectory("/");
            return;
        }
        catch (SftpPathNotFoundException) {  }

        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = path.StartsWith("/") ? "/" : "";

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            current = (current == "" || current == "/") ? current + part : current + "/" + part;

            try
            {
                client.GetAttributes(current);
            }
            catch (SftpPathNotFoundException)
            {
                try { client.CreateDirectory(current); } catch {  }
            }
        }
    }

    public void Dispose()
    {
        _workerCts.Cancel();
        _parallelLimit.Dispose();
    }

    #endregion
}
