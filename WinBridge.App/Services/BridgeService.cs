using System.Diagnostics;
using System.Linq; 
using Grpc.Core;
using WinBridge.Core.Grpc;
using WinBridge.App.Services;
using WinBridge.Core.Models;
using Renci.SshNet;

namespace WinBridge.App.Services;

/// <summary>
/// Implements the gRPC host service for WinBridge.
/// Handles communication between the host application and plugged-in modules.
/// </summary>
/// <param name="dataService">The service for accessing application data.</param>
public class BridgeService(DataService dataService) : WinBridgeHost.WinBridgeHostBase
{
    private readonly DataService _dataService = dataService;

    /// <summary>
    /// Retrieves a comprehensive list of configured servers.
    /// </summary>
    /// <param name="request">Empty request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A list of ServerModel objects.</returns>
    public override async Task<WinBridge.Core.Grpc.ServerList> GetServers(WinBridge.Core.Grpc.Empty request, ServerCallContext context)
    {
        Debug.WriteLine($"[gRPC] GetServers from {context.Peer}");

        var servers = await _dataService.GetServersAsync();

        var response = new WinBridge.Core.Grpc.ServerList();
        response.Servers.AddRange(servers.Select(s => new WinBridge.Core.Grpc.ServerModel
        {
            Id = s.Id,
            Name = s.Name,
            Host = s.Host,
            Protocol = s.Protocol.ToString(),
            Port = s.Port
        }));

        return response;
    }

    /// <summary>
    /// Registers a module with the host, storing its metadata and extensions.
    /// </summary>
    /// <param name="request">The registration details.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the authorization status and session token.</returns>
    public override async Task<WinBridge.Core.Grpc.RegistrationResponse> RegisterModule(WinBridge.Core.Grpc.ModuleRegistration request, ServerCallContext context)
    {
        System.Diagnostics.Debug.WriteLine($"[BridgeService] RECU Module: {request.ModuleId}");

        var storedExtensions = request.UiExtensions.Select(e => new WinBridge.App.Models.StoredExtension
        {
            Id = e.Id,
            Title = e.Title,
            IconGlyph = e.IconGlyph,
            EntryPoint = e.EntryPoint,
            BaseUrl = e.BaseUrl,
            SessionToken = e.SessionToken,
            Type = e.Type.ToString(),
            OsFilter = [.. e.OsFilter.Select(o => o.ToString())]
        }).ToList();

        var uiExtensionsJson = System.Text.Json.JsonSerializer.Serialize(storedExtensions);
        var supportedOsJson = System.Text.Json.JsonSerializer.Serialize(request.SupportedOs.Select(o => o.ToString()).ToList());

        var moduleInfo = _dataService.GetModule(request.ModuleId);
        moduleInfo ??= new WinBridge.App.Models.ModuleInfo { Id = request.ModuleId };

        moduleInfo.Name = request.ModuleId; 
        moduleInfo.Version = request.ApiVersion;
        moduleInfo.IsActive = true;
        moduleInfo.LastSeen = DateTime.Now;

        moduleInfo.ExtensionsJson = uiExtensionsJson;
        moduleInfo.SupportedOsJson = supportedOsJson;

        _dataService.SaveModule(moduleInfo);
        System.Diagnostics.Debug.WriteLine($"[BridgeService] Module {request.ModuleId} registered.");

        return new WinBridge.Core.Grpc.RegistrationResponse
        {
            IsAuthorized = true,
            SessionToken = Guid.NewGuid().ToString() 
        };
    }

    public override Task<WinBridge.Core.Grpc.Empty> StreamServerMetrics(IAsyncStreamReader<WinBridge.Core.Grpc.MetricUpdate> requestStream, ServerCallContext context)
    {
        return Task.FromResult(new WinBridge.Core.Grpc.Empty());
    }

