using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinBridge.App.Models;
using WinBridge.App.Services.Files;
using WinBridge.Core.Models;

namespace WinBridge.App.Views;

public sealed partial class SftpPage : Page
{
    private SftpClient? _sftpClient;
    private string _currentLocalPath = string.Empty;
    private string _currentRemotePath = string.Empty; 
    private CancellationTokenSource? _transferCts;

    public SftpPage()
    {
        this.InitializeComponent();
        Loaded += SftpPage_Loaded;
        Unloaded += SftpPage_Unloaded;
    }

    private async void SftpPage_Loaded(object sender, RoutedEventArgs e)
    {
        _currentLocalPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await NavigateLocalAsync(_currentLocalPath);

        try
        {
            var servers = await App.DataService.GetServersAsync();
            ServerCombo.ItemsSource = servers.OrderBy(s => s.Name).ToList();
            if (ServerCombo.Items.Count > 0) ServerCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur chargement serveurs: {ex.Message}");
        }
    }

    private void SftpPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Disconnect();
        _transferCts?.Cancel();
    }

    #region Connection

    private void OnServerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Disconnect();
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        if (ServerCombo.SelectedItem is not Server server) return;

        GlobalLoading.IsActive = true;
        StatusText.Text = $"Connexion Ã  {server.Name}...";
        ConnectButton.IsEnabled = false;

        try
        {
            _sftpClient = await App.SftpService.GetConnectedClientAsync(server);

            _currentRemotePath = _sftpClient.WorkingDirectory;
            await NavigateRemoteAsync(_currentRemotePath);

            StatusText.Text = $"ConnectÃ© Ã  {server.Name}";
            ConnectButton.Visibility = Visibility.Collapsed;
            DisconnectButton.Visibility = Visibility.Visible;
            ServerCombo.IsEnabled = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur connexion: {ex.Message}");
            ConnectButton.IsEnabled = true;
        }
        finally
        {
            GlobalLoading.IsActive = false;
        }
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        Disconnect();
    }

    private void Disconnect()
    {
        if (_sftpClient != null)
        {
            App.SftpService.Disconnect(_sftpClient);
            _sftpClient = null;
        }

        RemoteListView.ItemsSource = null;
        RemotePathBox.Text = "";

        ConnectButton.Visibility = Visibility.Visible;
        ConnectButton.IsEnabled = true;
        DisconnectButton.Visibility = Visibility.Collapsed;
        ServerCombo.IsEnabled = true;
        StatusText.Text = "DÃ©connectÃ©";
    }

    #endregion

    #region Navigation Local

    private async Task NavigateLocalAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var items = await Task.Run(() => App.FileSystemManager.GetLocalItems(path));
            LocalListView.ItemsSource = items;
            _currentLocalPath = path;
            LocalPathBox.Text = path;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur navigation locale: {ex.Message}");
        }
    }

    private void OnLocalUpClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var parent = Directory.GetParent(_currentLocalPath);
            if (parent != null) _ = NavigateLocalAsync(parent.FullName);
        }
        catch { }
    }

    private void OnLocalPathKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _ = NavigateLocalAsync(LocalPathBox.Text);
        }
    }

    private void OnLocalGoClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateLocalAsync(LocalPathBox.Text);
    }

    private void OnLocalDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (LocalListView.SelectedItem is FileItem item && item.IsDirectory)
        {
            _ = NavigateLocalAsync(item.FullPath);
        }
    }

    #endregion

    #region Navigation Remote

    private async Task NavigateRemoteAsync(string path)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected) return;

        path = path.Replace('\\', '/');

        GlobalLoading.IsActive = true;
        try
        {
            var items = await Task.Run(() => App.FileSystemManager.GetRemoteItems(_sftpClient, path));
            RemoteListView.ItemsSource = items;
            _currentRemotePath = path;
            RemotePathBox.Text = path;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur navigation distante: {ex.Message}");
        }
        finally
        {
            GlobalLoading.IsActive = false;
        }
    }

    private void OnRemoteUpClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentRemotePath) || _currentRemotePath == "/") return;

        int lastSlash = _currentRemotePath.LastIndexOf('/');
        string parent = (lastSlash <= 0) ? "/" : _currentRemotePath[..lastSlash];

        _ = NavigateRemoteAsync(parent);
    }

    private void OnRemotePathKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _ = NavigateRemoteAsync(RemotePathBox.Text);
        }
    }

    private void OnRemoteGoClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateRemoteAsync(RemotePathBox.Text);
    }

    private void OnRemoteDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (RemoteListView.SelectedItem is FileItem item && item.IsDirectory)
        {
            _ = NavigateRemoteAsync(item.FullPath);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateLocalAsync(_currentLocalPath);
        if (_sftpClient != null && _sftpClient.IsConnected)
        {
            _ = NavigateRemoteAsync(_currentRemotePath);
        }
    }

    #endregion

    #region Transfers

    private async void OnUploadClick(object sender, RoutedEventArgs e)
    {
        if (!IsConnected()) return;

        var selectedItems = LocalListView.SelectedItems.Cast<FileItem>().ToList();
        if (selectedItems.Count == 0) return;

        var sourcePaths = selectedItems.Select(i => i.FullPath).ToList();
        await RunUpload(sourcePaths);
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (!IsConnected()) return;

        var selectedItems = RemoteListView.SelectedItems.Cast<FileItem>().ToList();
        if (selectedItems.Count == 0) return;

        await RunDownload(selectedItems);
    }

    private async Task RunUpload(List<string> sourcePaths)
    {
        await PerformTransferAsync(async (progress, ct) =>
       {
           await App.TransferManager.UploadAsync(_sftpClient!, sourcePaths, _currentRemotePath, progress, ct);
       }, "Upload terminÃ©e", true);
    }

    private async Task RunDownload(List<FileItem> remoteItems)
    {
        await PerformTransferAsync(async (progress, ct) =>
        {
            await App.TransferManager.DownloadAsync(_sftpClient!, remoteItems, _currentLocalPath, progress, ct);
        }, "Download terminÃ©", false);
    }

    private async Task PerformTransferAsync(Func<IProgress<TransferProgressReport>, CancellationToken, Task> transferAction, string successMessage, bool refreshRemote)
    {
        _transferCts = new CancellationTokenSource();
        TransferRing.IsActive = true;

        var progress = new Progress<TransferProgressReport>(report =>
        {
            StatusText.Text = $"[{report.ItemsProcessed}] {report.Message}";
        });

        try
        {
            await transferAction(progress, _transferCts.Token);
            StatusText.Text = successMessage;

            if (refreshRemote) _ = NavigateRemoteAsync(_currentRemotePath);
            else _ = NavigateLocalAsync(_currentLocalPath);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Transfert annulÃ©.";
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur transfert: {ex.Message}");
        }
        finally
        {
            TransferRing.IsActive = false;
            _transferCts = null;
        }
    }

    private bool IsConnected()
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
        {
            ShowStatus("Non connectÃ©.");
            return false;
        }
        return true;
    }

    #endregion

    #region Drag & Drop (Productivity)

    private void OnLocalDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        
        var items = e.Items.Cast<FileItem>().Select(f => f.FullPath).ToList();
        e.Data.SetText(string.Join("|", items)); 
        e.Data.Properties.Add("Source", "Local");
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }

    private void OnLocalDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("Source") && e.DataView.Properties["Source"].ToString() == "Remote")
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "TÃ©lÃ©charger vers Local";
        }
        else
        {
            
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void OnLocalDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("Source") && e.DataView.Properties["Source"].ToString() == "Remote")
        {
            var text = await e.DataView.GetTextAsync();
            var paths = text.Split('|');

            var fileItems = paths.Select(p => new FileItem
            {
                FullPath = p,
                Name = Path.GetFileName(p),
                IsDirectory = false 
                                    
            }).ToList();

            if (RemoteListView.ItemsSource is List<FileItem> currentRemoteItems)
            {
                fileItems = [.. currentRemoteItems.Where(i => paths.Contains(i.FullPath))];
            }

            await RunDownload(fileItems);
        }
    }

    private void OnRemoteDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var items = e.Items.Cast<FileItem>().Select(f => f.FullPath).ToList();
        e.Data.SetText(string.Join("|", items));
        e.Data.Properties.Add("Source", "Remote");
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }

    private void OnRemoteDragOver(object sender, DragEventArgs e)
    {
        
        if (e.DataView.Properties.ContainsKey("Source") && e.DataView.Properties["Source"].ToString() == "Local")
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Uploader vers Distant";
        }
        
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Uploader fichiers externes";
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void OnRemoteDrop(object sender, DragEventArgs e)
    {
        
        if (e.DataView.Properties.ContainsKey("Source") && e.DataView.Properties["Source"].ToString() == "Local")
        {
            var text = await e.DataView.GetTextAsync();
            var paths = text.Split('|').ToList();
            await RunUpload(paths);
        }
        
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).ToList(); 
            await RunUpload(paths);
        }
    }

    #endregion

    #region Clipboard & Context Menu

    private void OnLocalCopyClick(object sender, RoutedEventArgs e) => CopyLocalToClipboard();

    private void OnRemotePasteClick(object sender, RoutedEventArgs e) => _ = PasteToRemoteAsync();

    private void OnRemoteCopyClick(object sender, RoutedEventArgs e) => CopyRemoteToClipboard();

    private void OnLocalListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.C)
        {
            CopyLocalToClipboard();
        }
    }

    private async void OnRemoteListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);

        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.V)
        {
            await PasteToRemoteAsync();
        }
        
        else if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.C)
        {
            CopyRemoteToClipboard();
        }
        
        else if (e.Key == Windows.System.VirtualKey.Delete)
        {
            OnDeleteRemoteClick(sender, e);
        }
    }

    private void CopyLocalToClipboard()
    {
        var selected = LocalListView.SelectedItems.Cast<FileItem>().ToList();
        if (selected.Count == 0) return;

        var dp = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dp.SetText(string.Join(Environment.NewLine, selected.Select(s => s.FullPath)));
        Clipboard.SetContent(dp);
    }

    private void CopyRemoteToClipboard()
    {
        var selected = RemoteListView.SelectedItems.Cast<FileItem>().Select(f => f.FullPath);
        if (!selected.Any()) return;

        var dp = new DataPackage();
        dp.SetText(string.Join(Environment.NewLine, selected));
        dp.RequestedOperation = DataPackageOperation.Copy;
        Clipboard.SetContent(dp);
    }

    private async Task PasteToRemoteAsync()
    {
        var dp = Clipboard.GetContent();
        if (dp.Contains(StandardDataFormats.StorageItems))
        {
            var items = await dp.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).ToList();
            await RunUpload(paths);
        }
    }

    private async void OnNewRemoteFolderClick(object sender, RoutedEventArgs e)
    {
        if (!IsConnected()) return;
        var name = await ShowInputDialogAsync("Nouveau dossier", "Nouveau dossier");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            string newPath = _currentRemotePath.TrimEnd('/') + "/" + name;
            _sftpClient!.CreateDirectory(newPath);
            _ = NavigateRemoteAsync(_currentRemotePath);
        }
        catch (Exception ex) { ShowStatus($"Erreur crÃ©ation: {ex.Message}"); }
    }

    private async void OnRenameRemoteClick(object sender, RoutedEventArgs e)
    {
        if (!IsConnected()) return;
        if (RemoteListView.SelectedItem is not FileItem item) return;

        var newName = await ShowInputDialogAsync("Renommer", item.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        try
        {
            string newPath = Path.GetDirectoryName(item.FullPath)?.Replace('\\', '/') + "/" + newName;
            
            _sftpClient!.RenameFile(item.FullPath, newPath);
            _ = NavigateRemoteAsync(_currentRemotePath);
        }
        catch (Exception ex) { ShowStatus($"Erreur renommage: {ex.Message}"); }
    }

    private async void OnDeleteRemoteClick(object sender, RoutedEventArgs e)
    {
        if (!IsConnected()) return;
        var items = RemoteListView.SelectedItems.Cast<FileItem>().ToList();
        if (items.Count == 0) return;

        var confirm = await ShowConfirmationDialogAsync($"Supprimer {items.Count} Ã©lÃ©ment(s) ?", "Cette action est irrÃ©versible.");
        if (!confirm) return;

        GlobalLoading.IsActive = true;
        try
        {
            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    if (item.IsDirectory)
                        DeleteDirectoryRecursive(item.FullPath);
                    else
                        _sftpClient!.DeleteFile(item.FullPath);
                }
            });
            _ = NavigateRemoteAsync(_currentRemotePath);
        }
        catch (Exception ex) { ShowStatus($"Erreur suppression: {ex.Message}"); }
        finally
        {
            GlobalLoading.IsActive = false;
        }
    }

    private void DeleteDirectoryRecursive(string path)
    {
        
        var files = _sftpClient!.ListDirectory(path);
        foreach (var file in files)
        {
            if (file.Name == "." || file.Name == "..") continue;

            if (file.IsDirectory)
            {
                DeleteDirectoryRecursive(file.FullName);
            }
            else
            {
                _sftpClient.DeleteFile(file.FullName);
            }
        }
        _sftpClient.DeleteDirectory(path);
    }

    #endregion

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
    }

    private async Task<string?> ShowInputDialogAsync(string title, string defaultValue = "")
    {
        var textBox = new TextBox { Text = defaultValue };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "OK",
            CloseButtonText = "Annuler",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Oui",
            CloseButtonText = "Non",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}

