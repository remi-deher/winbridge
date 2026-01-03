using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Renci.SshNet;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WinBridge.App.Models;
using WinBridge.App.Services;
using WinBridge.App.Services.Files;
using WinBridge.App.Services.Terminal;
using WinBridge.Core.Models;
using Windows.ApplicationModel.DataTransfer;

namespace WinBridge.App.Views;

public class CrumbItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public override string ToString() => Name;
}

public sealed partial class ServerDetailsPage : Page, IDisposable
{
    #region Fields

    private Server? _server;
    private TerminalSessionManager? _sessionManager;
    private readonly DataService _dataService;
    private readonly VaultService _vaultService;
    private readonly BridgeService _bridgeService;
    private bool _isConnected;
    private readonly Queue<string> _pendingPasswords = new();
    private DateTime _lastPasswordSentTime = DateTime.MinValue;
    private bool _webViewReady;
    private bool _isViewInitialized;
    private readonly StringBuilder _pendingTerminalOutput = new();

    private SftpClient? _rightSftpClient;
    private SftpClient? _leftSftpClient;
    private Server? _leftSourceServer;
    private string _currentLeftPath = string.Empty;
    private string _currentRightPath = string.Empty;
    private bool _isLeftLocal = true;
    private CancellationTokenSource? _transferCts;

    private List<FileItem> _allLeftItems = [];
    private readonly ObservableCollection<FileItem> _displayLeftItems = [];

    private List<FileItem> _allRightItems = [];
    private readonly ObservableCollection<FileItem> _displayRightItems = [];

    private string _leftSortColumn = "Name";
    private bool _leftSortAscending = true;
    private string _rightSortColumn = "Name";
    private bool _rightSortAscending = true;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Needed for XAML binding")]
    public ObservableCollection<TransferTask> TransferTasks => App.TransferManager.Tasks;
    public int TransferActiveCount => TransferTasks.Count(t => t.Status == TransferStatus.InProgress || t.Status == TransferStatus.Pending);

    #endregion

    #region Constructor

    public ServerDetailsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;

        _dataService = App.DataService;
        _vaultService = App.VaultService;
        _bridgeService = App.BridgeService;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private async void OnLoaded(object _, RoutedEventArgs _1)
    {
        if (!_isViewInitialized)
        {
            LeftFileGrid.ItemsSource = _displayLeftItems;
            RightFileGrid.ItemsSource = _displayRightItems;
            await InitializeWebViewAsync();
            await LoadModuleExtensions();
            _isViewInitialized = true;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await TerminalWebView.EnsureCoreWebView2Async();
            TerminalWebView.CoreWebView2.WebMessageReceived += OnWebViewMessageReceived;
            string htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal", "terminal.html");
            if (File.Exists(htmlPath))
                TerminalWebView.CoreWebView2.Navigate($"file:///{htmlPath.Replace('\\', '/')}");
        }
        catch (Exception ex) { Debug.WriteLine($"[ServerDetailsPage] Erreur init WebView2: {ex.Message}"); }
    }

    private void OnUnloaded(object _, RoutedEventArgs _1) { }

    private bool _isPageActive;

    public async Task SendTextToTerminal(string text, bool autoEnter)
    {
        if (_sessionManager == null || !_sessionManager.IsActive)
            return;

        string payload = text + (autoEnter ? "\n" : "");
        await _sessionManager.SendInputAsync(payload);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isPageActive = true;

        if (e.Parameter is Server server)
        {
            if (_server?.Id != server.Id)
            {
                _server = server;
                UpdateServerInfo();
                _ = ConnectToServerAsync();
                _ = InitializeRightSftpAsync();
                _ = InitializeLeftSourceSelectorAsync();
                
            }

            _ = LoadModuleExtensions();
        }
    }