    /// <summary>
    /// Displays a toast notification in the OS notification center.
    /// </summary>
    /// <param name="request">The notification content.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response indicating success.</returns>
    public override async Task<ShowToastResponse> ShowToast(ShowToastRequest request, ServerCallContext context)
    {

        var tcs = new TaskCompletionSource<bool>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                
                var builder = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
                    .AddText(request.Title)
                    .AddText(request.Message);

                var notification = builder.BuildNotification();
                Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BridgeService] Error showing toast: {ex.Message}");
                tcs.SetResult(false);
            }
        });

        bool success = await tcs.Task;
        return new ShowToastResponse { Success = success };
    }

    /// <summary>
    /// Shows an interactive dialog box to the user.
    /// </summary>
    /// <param name="request">The dialog parameters.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The result of the user interaction (e.g., button clicked).</returns>
    public override async Task<ShowDialogResponse> ShowDialog(ShowDialogRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<string>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = request.Title,
                    Content = request.Message,
                    XamlRoot = WinBridge.App.App.MainWindow.Content.XamlRoot
                };

                switch (request.Buttons)
                {
                    case "OkCancel":
                        dialog.PrimaryButtonText = "OK";
                        dialog.CloseButtonText = "Cancel";
                        break;
                    case "YesNo":
                        dialog.PrimaryButtonText = "Yes";
                        dialog.CloseButtonText = "No";
                        break;
                    case "Ok":
                    default:
                        dialog.CloseButtonText = "OK";
                        break;
                }

                var result = await dialog.ShowAsync();

                string responseStr = "Cancel"; 
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    responseStr = request.Buttons == "YesNo" ? "Yes" : "Ok";
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
                {
                    responseStr = request.Buttons == "YesNo" ? "No" : "Cancel";
                    if (request.Buttons == "Ok") responseStr = "Ok"; 
                }

                tcs.SetResult(responseStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BridgeService] Error showing dialog: {ex.Message}");
                tcs.SetResult("Error");
            }
        });

        var finalResult = await tcs.Task;
        return new ShowDialogResponse { Result = finalResult };
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _memoryStorage = new();

    /// <summary>
    /// Stores a key-value pair in the ephemeral memory storage.
    /// </summary>
    /// <param name="request">The storage request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    public override Task<StorageSetResponse> StorageSet(StorageSetRequest request, ServerCallContext context)
    {
        string compositeKey = $"{request.ModuleId}:{request.Key}";
        _memoryStorage[compositeKey] = request.Value;
        return Task.FromResult(new StorageSetResponse { Success = true });
    }

    /// <summary>
    /// Retrieves a value from the ephemeral memory storage.
    /// </summary>
    /// <param name="request">The get request containing the key.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The stored value or empty.</returns>
    /// <summary>
    /// Retrieves a value from the ephemeral memory storage.
    /// </summary>
    /// <param name="request">The get request containing the key.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The stored value or empty.</returns>
    public override Task<StorageGetResponse> StorageGet(StorageGetRequest request, ServerCallContext context)
    {
        string compositeKey = $"{request.ModuleId}:{request.Key}";
        string val = _memoryStorage.TryGetValue(compositeKey, out var v) ? v : string.Empty;
        return Task.FromResult(new StorageGetResponse { Value = val });
    }

    /// <summary>
    /// Executes a command on a remote server via SSH or other protocols.
    /// </summary>
    /// <param name="request">The command details.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The execution result including stdout/stderr.</returns>
    /// <summary>
    /// Executes a command on a remote server via SSH or other protocols.
    /// </summary>
    /// <param name="request">The command details.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The execution result including stdout/stderr.</returns>
    public override async Task<ExecuteCommandResponse> ExecuteCommand(ExecuteCommandRequest request, ServerCallContext context)
    {
        try 
        {
            var server = await _dataService.GetServerByIdAsync(request.ServerId);
            if (server == null) return new ExecuteCommandResponse { Success = false, Stderr = "Server not found" };

            if (server.Protocol == WinBridge.Core.Models.ServerProtocol.SSH)
            {
                if (server.CredentialId == null) return new ExecuteCommandResponse { Success = false, Stderr = "No credential assigned to server" };
                
                int credId = (int)server.CredentialId.Value;
                var cred = await _dataService.GetCredentialByIdAsync(credId);
                if (cred == null) return new ExecuteCommandResponse { Success = false, Stderr = "Credential not found" };

                string credKey = $"Credential_{cred.Id}";
                var password = VaultService.RetrieveSecret(credKey);
                if (password == null) return new ExecuteCommandResponse { Success = false, Stderr = "Secret not found in vault" };

                string host = (string)server.Host;
                int port = (int)server.Port;
                string username = (string)cred.UserName;
                string sanePassword = (string)(password ?? string.Empty);

                using var client = new Renci.SshNet.SshClient(host, port, username, sanePassword);

                client.Connect();

                string cmdText = (string)request.Command;
                var cmd = client.CreateCommand(cmdText);
                
                int timeoutVal = request.TimeoutSeconds > 0 ? (int)request.TimeoutSeconds : 30;
                cmd.CommandTimeout = TimeSpan.FromSeconds((double)timeoutVal);
                
                string result = (string)cmd.Execute();
                int exitCode = cmd.ExitStatus ?? -1;
                string error = (string)cmd.Error;
                
                client.Disconnect();

                return new ExecuteCommandResponse 
                { 
                    Success = exitCode == 0,
                    Stdout = result,
                    Stderr = error,
                    ExitCode = exitCode
                };
            }
            else
            {
                return new ExecuteCommandResponse { Success = false, Stderr = $"Protocol {server.Protocol} not implemented yet" };
            }
        }
        catch (Exception ex)
        {
             return new ExecuteCommandResponse { Success = false, Stderr = ex.Message };
        }
    }

    /// <summary>
    /// Sets text content to the system clipboard.
    /// </summary>
    /// <param name="request">The text to copy.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    /// <summary>
    /// Sets text content to the system clipboard.
    /// </summary>
    /// <param name="request">The text to copy.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    public override async Task<ClipboardSetTextResponse> ClipboardSetText(ClipboardSetTextRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<bool>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(request.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard Set Error: {ex}");
                tcs.SetResult(false);
            }
        });

        bool success = await tcs.Task;
        return new ClipboardSetTextResponse { Success = success };
    }

    /// <summary>
    /// Retrieves text content from the system clipboard.
    /// </summary>
    /// <param name="request">Empty request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The text content of the clipboard.</returns>
    /// <summary>
    /// Retrieves text content from the system clipboard.
    /// </summary>
    /// <param name="request">Empty request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The text content of the clipboard.</returns>
    public override async Task<ClipboardGetTextResponse> ClipboardGetText(ClipboardGetTextRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<string>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    string text = await view.GetTextAsync();
                    tcs.SetResult(text);
                }
                else
                {
                    tcs.SetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard Get Error: {ex}");
                tcs.SetResult(string.Empty);
            }
        });

        string result = await tcs.Task;
        return new ClipboardGetTextResponse { Text = result };
    }

    /// <summary>
    /// Opens a native file picker dialog for the user to select a file.
    /// </summary>
    /// <param name="request">The picker options (extensions).</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The selected file path.</returns>
    /// <summary>
    /// Opens a native file picker dialog for the user to select a file.
    /// </summary>
    /// <param name="request">The picker options (extensions).</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The selected file path.</returns>
    public override async Task<PickFileResponse> FileSystemPickFile(PickFileRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<string?>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(WinBridge.App.App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                
                if (request.AllowedExtensions != null && request.AllowedExtensions.Count > 0)
                {
                    foreach (var ext in request.AllowedExtensions)
                    {
                        picker.FileTypeFilter.Add(ext);
                    }
                }
                else
                {
                    picker.FileTypeFilter.Add("*");
                }

                var file = await picker.PickSingleFileAsync();
                tcs.SetResult(file?.Path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PickFile Error: {ex}");
                tcs.SetResult(null);
            }
        });

        string? path = await tcs.Task;
        return new PickFileResponse { Success = !string.IsNullOrEmpty(path), FilePath = path ?? string.Empty };
    }

    /// <summary>
    /// Opens a native file save dialog for the user.
    /// </summary>
    /// <param name="request">The save options (default name/extension).</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The selected save path.</returns>
    /// <summary>
    /// Opens a native file save dialog for the user.
    /// </summary>
    /// <param name="request">The save options (default name/extension).</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The selected save path.</returns>
    public override async Task<PickSaveFileResponse> FileSystemPickSaveFile(PickSaveFileRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<string?>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(WinBridge.App.App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                
                string ext = string.IsNullOrWhiteSpace(request.DefaultExtension) ? ".txt" : request.DefaultExtension;
                picker.FileTypeChoices.Add("File", new List<string>() { ext });

                if (!string.IsNullOrWhiteSpace(request.SuggestedName))
                {
                    picker.SuggestedFileName = request.SuggestedName;
                }
                else
                {
                     picker.SuggestedFileName = "New File";
                }

                var file = await picker.PickSaveFileAsync();
                tcs.SetResult(file?.Path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PickSaveFile Error: {ex}");
                tcs.SetResult(null);
            }
        });

        string? path = await tcs.Task;
        return new PickSaveFileResponse { Success = !string.IsNullOrEmpty(path), FilePath = path ?? string.Empty };
    }

    /// <summary>
    /// Retrieves a secure secret from the VaultService.
    /// </summary>
    /// <param name="request">The key identifier for the secret.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The secret value if authorized.</returns>
    /// <summary>
    /// Retrieves a secure secret from the VaultService.
    /// </summary>
    /// <param name="request">The key identifier for the secret.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The secret value if authorized.</returns>
    public override Task<VaultGetSecretResponse> VaultGetSecret(VaultGetSecretRequest request, ServerCallContext context)
    {
        try
        {

            string? secret = VaultService.RetrieveSecret(request.Key);
            
            return Task.FromResult(new VaultGetSecretResponse 
            { 
                Success = secret != null, 
                Value = secret ?? string.Empty 
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VaultGetSecret Error: {ex}");
            return Task.FromResult(new VaultGetSecretResponse { Success = false, Value = string.Empty });
        }
    }

    /// <summary>
    /// Sends text input to the currently active terminal in the UI.
    /// </summary>
    /// <param name="request">The text to send.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    /// <summary>
    /// Sends text input to the currently active terminal in the UI.
    /// </summary>
    /// <param name="request">The text to send.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    public override async Task<TerminalSendTextResponse> TerminalSendText(TerminalSendTextRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<bool>();

        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try 
            {

                if (WinBridge.App.Views.AppShellPage.Current == null)
                {
                    tcs.SetResult(false);
                    return;
                }

                var shell = WinBridge.App.Views.AppShellPage.Current;
                if (shell.PublicTabView.Visibility != Microsoft.UI.Xaml.Visibility.Visible)
                {
                    tcs.SetResult(false); 
                    return;
                }

                var selectedTab = shell.PublicTabView.SelectedItem as Microsoft.UI.Xaml.Controls.TabViewItem;
                if (selectedTab?.Content is Microsoft.UI.Xaml.Controls.Frame frame && 
                    frame.Content is WinBridge.App.Views.ServerDetailsPage serverPage)
                {
                    await serverPage.SendTextToTerminal(request.Text, request.AutoEnter);
                    tcs.SetResult(true);
                }
                else
                {
                    tcs.SetResult(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TerminalSendText Error: {ex}");
                tcs.SetResult(false);
            }
        });

        bool success = await tcs.Task;
        return new TerminalSendTextResponse { Success = success };
    }

    /// <summary>
    /// Navigates the main application window to a specific target or view.
    /// </summary>
    /// <param name="request">The navigation target (e.g., 'dashboard' or 'server:{id}').</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    /// <summary>
    /// Navigates the main application window to a specific target or view.
    /// </summary>
    /// <param name="request">The navigation target (e.g., 'dashboard' or 'server:{id}').</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    public override async Task<NavigateToResponse> NavigateTo(NavigateToRequest request, ServerCallContext context)
    {
        bool success = false;
        object? serverTarget = null;
        string serverName = "";

        if (request.Target.StartsWith("server:", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(request.Target.Substring(7), out int serverId))
            {
                 var servers = await _dataService.GetServersAsync();
                 var s = servers.FirstOrDefault(x => x.Id == serverId);
                 if (s != null)
                 {
                     serverTarget = s;
                     serverName = s.Name;
                 }
                 else
                 {
                     return new NavigateToResponse { Success = false };
                 }
            }
            else
            {
                 return new NavigateToResponse { Success = false };
            }
        }

        var tcs = new TaskCompletionSource<bool>();
        WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (request.Target.Equals("dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    WinBridge.App.Views.AppShellPage.Current.NavigateToSystemView("DashboardPage");
                    success = true;
                }
                else if (request.Target.Equals("settings", StringComparison.OrdinalIgnoreCase))
                {
                    WinBridge.App.Views.AppShellPage.Current.NavigateToSystemView("SettingsPage");
                    success = true;
                }
                else if (serverTarget != null)
                {
                     WinBridge.App.Views.AppShellPage.Current.OpenTab(serverName, typeof(WinBridge.App.Views.ServerDetailsPage), serverTarget);
                     success = true;
                }
                tcs.SetResult(success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NavigateTo Error: {ex}");
                tcs.SetResult(false);
            }
        });

        success = await tcs.Task;
        return new NavigateToResponse { Success = success };
    }

    /// <summary>
    /// Logs a debug message from a module to the host output.
    /// </summary>
    /// <param name="request">The log message and level.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    /// <summary>
    /// Logs a debug message from a module to the host output.
    /// </summary>
    /// <param name="request">The log message and level.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>Success status.</returns>
    public override Task<LogMessageResponse> LogMessage(LogMessageRequest request, ServerCallContext context)
    {
        try
        {
            
            string formattedMessage = $"[Module {request.ModuleId}] [{request.Level.ToUpper()}] {request.Message}";
            System.Diagnostics.Debug.WriteLine(formattedMessage);
            
            return Task.FromResult(new LogMessageResponse { Success = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BridgeService] LogMessage Error: {ex.Message}");
            return Task.FromResult(new LogMessageResponse { Success = false });
        }
    }

    /// <summary>
    /// Establishes a server-streaming connection to push events from Host to Module.
    /// </summary>
    /// <param name="request">The connection request.</param>
    /// <param name="responseStream">The stream to write events to.</param>
    /// <param name="context">The server call context.</param>
    /// <summary>
    /// Establishes a server-streaming connection to push events from Host to Module.
    /// </summary>
    /// <param name="request">The connection request.</param>
    /// <param name="responseStream">The stream to write events to.</param>
    /// <param name="context">The server call context.</param>
    public override async Task ListenToEvents(ListenToEventsRequest request, IServerStreamWriter<BridgeEvent> responseStream, ServerCallContext context)
    {
        Debug.WriteLine($"[BridgeService] Module {request.ModuleId} connected to event stream");

        try
        {
            
            var currentTheme = GetCurrentTheme();
            await responseStream.WriteAsync(new BridgeEvent
            {
                Type = "ThemeChanged",
                Payload = currentTheme
            });

            while (!context.CancellationToken.IsCancellationRequested)
            {
                
                await Task.Delay(1000, context.CancellationToken);

            }
        }
        catch (OperationCanceledException)
        {
            
            Debug.WriteLine($"[BridgeService] Module {request.ModuleId} disconnected from event stream");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BridgeService] ListenToEvents Error for {request.ModuleId}: {ex.Message}");
        }
    }

    private static string GetCurrentTheme()
    {
        try
        {

            var tcs = new TaskCompletionSource<string>();
            
            WinBridge.App.App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var theme = (WinBridge.App.App.MainWindow.Content as Microsoft.UI.Xaml.FrameworkElement)?.RequestedTheme ?? Microsoft.UI.Xaml.ElementTheme.Default;
                    string themeStr = theme switch
                    {
                        Microsoft.UI.Xaml.ElementTheme.Dark => "Dark",
                        Microsoft.UI.Xaml.ElementTheme.Light => "Light",
                        _ => "Default"
                    };
                    tcs.SetResult(themeStr);
                }
                catch
                {
                    tcs.SetResult("Default");
                }
            });

            return tcs.Task.Result;
        }
        catch
        {
            return "Default";
        }
    }
}