    private async Task LoadModuleExtensions()
    {
        try
        {

            var pivot = this.FindName("MainPivot") as Pivot ?? this.FindName("Pivot") as Pivot;

            if (pivot == null) return;

            while (pivot.Items.Count > 5)
            {
                pivot.Items.RemoveAt(pivot.Items.Count - 1);
            }

            var modules = await _dataService.GetRegisteredModulesAsync();

            foreach (var module in modules.Where(m => m.IsActive))
            {
                
                if (!string.IsNullOrEmpty(module.SupportedOsJson) && _server != null)
                {
                    try
                    {
                        var supportedOs = System.Text.Json.JsonSerializer.Deserialize<List<string>>(module.SupportedOsJson);
                        if (supportedOs != null && supportedOs.Count > 0)
                        {
                            bool compatible = supportedOs.Any(os => string.Equals(os, _server.Os.ToString(), StringComparison.OrdinalIgnoreCase));

                            if (!compatible) continue; 
                        }
                    }
                    catch {  }
                }

                if (string.IsNullOrEmpty(module.ExtensionsJson)) continue;

                List<WinBridge.App.Models.StoredExtension>? extensions = null;
                try
                {
                    extensions = System.Text.Json.JsonSerializer.Deserialize<List<WinBridge.App.Models.StoredExtension>>(module.ExtensionsJson);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] Erreur dÃ©sÃ©rialisation Extensions {module.Id}: {ex.Message}");
                    continue;
                }

                if (extensions == null) continue;

                foreach (var ext in extensions)
                {
                    if (ext.Type != "ServerTab" && ext.Type != "SERVER_TAB") continue;

                    if (ext.OsFilter != null && ext.OsFilter.Count > 0 && _server != null)
                    {
                        bool tabCompatible = ext.OsFilter.Any(f => string.Equals(f, _server.Os.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (!tabCompatible) continue;
                    }

                    var pivotItem = new PivotItem();

                    var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    headerStack.Children.Add(new FontIcon { Glyph = ext.IconGlyph ?? "\uE74C", FontSize = 16 });
                    headerStack.Children.Add(new TextBlock { Text = ext.Title });
                    pivotItem.Header = headerStack;

                    var webView = new WebView2();
                    
                    if (!string.IsNullOrEmpty(ext.BaseUrl) && !string.IsNullOrEmpty(ext.EntryPoint))
                    {
                        var fullUrl = $"{ext.BaseUrl.TrimEnd('/')}/{ext.EntryPoint.TrimStart('/')}";
                        if (_server != null)
                        {
                            fullUrl += $"?token={ext.SessionToken}&serverId={_server.Id}";
                        }

                        pivotItem.Content = webView;
                        pivotItem.Loaded += async (s, e) =>
                        {
                            try { await webView.EnsureCoreWebView2Async(); webView.Source = new Uri(fullUrl); }
                            catch { }
                        };
                    }
                    else
                    {
                        pivotItem.Content = new TextBlock { Text = "URL invalide", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    }

                    pivot.Items.Add(pivotItem);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UI] Error loading module extensions: {ex.Message}");
        }
    }

    private void AddModuleTab(WinBridge.App.Models.ModuleInfo module, WinBridge.App.Models.StoredExtension ext)
    {
        
        var pivotItem = new PivotItem();

        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        string glyph = !string.IsNullOrEmpty(ext.IconGlyph) ? ext.IconGlyph : "\uE74C"; 

        headerStack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        headerStack.Children.Add(new TextBlock { Text = ext.Title });

        pivotItem.Header = headerStack;

        var webView = new WebView2();
        
        var container = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        container.Children.Add(webView);

        pivotItem.Content = container;

        pivotItem.Loaded += async (s, e) =>
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                string targetUrl = ext.EntryPoint;

                if (!Uri.IsWellFormedUriString(targetUrl, UriKind.Absolute) && !string.IsNullOrEmpty(ext.BaseUrl))
                {
                    string baseUri = ext.BaseUrl.TrimEnd('/');
                    string path = targetUrl.TrimStart('/');
                    targetUrl = $"{baseUri}/{path}";
                }

                if (!string.IsNullOrEmpty(ext.SessionToken))
                {
                    string separator = targetUrl.Contains('?') ? "&" : "?";
                    
                    if (!targetUrl.Contains("token="))
                    {
                        targetUrl = $"{targetUrl}{separator}token={ext.SessionToken}";
                    }
                }

                if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
                {
                    webView.Source = uri;
                    Debug.WriteLine($"[Module] Navigating {module.Name} tab to {uri}");
                }
                else
                {
                    Debug.WriteLine($"[Module] Invalid Target URI: {targetUrl}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Module] Error initializing WebView for {module.Name}: {ex.Message}");
            }
        };

        MainPivot.Items.Add(pivotItem);
    }

    #endregion

    #region SFTP Initialization

    private async Task InitializeRightSftpAsync()
    {
        if (_server == null) return;
        try
        {
            if (_rightSftpClient != null) { App.SftpService.Disconnect(_rightSftpClient); }
            _rightSftpClient = await App.SftpService.GetConnectedClientAsync(_server);
            _currentRightPath = _rightSftpClient.WorkingDirectory;
            await NavigateRightAsync(_currentRightPath);
        }
        catch (Exception ex)
        {
            ShowMessage("Erreur SFTP", $"Impossible de se connecter au SFTP de {_server.Name}: {ex.Message}");
        }
    }

    private async Task InitializeLeftSourceSelectorAsync()
    {
        try
        {
            var servers = await _dataService.GetServersAsync();
            var items = new List<SourceItem>
            {
                new() { Name = "ðŸ’» Mon PC (Local)", Server = null }
            };
            foreach (var s in servers.OrderBy(s => s.Name))
            {
                if (s.Id != _server?.Id)
                    items.Add(new SourceItem { Name = $"â˜ï¸ {s.Name}", Server = s });
            }
            LeftSourceSelector.ItemsSource = items;
            LeftSourceSelector.SelectedIndex = 0;
        }
        catch (Exception ex) { Debug.WriteLine($"[SFTP-Left] Erreur init selector: {ex.Message}"); }
    }

    public class SourceItem
    {
        public string Name { get; set; } = string.Empty;
        public Server? Server { get; set; }
        public override string ToString() => Name;
    }

    #endregion

    #region SFTP Logic (Left Pane)

    private async void OnLeftSourceChanged(object _, SelectionChangedEventArgs _1)
    {
        if (LeftSourceSelector.SelectedItem is SourceItem item)
        {
            if (_leftSftpClient != null)
            {
                App.SftpService.Disconnect(_leftSftpClient);
                _leftSftpClient = null;
            }

            _leftSourceServer = item.Server;
            _isLeftLocal = (_leftSourceServer == null);

            if (_isLeftLocal)
            {
                _currentLeftPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                await NavigateLeftAsync(_currentLeftPath);
            }
            else
            {
                try
                {
                    _leftSftpClient = await App.SftpService.GetConnectedClientAsync(_leftSourceServer!);
                    _currentLeftPath = _leftSftpClient.WorkingDirectory;
                    await NavigateLeftAsync(_currentLeftPath);
                }
                catch (Exception ex)
                {
                    ShowMessage("Erreur Connexion Gauche", ex.Message);
                    _allLeftItems.Clear();
                    ApplyLeftFilterAndSort();
                }
            }
        }
    }

    private async Task NavigateLeftAsync(string path, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            List<FileItem> items = [];
            if (_isLeftLocal)
            {
                
                items = await Task.Run(() => App.FileSystemManager.GetLocalItems(path));
            }
            else
            {
                if (_leftSftpClient == null || !_leftSftpClient.IsConnected) return;
                path = path.Replace('\\', '/');
                items = await Task.Run(() => App.FileSystemManager.GetRemoteItems(_leftSftpClient, path: path, sortColumn: "Name", ascending: true, forceRefresh: forceRefresh));
            }

            _currentLeftPath = path;
            _allLeftItems = items;
            ApplyLeftFilterAndSort();
            UpdateLeftBreadcrumb(path);
        }
        catch (Exception ex)
        {
            bool retry = await ShowRetryDialogAsync($"Erreur lors de la navigation vers {path} :\n{ex.Message}");
            if (retry) await NavigateLeftAsync(path, forceRefresh: true);
        }
    }

    private void ApplyLeftFilterAndSort()
    {
        var query = _allLeftItems.AsEnumerable();

        string filter = LeftSearchBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        query = query.OrderByDescending(i => i.IsDirectory);

        query = _leftSortColumn switch
        {
            "Size" => _leftSortAscending ? ((IOrderedEnumerable<FileItem>)query).ThenBy(i => i.Size) : ((IOrderedEnumerable<FileItem>)query).ThenByDescending(i => i.Size),
            "Name" or _ => _leftSortAscending ? ((IOrderedEnumerable<FileItem>)query).ThenBy(i => i.Name) : ((IOrderedEnumerable<FileItem>)query).ThenByDescending(i => i.Name)
        };

        _displayLeftItems.Clear();
        foreach (var item in query) _displayLeftItems.Add(item);
    }

    private void OnLeftFilterChanged(AutoSuggestBox _, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyLeftFilterAndSort();
        }
    }

    private void UpdateLeftBreadcrumb(string path)
    {
        var crumbs = new List<CrumbItem>();
        if (_isLeftLocal)
        {
            try
            {
                string root = Path.GetPathRoot(path) ?? "";
                if (!string.IsNullOrEmpty(root))
                {
                    
                    crumbs.Add(new CrumbItem { Name = root, FullPath = root });

                    string relative = path[root.Length..];
                    string[] parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                    string current = root;
                    foreach (var p in parts)
                    {
                        current = Path.Combine(current, p);
                        crumbs.Add(new CrumbItem { Name = p, FullPath = current });
                    }
                }
                else
                {
                    crumbs.Add(new CrumbItem { Name = path, FullPath = path });
                }
            }
            catch { crumbs.Add(new CrumbItem { Name = path, FullPath = path }); }
        }
        else
        {
            
            crumbs.Add(new CrumbItem { Name = "Racine", FullPath = "/" });
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string current = "/";
            foreach (var p in parts)
            {
                current = (current == "/" ? "" : current) + "/" + p;
                crumbs.Add(new CrumbItem { Name = p, FullPath = current });
            }
        }
        LeftBreadcrumb.ItemsSource = crumbs;
    }

    private void OnLeftBreadcrumbClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is CrumbItem item)
        {
            _ = NavigateLeftAsync(item.FullPath);
        }
    }

    private void OnLeftUpClick(object sender, RoutedEventArgs _1)
    {
        try
        {
            if (_isLeftLocal)
            {
                var parent = Directory.GetParent(_currentLeftPath);
                if (parent != null) _ = NavigateLeftAsync(parent.FullName);
            }
            else
            {
                if (string.IsNullOrEmpty(_currentLeftPath) || _currentLeftPath == "/") return;
                int lastSlash = _currentLeftPath.LastIndexOf('/');
                string parent = (lastSlash <= 0) ? "/" : _currentLeftPath[..lastSlash];
                _ = NavigateLeftAsync(parent);
            }
        }
        catch { }
    }

    private void OnLeftSortName(object _, TappedRoutedEventArgs _1) { _leftSortColumn = "Name"; _leftSortAscending = !_leftSortAscending; ApplyLeftFilterAndSort(); }
    private void OnLeftSortSize(object _, TappedRoutedEventArgs _1) { _leftSortColumn = "Size"; _leftSortAscending = !_leftSortAscending; ApplyLeftFilterAndSort(); }

    private async void OnLeftRenameClick(object _, RoutedEventArgs _1)
    {
        if (LeftFileGrid.SelectedItem is FileItem item)
        {
            string? newName = await ShowInputDialogAsync("Renommer", $"Renommer '{item.Name}' en :", item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            try
            {
                if (_isLeftLocal)
                {
                    string dir = Path.GetDirectoryName(item.FullPath) ?? _currentLeftPath;
                    string newPath = Path.Combine(dir, newName);
                    if (item.IsDirectory) Directory.Move(item.FullPath, newPath);
                    else File.Move(item.FullPath, newPath);
                }
                else if (_leftSftpClient != null)
                {
                    string parent = _currentLeftPath;
                    string newPath = (parent == "/" ? "" : parent) + "/" + newName;
                    await SftpService.RenameRemoteAsync(_leftSftpClient, item.FullPath, newPath);
                }
                await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
            }
            catch (Exception ex) { ShowMessage("Erreur", ex.Message); }
        }
    }

    private async void OnLeftDeleteClick(object _, RoutedEventArgs _1)
    {
        var items = LeftFileGrid.SelectedItems.Cast<FileItem>().ToList();
        if (items.Count == 0) return;

        if (!await ShowConfirmationDialogAsync("Supprimer", $"Supprimer {items.Count} Ã©lÃ©ment(s) ?")) return;

        try
        {
            if (_isLeftLocal)
            {
                foreach (var item in items)
                {
                    if (item.IsDirectory) Directory.Delete(item.FullPath, true);
                    else File.Delete(item.FullPath);
                }
            }
            else if (_leftSftpClient != null)
            {
                foreach (var item in items) await SftpService.DeleteRemoteAsync(_leftSftpClient, item.FullPath, item.IsDirectory);
            }
            await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
        }
        catch (Exception ex) { ShowMessage("Erreur", ex.Message); }
    }

    private async void OnLeftNewFolderClick(object _, RoutedEventArgs _1)
    {
        string? folderName = await ShowInputDialogAsync("Nouveau Dossier", "Nom :");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        try
        {
            if (_isLeftLocal)
            {
                Directory.CreateDirectory(Path.Combine(_currentLeftPath, folderName));
            }
            else if (_leftSftpClient != null)
            {
                string newPath = (_currentLeftPath == "/" ? "" : _currentLeftPath) + "/" + folderName;
                await SftpService.CreateRemoteDirectoryAsync(_leftSftpClient, newPath);
            }
            await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
        }
        catch (Exception ex) { ShowMessage("Erreur", ex.Message); }
    }

    private async void OnLeftPropertiesClick(object _, RoutedEventArgs _1)
    {
        if (LeftFileGrid.SelectedItem is FileItem item)
        {
            if (_isLeftLocal)
            {
                try
                {
                    var info = new FileInfo(item.FullPath);
                    string content = $"Nom: {item.Name}\nChemin: {item.FullPath}\nTaille: {item.Size} octets\nDate modif: {item.ModifiedDate:g}\nAttributs: {info.Attributes}";
                    ShowMessage("PropriÃ©tÃ©s (Local)", content);
                }
                catch { ShowMessage("Info", "Impossible de lire les propriÃ©tÃ©s."); }
            }
            else if (_leftSftpClient != null)
            {
                string? newChmod = await ShowInputDialogAsync("Permissions (Chmod)", $"DÃ©finir les permissions (ex: 755, 644) pour '{item.Name}':", "755");
                if (string.IsNullOrWhiteSpace(newChmod)) return;

                try
                {
                    await SftpService.SetPermissionsAsync(_leftSftpClient, item.FullPath, newChmod);
                    ShowMessage("SuccÃ¨s", "Permissions appliquÃ©es.");
                    await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
                }
                catch (Exception ex) { ShowMessage("Erreur", ex.Message); }
            }
        }
    }

    private void OnLeftCopyPathClick(object _, RoutedEventArgs _1)
    {
        if (LeftFileGrid.SelectedItem is FileItem item)
        {
            var dp = new DataPackage();
            dp.SetText(item.FullPath);
            Clipboard.SetContent(dp);
        }
    }

    private async void OnLeftDoubleTapped(object _, DoubleTappedRoutedEventArgs _1)
    {
        if (LeftFileGrid.SelectedItem is FileItem item)
        {
            if (item.IsDirectory)
            {
                await NavigateLeftAsync(item.FullPath);
            }
            else
            {
                string[] textExts = [".txt", ".log", ".json", ".xml", ".cs", ".js", ".ts", ".py", ".md", ".sh", ".config", ".yml", ".yaml", ".ini", ".conf", ".html", ".css", ".sql", ".env"];
                if (textExts.Contains(Path.GetExtension(item.Name).ToLower()))
                {
                    if (_isLeftLocal) await ShowLocalEditorDialogAsync(item.FullPath);
                    else if (_leftSftpClient != null) await ShowRemoteEditorDialogAsync(_leftSftpClient!, item.FullPath);
                }
            }
        }
    }

    private void OnLeftDragItemsStarting(object _, DragItemsStartingEventArgs e)
    {
        var items = e.Items.Cast<FileItem>().ToList();
        var paths = string.Join("|", items.Select(i => i.FullPath));
        e.Data.SetText(paths);
        e.Data.Properties.Add("SourceOrigin", _isLeftLocal ? "LocalLeft" : "RemoteLeft");
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }
    private void OnLeftDragOver(object _, DragEventArgs e) { e.AcceptedOperation = DataPackageOperation.Copy; e.DragUIOverride.Caption = "Copier vers " + (_isLeftLocal ? "Local" : "Serveur"); }
    private async void OnLeftDrop(object _, DragEventArgs e)
    {
        
        if (e.DataView.Properties.TryGetValue("SourceOrigin", out object? originObj))
        {
            string origin = originObj?.ToString() ?? "";
            if (origin == "RemoteRight")
            {
                var selectedItems = RightFileGrid.SelectedItems.Cast<FileItem>().ToList();
                if (selectedItems.Count == 0) return;

                if (_isLeftLocal)
                    await PerformTransferAsync(async (p, ct) => await App.TransferManager.DownloadAsync(_rightSftpClient!, selectedItems, _currentLeftPath, p, ct), "Download OK", false);
                else if (_leftSftpClient != null)
                    await PerformTransferAsync(async (p, ct) => await App.TransferManager.TransferBetweenServersAsync(_rightSftpClient!, _leftSftpClient, selectedItems, _currentLeftPath, p, ct), "S2S Transfer OK", false);
            }
        }
    }

    #endregion

    #region SFTP Logic (Right Pane - Always Remote)

    private async Task NavigateRightAsync(string path, bool forceRefresh = false)
    {
        if (_rightSftpClient == null || !_rightSftpClient.IsConnected) return;
        path = path.Replace('\\', '/');

        try
        {
            var items = await Task.Run(() => App.FileSystemManager.GetRemoteItems(_rightSftpClient, path: path, sortColumn: "Name", ascending: true, forceRefresh: forceRefresh));
            _currentRightPath = path;
            _allRightItems = items;
            ApplyRightFilterAndSort();
            UpdateRightBreadcrumb(path);
            StartSysMetricsTimer();
        }
        catch (Exception ex)
        {
            bool retry = await ShowRetryDialogAsync($"Erreur lors de la navigation vers {path} :\n{ex.Message}");
            if (retry) await NavigateRightAsync(path, forceRefresh: true);
        }
    }

    private void ApplyRightFilterAndSort()
    {
        var query = _allRightItems.AsEnumerable();
        string filter = RightSearchBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        query = query.OrderByDescending(i => i.IsDirectory);

        query = _rightSortColumn switch
        {
            "Size" => _rightSortAscending ? ((IOrderedEnumerable<FileItem>)query).ThenBy(i => i.Size) : ((IOrderedEnumerable<FileItem>)query).ThenByDescending(i => i.Size),
            "Name" or _ => _rightSortAscending ? ((IOrderedEnumerable<FileItem>)query).ThenBy(i => i.Name) : ((IOrderedEnumerable<FileItem>)query).ThenByDescending(i => i.Name)
        };

        _displayRightItems.Clear();
        foreach (var item in query) _displayRightItems.Add(item);
    }

    private void OnRightFilterChanged(AutoSuggestBox _, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) ApplyRightFilterAndSort();
    }

    private void UpdateRightBreadcrumb(string path)
    {
        
        var crumbs = new List<CrumbItem>
        {
            new() { Name = "Racine", FullPath = "/" }
        };
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "/";
        foreach (var p in parts)
        {
            current = (current == "/" ? "" : current) + "/" + p;
            crumbs.Add(new CrumbItem { Name = p, FullPath = current });
        }
        RightBreadcrumb.ItemsSource = crumbs;
    }

    private void OnRightBreadcrumbClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is CrumbItem item) _ = NavigateRightAsync(item.FullPath);
    }

    private void OnRightUpClick(object sender, RoutedEventArgs _1)
    {
        if (string.IsNullOrEmpty(_currentRightPath) || _currentRightPath == "/") return;
        int lastSlash = _currentRightPath.LastIndexOf('/');
        string parent = (lastSlash <= 0) ? "/" : _currentRightPath[..lastSlash];
        _ = NavigateRightAsync(parent);
    }

    private void OnRightSortName(object _, TappedRoutedEventArgs _1) { _rightSortColumn = "Name"; _rightSortAscending = !_rightSortAscending; ApplyRightFilterAndSort(); }
    private void OnRightSortSize(object _, TappedRoutedEventArgs _1) { _rightSortColumn = "Size"; _rightSortAscending = !_rightSortAscending; ApplyRightFilterAndSort(); }

    private async void OnRightRenameClick(object _, RoutedEventArgs _1)
    {
        if (RightFileGrid.SelectedItem is FileItem item)
        {
            string? newName = await ShowInputDialogAsync("Renommer", $"Renommer '{item.Name}' en :", item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            try
            {

                string parent = _currentRightPath;
                string newPath = (parent == "/" ? "" : parent) + "/" + newName;

                await SftpService.RenameRemoteAsync(_rightSftpClient!, item.FullPath, newPath);
                await NavigateRightAsync(_currentRightPath, forceRefresh: true);
            }
            catch (Exception ex) { ShowMessage("Erreur Renommer", ex.Message); }
        }
    }

    private async void OnRightDeleteClick(object _, RoutedEventArgs _1)
    {
        var items = RightFileGrid.SelectedItems.Cast<FileItem>().ToList();
        if (items.Count == 0) return;

        bool confirm = await ShowConfirmationDialogAsync("Supprimer", $"Voulez-vous vraiment supprimer {items.Count} Ã©lÃ©ment(s) ?\nAttention : Cette action est irrÃ©versible.");
        if (!confirm) return;

        try
        {
            foreach (var item in items)
            {
                await SftpService.DeleteRemoteAsync(_rightSftpClient!, item.FullPath, item.IsDirectory);
            }
            await NavigateRightAsync(_currentRightPath, forceRefresh: true);
        }
        catch (Exception ex) { ShowMessage("Erreur Suppression", ex.Message); }
    }

    private async void OnRightNewFolderClick(object _, RoutedEventArgs _1)
    {
        string? folderName = await ShowInputDialogAsync("Nouveau Dossier", "Nom du dossier :");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        try
        {
            string newPath = (_currentRightPath == "/" ? "" : _currentRightPath) + "/" + folderName;
            await SftpService.CreateRemoteDirectoryAsync(_rightSftpClient!, newPath);
            await NavigateRightAsync(_currentRightPath, forceRefresh: true);
        }
        catch (Exception ex) { ShowMessage("Erreur Nouveau Dossier", ex.Message); }
    }
    private async void OnRightPropertiesClick(object _, RoutedEventArgs _1)
    {
        if (RightFileGrid.SelectedItem is FileItem item && _rightSftpClient != null)
        {
            string? newChmod = await ShowInputDialogAsync("Permissions (Chmod)", $"DÃ©finir les permissions (ex: 755, 644) pour '{item.Name}':", "755");
            if (string.IsNullOrWhiteSpace(newChmod)) return;

            try
            {
                await SftpService.SetPermissionsAsync(_rightSftpClient, item.FullPath, newChmod);
                ShowMessage("SuccÃ¨s", "Permissions appliquÃ©es.");
                await NavigateRightAsync(_currentRightPath, forceRefresh: true);
            }
            catch (Exception ex) { ShowMessage("Erreur", ex.Message); }
        }
    }
    private void OnRightCopyPathClick(object _, RoutedEventArgs _1)
    {
        if (RightFileGrid.SelectedItem is FileItem item)
        {
            var dp = new DataPackage();
            dp.SetText(item.FullPath);
            Clipboard.SetContent(dp);
        }
    }

    private async void OnRightDoubleTapped(object _, DoubleTappedRoutedEventArgs _1)
    {
        if (RightFileGrid.SelectedItem is FileItem item)
        {
            if (item.IsDirectory) await NavigateRightAsync(item.FullPath);
            else await OpenRemoteFileAsync(item);
        }
    }

    private void OnRightDragItemsStarting(object _, DragItemsStartingEventArgs e)
    {
        var items = e.Items.Cast<FileItem>().ToList();
        var paths = string.Join("|", items.Select(i => i.FullPath));
        e.Data.SetText(paths);
        e.Data.Properties.Add("SourceOrigin", "RemoteRight");
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }
    private void OnRightDragOver(object _, DragEventArgs e) { e.AcceptedOperation = DataPackageOperation.Copy; e.DragUIOverride.Caption = "Envoyer vers Serveur"; }
    private async void OnRightDrop(object _, DragEventArgs e)
    {
        
        if (e.DataView.Properties.TryGetValue("SourceOrigin", out object? originObj))
        {
            string origin = originObj?.ToString() ?? "";

            if (origin == "LocalLeft")
            {
                var text = await e.DataView.GetTextAsync();
                var paths = text.Split('|').ToList();
                await PerformTransferAsync(async (p, ct) => await App.TransferManager.UploadAsync(_rightSftpClient!, paths, _currentRightPath, p, ct), "Envoi terminÃ©.", true);
            }
            else if (origin == "RemoteLeft" && _leftSftpClient != null)
            {
                var selectedItems = LeftFileGrid.SelectedItems.Cast<FileItem>().ToList();
                await PerformTransferAsync(async (p, ct) => await App.TransferManager.TransferBetweenServersAsync(_leftSftpClient, _rightSftpClient!, selectedItems, _currentRightPath, p, ct), "Transfert S2S terminÃ©.", true);
            }
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).ToList();
            await PerformTransferAsync(async (p, ct) => await App.TransferManager.UploadAsync(_rightSftpClient!, paths, _currentRightPath, p, ct), "Envoi terminÃ©.", true);
        }
    }

    private async Task PerformTransferAsync(Func<IProgress<TransferProgressReport>, CancellationToken, Task> action, string successMsg, bool refreshRight)
    {
        if (_transferCts != null) { ShowMessage("OccupÃ©", "Un transfert est dÃ©jÃ  en cours."); return; }
        _transferCts = new CancellationTokenSource();
        ConnectionProgressRing.IsActive = true;

        var progress = new Progress<TransferProgressReport>(report =>
        {
            DispatcherQueue.TryEnqueue(() => ConnectionStatusText.Text = $"[{report.ItemsProcessed}] {report.Message}");
        });

        try
        {
            await action(progress, _transferCts.Token);
            DispatcherQueue.TryEnqueue(() => ConnectionStatusText.Text = successMsg);
            if (refreshRight) await NavigateRightAsync(_currentRightPath);
            else await NavigateLeftAsync(_currentLeftPath);
        }
        catch (OperationCanceledException) { DispatcherQueue.TryEnqueue(() => ConnectionStatusText.Text = "AnnulÃ©."); }
        catch (Exception ex) { DispatcherQueue.TryEnqueue(() => ShowMessage("Erreur Transfert", ex.Message)); }
        finally
        {
            DispatcherQueue.TryEnqueue(() => { ConnectionProgressRing.IsActive = false; _transferCts = null; });
        }
    }

    #endregion

    #region Event Handlers (General)
    private async Task ConnectToServerAsync() => await ConnectToServerAsync(_server!);
    private void OnOpenTerminalSidebarClick(object _, RoutedEventArgs _1) => MainPivot.SelectedIndex = 1;
    private void OnManageFilesClick(object _, RoutedEventArgs _1) => MainPivot.SelectedIndex = 2;
    #endregion

    #region Terminal & SSH Methods (Legacy - Preserved)

    private async Task ConnectToServerAsync(Server server)
    {

        if (server == null || _isConnected) return;
        _isConnected = true; 

        _pendingPasswords.Clear();
        string? targetUsername = null;
        string? targetPassword = null;
        string? bastionConnection = null;
        string? bastionPassword = null;

        if (server.CredentialId.HasValue)
        {
            
            targetPassword = VaultService.RetrieveSecret($"Credential_{server.CredentialId.Value}");
            var credential = await _dataService.GetCredentialByIdAsync(server.CredentialId.Value);
            targetUsername = credential?.UserName;
        }
        
        if (server.UseBastion)
        {
            string bHost = server.BastionHost ?? "";
            int bPort = server.BastionPort;
            string? bUser = null;
            int? bCredId = server.BastionCredentialId;
            if (server.BastionServerId.HasValue)
            {
                var bServer = await _dataService.GetServerByIdAsync(server.BastionServerId.Value);
                if (bServer != null) { bHost = bServer.Host; bPort = bServer.Port; bCredId = bServer.CredentialId; }
            }
            if (!string.IsNullOrEmpty(bHost) && bCredId.HasValue)
            {
                var bCred = await _dataService.GetCredentialByIdAsync(bCredId.Value);
                if (bCred != null)
                {
                    bUser = bCred.UserName;
                    bastionPassword = VaultService.RetrieveSecret($"Credential_{bCredId.Value}");
                    bastionConnection = $"{bUser}@{bHost}:{bPort}";
                }
            }
        }

        if (string.IsNullOrEmpty(targetUsername)) { _isConnected = false; return; }

        if (!string.IsNullOrEmpty(bastionPassword)) _pendingPasswords.Enqueue(bastionPassword);
        if (!string.IsNullOrEmpty(targetPassword)) _pendingPasswords.Enqueue(targetPassword);

        string sshCommand = "ssh.exe";
        StringBuilder argsBuilder = new();
        argsBuilder.Append("-t -A -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null ");
        if (!string.IsNullOrEmpty(bastionConnection)) argsBuilder.Append($"-J {bastionConnection} ");
        argsBuilder.Append($"{targetUsername}@{server.Host} ");
        if (server.Port != 22) argsBuilder.Append($"-p {server.Port} ");
        if (!string.IsNullOrEmpty(server.SshArguments)) argsBuilder.Append($"{server.SshArguments} ");

        _sessionManager = new TerminalSessionManager(_bridgeService);
        _sessionManager.DataReceived += OnSessionDataReceived;
        _sessionManager.SessionClosed += OnSessionClosed;
        _sessionManager.StartLocalSession(sshCommand, argsBuilder.ToString().Trim(), 30, 120);
        if (_sessionManager.PtyResult != null) _ = ReadPtyOutputAsync(_sessionManager.PtyResult.Output);
    }

    private Task ReadPtyOutputAsync(FileStream outputStream)
    {
        return Task.Run(async () =>
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (true)
                {
                    if (!outputStream.CanRead) break;
                    int bytesRead = await outputStream.ReadAsync(buffer.AsMemory());
                    if (bytesRead == 0) break;
                    string output = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (_pendingPasswords.Count > 0 && (DateTime.Now - _lastPasswordSentTime).TotalSeconds > 1.5)
                    {
                        if (output.Contains("password:", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                string pwd = _pendingPasswords.Dequeue();
                                byte[] data = Encoding.UTF8.GetBytes(pwd + "\n");
                                _sessionManager?.PtyResult?.Input.Write(data, 0, data.Length);
                                _sessionManager?.PtyResult?.Input.Flush();
                                _lastPasswordSentTime = DateTime.Now;
                            }
                            catch { }
                        }
                    }

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (_webViewReady && TerminalWebView.CoreWebView2 != null)
                        {
                            string escapedOutput = JsonSerializer.Serialize(output);
                            await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"writeToTerminal({escapedOutput})");
                        }
                        else { _pendingTerminalOutput.Append(output); }
                    });
                }
            }
            catch { }
        });
    }

    private void OnWebViewMessageReceived(object? _, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            using JsonDocument doc = JsonDocument.Parse(json);
            string type = doc.RootElement.GetProperty("type").GetString() ?? "";

            if (type == "ready")
            {
                _webViewReady = true;
                if (_pendingTerminalOutput.Length > 0)
                {
                    string data = JsonSerializer.Serialize(_pendingTerminalOutput.ToString());
                    _pendingTerminalOutput.Clear();
                    DispatcherQueue.TryEnqueue(async () => await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"writeToTerminal({data})"));
                }
            }
            else if (type == "input")
            {
                string data = doc.RootElement.GetProperty("data").GetString() ?? "";
                if (!string.IsNullOrEmpty(data))
                {
                    byte[] b = Encoding.UTF8.GetBytes(data);
                    _sessionManager?.PtyResult?.Input.Write(b, 0, b.Length);
                    _sessionManager?.PtyResult?.Input.Flush();
                }
            }
            else if (type == "resize")
            {
                _sessionManager?.Resize(doc.RootElement.GetProperty("cols").GetInt32(), doc.RootElement.GetProperty("rows").GetInt32());
            }
        }
        catch { }
    }

    private void OnSessionDataReceived(object? _, TerminalDataEventArgs _1) { }
    private void OnSessionClosed(object? _, EventArgs _1) { DispatcherQueue.TryEnqueue(() => _isConnected = false); }
    private void DisconnectSession() { if (_sessionManager != null) { _sessionManager.CloseSession(); _sessionManager.Dispose(); _sessionManager = null; } _pendingPasswords.Clear(); _isConnected = false; }
    private async void ScheduleReconnection(int delayMs) { await Task.Delay(delayMs); if (_isPageActive && !_isConnected) await ConnectToServerAsync(); }

    private void UpdateServerInfo()
    {
        if (_server == null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            ServerNameBlock.Text = _server.Name;
            ServerIpBlock.Text = _server.Host;
        });
    }

    private async void ShowMessage(string title, string msg) => await new ContentDialog { Title = title, Content = msg, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();

    private async Task<bool> ShowRetryDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Erreur",
            Content = message,
            PrimaryButtonText = "RÃ©essayer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Oui",
            CloseButtonText = "Non",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<string?> ShowInputDialogAsync(string title, string content, string defaultText = "")
    {
        var inputTextBox = new TextBox { AcceptsReturn = false, Height = 32, Text = defaultText };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = content }, inputTextBox } },
            PrimaryButtonText = "OK",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? inputTextBox.Text : null;
    }

    private async Task ShowRemoteEditorDialogAsync(SftpClient client, string filePath)
    {
        try
        {
            string content = await SftpService.ReadRemoteTextAsync(client, filePath);

            var textBox = new TextBox
            {
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas"),
                Text = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 400,
                MaxHeight = 600,
                MinWidth = 600
            };

            var dialog = new ContentDialog
            {
                Title = $"Ã‰dition Distante - {Path.GetFileName(filePath)}",
                Content = textBox,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Fermer",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await SftpService.WriteRemoteTextAsync(client, filePath, textBox.Text);
                ShowMessage("SuccÃ¨s", "Fichier sauvegardÃ©.");
                
                if (!_isLeftLocal && _leftSftpClient != null) await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
                await NavigateRightAsync(_currentRightPath, forceRefresh: true);
            }
        }
        catch (Exception ex) { ShowMessage("Erreur", "Impossible d'ouvrir le fichier : " + ex.Message); }
    }

    private async Task ShowLocalEditorDialogAsync(string filePath)
    {
        try
        {
            string content = await File.ReadAllTextAsync(filePath);

            var textBox = new TextBox
            {
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas"),
                Text = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 400,
                MaxHeight = 600,
                MinWidth = 600
            };

            var dialog = new ContentDialog
            {
                Title = $"Ã‰dition Locale - {Path.GetFileName(filePath)}",
                Content = textBox,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Fermer",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await File.WriteAllTextAsync(filePath, textBox.Text);
                ShowMessage("SuccÃ¨s", "Fichier local sauvegardÃ©.");
                await NavigateLeftAsync(_currentLeftPath, forceRefresh: true);
            }
        }
        catch (Exception ex) { ShowMessage("Erreur", "Impossible d'ouvrir le fichier local : " + ex.Message); }
    }

    private void OnFileGridRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var listView = sender as ListView;
        var originalSource = e.OriginalSource as FrameworkElement;
        if (originalSource?.DataContext is FileItem item && listView != null)
        {
            if (!listView.SelectedItems.Contains(item))
            {
                listView.SelectedItem = item;
            }
        }
    }

    private async void OnUploadFileClick(object _, RoutedEventArgs _1)
    {
        if (_rightSftpClient == null) return;
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var paths = new List<string> { file.Path };
            await PerformTransferAsync(async (p, ct) => await App.TransferManager.UploadAsync(_rightSftpClient!, paths, _currentRightPath, p, ct), "Envoi terminÃ©.", true);
            await NavigateRightAsync(_currentRightPath, forceRefresh: true);
        }
    }

    private async void OnUploadFolderClick(object _, RoutedEventArgs _1)
    {
        if (_rightSftpClient == null) return;
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            var paths = new List<string> { folder.Path };
            await PerformTransferAsync(async (p, ct) => await App.TransferManager.UploadAsync(_rightSftpClient!, paths, _currentRightPath, p, ct), "Envoi terminÃ©.", true);
            await NavigateRightAsync(_currentRightPath, forceRefresh: true);
        }
    }

    private async void OnDownloadClick(object _, RoutedEventArgs _1)
    {
        if (_rightSftpClient == null) return;
        var items = RightFileGrid.SelectedItems.Cast<FileItem>().ToList();
        if (items.Count == 0) { ShowMessage("Info", "SÃ©lectionnez des Ã©lÃ©ments Ã  tÃ©lÃ©charger."); return; }

        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            await PerformTransferAsync(async (p, ct) => await App.TransferManager.DownloadAsync(_rightSftpClient!, items, folder.Path, p, ct), "TÃ©lÃ©chargement terminÃ©", false);
        }
    }

    private async void OnRightOpenClick(object _, RoutedEventArgs _1)
    {
        if (RightFileGrid.SelectedItem is FileItem item && !item.IsDirectory)
        {
            await OpenRemoteFileAsync(item);
        }
    }

    private async Task OpenRemoteFileAsync(FileItem item)
    {
        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), item.Name);

            ConnectionProgressRing.IsActive = true;
            await Task.Run(() =>
            {
                using var fileStream = File.Create(tempPath);
                _rightSftpClient!.DownloadFile(item.FullPath, fileStream);
            });
            ConnectionProgressRing.IsActive = false;

            var p = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true }
            };
            try { p.Start(); } catch { System.Diagnostics.Process.Start("notepad.exe", tempPath); }

            var dialog = new ContentDialog
            {
                Title = "Mode Ã‰dition Externe",
                Content = $"Le fichier '{item.Name}' a Ã©tÃ© ouvert.\n\nModifiez-le, sauvegardez (Ctrl+S), puis cliquez sur 'Mettre Ã  jour' ci-dessous pour envoyer les modifications au serveur.",
                PrimaryButtonText = "Mettre Ã  jour",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ConnectionProgressRing.IsActive = true;
                await Task.Run(() =>
                {
                    using var fileStream = File.OpenRead(tempPath);
                    _rightSftpClient!.UploadFile(fileStream, item.FullPath);
                });
                ConnectionProgressRing.IsActive = false;
                ShowMessage("SuccÃ¨s", "Fichier mis Ã  jour.");
                await NavigateRightAsync(_currentRightPath, forceRefresh: true);
            }

            try { File.Delete(tempPath); } catch { }
        }
        catch (Exception ex)
        {
            ConnectionProgressRing.IsActive = false;
            ShowMessage("Erreur", "Impossible d'ouvrir le fichier : " + ex.Message);
        }
    }

    #endregion

    #region System Metrics

    private DispatcherTimer? _sysMetricsTimer;

    private void StartSysMetricsTimer()
    {
        if (_sysMetricsTimer != null) return;
        _sysMetricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _sysMetricsTimer.Tick += OnMetricsTick;
        _sysMetricsTimer.Start();
        OnMetricsTick(this, null);
    }

    private void StopSysMetricsTimer()
    {
        if (_sysMetricsTimer != null)
        {
            _sysMetricsTimer.Stop();
            _sysMetricsTimer.Tick -= OnMetricsTick;
            _sysMetricsTimer = null;
        }
    }

    private bool _isMetricsLoading = false;

    private async void OnMetricsTick(object? _, object? _1)
    {
        if (_isMetricsLoading) return;
        if (_rightSftpClient == null || !_rightSftpClient.IsConnected) return;

        _isMetricsLoading = true;
        try
        {
            var status = await App.SftpService.GetServerStatusAsync(_rightSftpClient);
            DispatcherQueue.TryEnqueue(() => UpdateMetricsUI(status));
        }
        catch { }
        finally { _isMetricsLoading = false; }
    }

    private long _lastRxBytes = 0;
    private long _lastTxBytes = 0;
    private DateTime _lastMetricsTime = DateTime.MinValue;

    private void UpdateMetricsUI(WinBridge.App.Services.Files.SftpService.ServerStatus status)
    {
        
        if (CpuBar != null) { CpuBar.Value = status.Cpu; CpuText.Text = $"{status.Cpu:F0}%"; }
        if (RamBar != null) { RamBar.Value = status.RamPercent; RamText.Text = status.RamText; }
        if (DiskBar != null) { DiskBar.Value = status.DiskPercent; DiskText.Text = status.DiskText; }

        NetInterfaceHeader?.Text = !string.IsNullOrEmpty(status.NetworkInterface) ? status.NetworkInterface : "--";

        var now = DateTime.Now;
        if (_lastMetricsTime != DateTime.MinValue)
        {
            var seconds = (now - _lastMetricsTime).TotalSeconds;
            if (seconds > 0)
            {
                long rxDiff = status.RxBytes - _lastRxBytes;
                long txDiff = status.TxBytes - _lastTxBytes;

                if (rxDiff < 0) rxDiff = 0;
                if (txDiff < 0) txDiff = 0;

                NetDownText?.Text = FormatSpeed(rxDiff / seconds);
                NetUpText?.Text = FormatSpeed(txDiff / seconds);
            }
        }
        else
        {
            
            NetDownText?.Text = "0 B/s";
            NetUpText?.Text = "0 B/s";
        }

        _lastMetricsTime = now;
        _lastRxBytes = status.RxBytes;
        _lastTxBytes = status.TxBytes;

        if (ServerIpBlock != null && !string.IsNullOrEmpty(status.IPAddress)) ServerIpBlock.Text = status.IPAddress;

        OsValText?.Text = status.OSName;
        KernelValText?.Text = status.KernelVersion;
        UptimeValText?.Text = status.Uptime;
        CpuModelText?.Text = status.CpuModel;
        CpuCoresText?.Text = $"{status.CpuCores} CÅ“urs";
        RamValText?.Text = status.RamTotal;

        NetInterfaceText?.Text = status.NetworkInterface;
        NetIpOverviewText?.Text = status.IPAddress;

        DiskList?.ItemsSource = status.Disks;

        if (status.Processes != null)
        {
            SyncBackingStore(status.Processes);
            RefreshProcessList();
        }

        if (_server != null)
        {
            bool needsUpdate = false;

            if (string.IsNullOrEmpty(_server.OsVersion) ||
                _server.CpuCount == 0 ||
                string.IsNullOrEmpty(_server.TotalRam) ||
                string.IsNullOrEmpty(_server.CpuModel))
            {
                
                if (string.IsNullOrEmpty(_server.OsVersion) && !string.IsNullOrEmpty(status.OSName))
                {
                    _server.OsVersion = status.OSName;
                    needsUpdate = true;
                    Debug.WriteLine($"[FirstConnection] OsVersion dÃ©tectÃ©: {status.OSName}");
                }

                if (_server.CpuCount == 0 && int.TryParse(status.CpuCores, out int cores))
                {
                    _server.CpuCount = cores;
                    needsUpdate = true;
                    Debug.WriteLine($"[FirstConnection] CpuCount dÃ©tectÃ©: {cores}");
                }

                if (string.IsNullOrEmpty(_server.TotalRam) && !string.IsNullOrEmpty(status.RamTotal))
                {
                    _server.TotalRam = status.RamTotal;
                    needsUpdate = true;
                    Debug.WriteLine($"[FirstConnection] TotalRam dÃ©tectÃ©: {status.RamTotal}");
                }

                if (string.IsNullOrEmpty(_server.CpuModel) && !string.IsNullOrEmpty(status.CpuModel))
                {
                    _server.CpuModel = status.CpuModel;
                    needsUpdate = true;
                    Debug.WriteLine($"[FirstConnection] CpuModel dÃ©tectÃ©: {status.CpuModel}");
                }

                if (needsUpdate)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _dataService.UpdateServerAsync(_server);
                            Debug.WriteLine($"[FirstConnection] CaractÃ©ristiques matÃ©rielles sauvegardÃ©es pour serveur ID={_server.Id}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[FirstConnection] Erreur lors de la sauvegarde: {ex.Message}");
                        }
                    });
                }
            }
        }
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024 * 1024) return $"{bytesPerSec / (1024 * 1024 * 1024):F1} GB/s";
        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / (1024 * 1024):F1} MB/s";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024:F0} KB/s"; 
        return $"{bytesPerSec:F0} B/s";
    }

    #region Process Management
    private readonly List<WinBridge.App.Models.ProcessItem> _backingStore = [];
    private readonly ObservableCollection<WinBridge.App.Models.ProcessItem> _processView = [];
    private string _procSortCol = "Cpu";
    private bool _procSortAsc = false;
    private string _procFilterMode = "All"; 
    private string _procNameFilter = "";

    private void OnProcFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _procFilterMode = tag;
            RefreshProcessList();
        }
    }

    private void OnProcNameFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _procNameFilter = tb.Text;
            RefreshProcessList();
        }
    }

    private void OnSortHeaderTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string col)
        {
            if (_procSortCol == col) _procSortAsc = !_procSortAsc;
            else
            {
                _procSortCol = col;
                
                if (col == "Cpu" || col == "Mem") _procSortAsc = false; 
                else _procSortAsc = true; 
            }
            RefreshProcessList();
            UpdateSortIcons();
        }
    }

    private void UpdateSortIcons()
    {
        SortIconPid?.Visibility = Visibility.Collapsed;
        SortIconCommand?.Visibility = Visibility.Collapsed;
        SortIconUser?.Visibility = Visibility.Collapsed;
        SortIconCpu?.Visibility = Visibility.Collapsed;
        SortIconMem?.Visibility = Visibility.Collapsed;

        FontIcon? icon = _procSortCol switch
        {
            "Pid" => SortIconPid,
            "Command" => SortIconCommand,
            "User" => SortIconUser,
            "Cpu" => SortIconCpu,
            "Mem" => SortIconMem,
            _ => null
        };

        if (icon != null)
        {
            icon.Visibility = Visibility.Visible;
            icon.Glyph = _procSortAsc ? "\uE70E" : "\uE70D"; 
        }
    }

    private void RefreshProcessList()
    {
        
        if (ProcessList != null && ProcessList.ItemsSource == null)
        {
            ProcessList.ItemsSource = _processView;
        }

        if (_backingStore == null) return;
        var query = _backingStore.AsEnumerable();

        if (_procFilterMode == "Active")
        {
            query = query.Where(p => p.CpuValue > 0.0 || p.Status == "R");
        }
        else if (_procFilterMode == "Inactive")
        {
            query = query.Where(p => p.Status == "S" || p.Status == "T" || p.Status == "Z" || p.Status == "I");
        }

        if (!string.IsNullOrEmpty(_procNameFilter))
        {
            query = query.Where(p => p.Command.Contains(_procNameFilter, StringComparison.OrdinalIgnoreCase));
        }

        query = _procSortCol switch
        {
            "Pid" => _procSortAsc ? query.OrderBy(p => p.PidValue) : query.OrderByDescending(p => p.PidValue),
            "Command" => _procSortAsc ? query.OrderBy(p => p.Command) : query.OrderByDescending(p => p.Command),
            "User" => _procSortAsc ? query.OrderBy(p => p.User) : query.OrderByDescending(p => p.User),
            "Cpu" => _procSortAsc ? query.OrderBy(p => p.CpuValue) : query.OrderByDescending(p => p.CpuValue),
            "Mem" => _procSortAsc ? query.OrderBy(p => p.MemValue) : query.OrderByDescending(p => p.MemValue),
            _ => query
        };

        SyncObservableCollection(_processView, [.. query]);
    }

    private void SyncBackingStore(List<WinBridge.App.Services.Files.SftpService.ProcessInfo> newInfos)
    {
        var newDict = newInfos.ToDictionary(k => k.Pid, v => v);
        var toRemove = new List<WinBridge.App.Models.ProcessItem>();

        foreach (var item in _backingStore)
        {
            if (newDict.TryGetValue(item.Pid, out var info))
            {
                item.UpdateFrom(info);
                newDict.Remove(item.Pid); 
            }
            else
            {
                toRemove.Add(item);
            }
        }

        foreach (var dead in toRemove)
        {
            _backingStore.Remove(dead);
        }

        foreach (var kvp in newDict)
        {
            _backingStore.Add(WinBridge.App.Models.ProcessItem.FromInfo(kvp.Value));
        }
    }

    private static void SyncObservableCollection(ObservableCollection<WinBridge.App.Models.ProcessItem> target, List<WinBridge.App.Models.ProcessItem> source)
    {
        
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!source.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            int index = target.IndexOf(item);

            if (index == -1)
            {
                target.Insert(i, item);
            }
            else if (index != i)
            {
                target.Move(index, i);
            }
        }
    }

    private static double ParseDouble(string val)
    {
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double res)) return res;
        return 0.0;
    }

    private async void OnStopProcessClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.CommandParameter is string pid)
        {
            if (_rightSftpClient != null) await SftpService.ExecuteProcessActionAsync(_rightSftpClient, pid, "STOP");
        }
    }

    private async void OnRestartProcessClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.CommandParameter is string pid)
        {
            if (_rightSftpClient != null) await SftpService.ExecuteProcessActionAsync(_rightSftpClient, pid, "RESTART");
        }
    }
    #endregion

    #endregion

    #region Logs Management
    private readonly ObservableCollection<WinBridge.App.Models.FileItem> _logFiles = [];

    private async void OnRefreshLogsClick(object _, RoutedEventArgs _1)
    {
        await LoadLogFiles();
    }

    private async Task LoadLogFiles()
    {
        if (_rightSftpClient == null || !_rightSftpClient.IsConnected) return;

        LogLoadingRing?.IsActive = true;
        _logFiles.Clear();

        try
        {
            string logPath = "/var/log";
            if (_server != null && _server.Os == WinBridge.Core.Models.OsType.Windows)
            {
                logPath = "C:/ProgramData/WinBridge/Logs";
            }

            LogPathBox?.Text = logPath;

            var files = await Task.Run(() => _rightSftpClient.ListDirectory(logPath));
            var logList = files.Where(f => !f.IsDirectory && !f.Name.StartsWith('.'))
                               .OrderByDescending(f => f.LastWriteTime)
                               .Select(f => new WinBridge.App.Models.FileItem
                               {
                                   Name = f.Name,
                                   FullPath = f.FullName,
                                   IsDirectory = false,
                                   Size = f.Length,
                                   ModifiedDate = f.LastWriteTime
                               })
                               .ToList();

            foreach (var item in logList) _logFiles.Add(item);

            if (LogFileList != null && LogFileList.ItemsSource == null) LogFileList.ItemsSource = _logFiles;
        }
        catch (Exception ex)
        {
            LogContentText?.Text = $"Erreur lors du listage des logs: {ex.Message}";
        }
        finally
        {
            LogLoadingRing?.IsActive = false;
        }
    }

    private string? _cachedSudoPassword;

    private async void OnLogFileSelectionChanged(object _, SelectionChangedEventArgs _1)
    {
        if (LogFileList != null && LogFileList.SelectedItem is WinBridge.App.Models.FileItem item)
        {
            LogLoadingRing?.IsActive = true;
            LogContentText?.Text = "Lecture en cours...";
            try
            {
                
                if (_rightSftpClient == null || !_rightSftpClient.IsConnected)
                {
                    LogContentText?.Text = "SFTP non connectÃ©. Impossible de lire les logs.";
                    return;
                }

                var content = await SftpService.ReadLogTailAsync(_rightSftpClient, item.FullPath, 500, _cachedSudoPassword);

                bool permissionDenied = content.Contains("Permission denied") ||
                                       content.Contains("Permission non accordÃ©e") ||
                                       content.Contains("impossible d'ouvrir") ||
                                       content.Contains("[STDERR]");

                if (permissionDenied)
                {

                    if (string.IsNullOrEmpty(_cachedSudoPassword) && _server?.CredentialId != null)
                    {
                        string sudoKey = $"Credential_{_server.CredentialId}_Sudo";
                        _cachedSudoPassword = VaultService.RetrieveSecret(sudoKey);

                        if (string.IsNullOrEmpty(_cachedSudoPassword))
                        {

                            string mainKey = $"Credential_{_server.CredentialId}";

                        }
                    }

                    if (!string.IsNullOrEmpty(_cachedSudoPassword))
                    {
                        content = await SftpService.ReadLogTailAsync(_rightSftpClient, item.FullPath, 500, _cachedSudoPassword);
                        permissionDenied = content.Contains("Permission denied") ||
                                          content.Contains("Permission non accordÃ©e") ||
                                          content.Contains("impossible d'ouvrir") ||
                                          content.Contains("[STDERR]"); 
                    }

                    if (permissionDenied || content.Contains("incorrect password")) 
                    {
                        
                        var passwordBox = new PasswordBox();
                        var dialog = new ContentDialog
                        {
                            Title = "Permission RefusÃ©e",
                            Content = new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                 {
                                     new TextBlock { Text = "Ce fichier nÃ©cessite des droits d'administration (sudo). Veuillez entrer le mot de passe :" },
                                     passwordBox
                                 }
                            },
                            PrimaryButtonText = "Valider",
                            CloseButtonText = "Annuler",
                            DefaultButton = ContentDialogButton.Primary,
                            XamlRoot = this.XamlRoot
                        };

                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            _cachedSudoPassword = passwordBox.Password;
                            
                            content = await SftpService.ReadLogTailAsync(_rightSftpClient, item.FullPath, 500, _cachedSudoPassword);
                        }
                    }
                }

                LogContentText?.Text = string.IsNullOrEmpty(content) ? "(Fichier vide ou inaccessible)" : content;
            }
            catch (Exception ex)
            {
                LogContentText?.Text = $"Erreur lecture: {ex.Message}";
            }
            finally
            {
                LogLoadingRing?.IsActive = false;
            }
        }
    }

    private async void OnDownloadsLogClick(object _, RoutedEventArgs _1)
    {
        if (LogFileList != null && LogFileList.SelectedItem is WinBridge.App.Models.FileItem item)
        {

            try
            {
                if (_rightSftpClient == null || !_rightSftpClient.IsConnected) return;

                var localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", item.Name);
                var task = new WinBridge.App.Models.TransferTask
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = item.Name,
                    SourcePath = item.FullPath,
                    DestinationPath = localPath,
                    TotalBytes = item.Size, 
                    Direction = WinBridge.App.Models.TransferDirection.Download, 
                    Status = WinBridge.App.Models.TransferStatus.Pending
                };

                App.TransferManager.AddTask(task);
                await TransferManager.StartTransferAsync(task, _rightSftpClient); 

                ShowMessage("TÃ©lÃ©chargement lancÃ©", $"Le fichier {item.Name} est en cours de tÃ©lÃ©chargement vers {localPath}");
            }
            catch (Exception ex)
            {
                ShowMessage("Erreur", ex.Message);
            }
        }
    }
    #endregion

    public void Dispose()
    {
        StopSysMetricsTimer();
        DisconnectSession();
        if (_rightSftpClient != null) { App.SftpService.Disconnect(_rightSftpClient); _rightSftpClient = null; }
        if (_leftSftpClient != null) { App.SftpService.Disconnect(_leftSftpClient); _leftSftpClient = null; }
    }
}

