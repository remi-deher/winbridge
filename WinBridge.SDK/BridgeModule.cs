using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grpc.Net.Client;
using Grpc.Core;
using WinBridge.Core.Grpc; 
using System.Net;

namespace WinBridge.SDK;

/// <summary>
/// Represents a WinBridge module, providing access to core services and UI extensions.
/// This is the main entry point for developing third-party modules.
/// </summary>
public class BridgeModule
{
    /// <summary>
    /// Gets the unique identifier of the module.
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// Gets the display name of the module.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the version of the module.
    /// </summary>
    public string Version { get; }

    private readonly List<UiExtension> _extensions = new();
    private readonly Dictionary<string, Action<string>> _actions = new();
    private readonly Dictionary<string, Action<WinBridgeHost.WinBridgeHostClient, HttpContext>> _getHandlers = new();
    private readonly Dictionary<string, Action<WinBridgeHost.WinBridgeHostClient, HttpContext>> _postHandlers = new();

    private readonly List<OsType> _supportedOs = new();
    private string _testedOnInfo = string.Empty;

    private readonly string _securityToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="BridgeModule"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the module.</param>
    /// <param name="name">The display name of the module.</param>
    /// <param name="version">The version string of the module.</param>
    public BridgeModule(string id, string name, string version)
    {
        Id = id;
        Name = name;
        Version = version;
        _securityToken = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Declares that this module supports Linux servers.
    /// </summary>
    public void SupportsLinux() => _supportedOs.Add(OsType.Linux);

    /// <summary>
    /// Declares that this module supports Windows servers.
    /// </summary>
    public void SupportsWindows() => _supportedOs.Add(OsType.Windows);

    /// <summary>
    /// Sets information about the environment this module was tested on.
    /// </summary>
    /// <param name="info">Description of the testing environment.</param>
    public void TestedOn(string info) => _testedOnInfo = info;

    /// <summary>
    /// Adds a new tab to the Server Details view.
    /// </summary>
    /// <param name="title">The title of the tab.</param>
    /// <param name="icon">The icon glyph to display.</param>
    /// <param name="relativePath">The relative path to the HTML entry point.</param>
    public void AddTab(string title, string icon, string relativePath)
    {
        _extensions.Add(new UiExtension
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            IconGlyph = icon,
            EntryPoint = relativePath, 
            Type = ExtensionType.ServerTab,
            SessionToken = _securityToken
        });
    }

    /// <summary>
    /// Adds a context menu action for the server.
    /// </summary>
    /// <param name="title">The title of the action.</param>
    /// <param name="icon">The icon glyph to display.</param>
    /// <param name="onExecute">The action to execute when triggered. Receives the server ID.</param>
    public void AddAction(string title, string icon, Action<string> onExecute)
    {
        var commandId = Guid.NewGuid().ToString();
        _actions[commandId] = onExecute;

        _extensions.Add(new UiExtension
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            IconGlyph = icon,
            Type = ExtensionType.ServerAction,
            CommandId = commandId
        });
    }

    /// <summary>
    /// Registers a handler for HTTP GET requests.
    /// </summary>
    /// <param name="route">The route to handle (e.g., "/api/data").</param>
    /// <param name="handler">The action to execute. Receives the client and http context.</param>
    public void HandleGet(string route, Action<WinBridgeHost.WinBridgeHostClient, HttpContext> handler)
    {
        
        if (!route.StartsWith("/")) route = "/" + route;
        _getHandlers[route] = handler;
    }

    /// <summary>
    /// Registers a handler for HTTP POST requests.
    /// </summary>
    /// <param name="route">The route to handle (e.g., "/api/submit").</param>
    /// <param name="handler">The action to execute. Receives the client and http context.</param>
    public void HandlePost(string route, Action<WinBridgeHost.WinBridgeHostClient, HttpContext> handler)
    {
        
        if (!route.StartsWith("/")) route = "/" + route;
        _postHandlers[route] = handler;
    }

    /// <summary>
    /// Starts the module, connecting to the WinBridge Core and hosting the web server.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. This task continues until the process exits.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection to Core fails.</exception>
    public async Task RunAsync()
    {

        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) =>
                {
                    var pipeName = WinBridge.Core.WinBridgeConstants.PipeName;
                    var stream = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
                    await stream.ConnectAsync(token);
                    return stream;
                }
            }
        });

        _activeClient = new WinBridgeHost.WinBridgeHostClient(channel);
        var client = _activeClient;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        
        var app = builder.Build();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.Combine(AppContext.BaseDirectory)),
            RequestPath = ""
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value == "/favicon.ico") 
            { 
                context.Response.StatusCode = 404; 
                return; 
            }

            if (context.Request.Query["token"] != _securityToken)
            {

                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Invalid Token");
                return;
            }
            await next();
        });

        foreach (var route in _getHandlers.Keys)
        {
            app.MapGet(route, async (HttpContext context) => 
            {
                try
                {
                    _getHandlers[route](client, context);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"Error in GET handler: {ex.Message}");
                }
            });
        }

        foreach (var route in _postHandlers.Keys)
        {
            app.MapPost(route, async (HttpContext context) => 
            {
                try
                {
                    _postHandlers[route](client, context);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"Error in POST handler: {ex.Message}");
                }
            });
        }

        await app.StartAsync();
        var localUrl = app.Urls.First(); 

        var registration = new ModuleRegistration
        {
            ModuleId = Id,
            ApiVersion = Version,
            TestedOnInfo = _testedOnInfo
        };
        
        registration.SupportedOs.Add(_supportedOs);

        foreach (var ext in _extensions)
        {
            if (ext.Type == ExtensionType.ServerTab || ext.Type == ExtensionType.DashboardWidget)
            {
                ext.BaseUrl = localUrl;
                
            }
            registration.UiExtensions.Add(ext);
        }

        try 
        {
            var response = await client.RegisterModuleAsync(registration);
            Console.WriteLine($"[Module {Name}] ConnectÃ© avec succÃ¨s. Session: {response.SessionToken}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Module {Name}] Erreur de connexion au Core: {ex.Message}");
            
        }

        _ = Task.Run(async () => await ListenToEventsAsync());

        await Task.Delay(-1);
    }

    private IStorage? _storage;
    public IStorage Storage => _storage ??= new InternalStorage(this);

    private ILogger? _logger;
    public ILogger Logger => _logger ??= new InternalLogger(this);

    private ITheme? _theme;
    public ITheme Theme => _theme ??= new InternalTheme();

    private WinBridgeHost.WinBridgeHostClient? _activeClient;
    private WinBridgeHost.WinBridgeHostClient GetClient() 
    {
        
        if (_activeClient == null)
        {

            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    ConnectCallback = async (ctx, tok) =>
                    {
                        var pipeName = WinBridge.Core.WinBridgeConstants.PipeName;
                        var stream = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
                        await stream.ConnectAsync(tok);
                        return stream;
                    }
                }
            });
            return new WinBridgeHost.WinBridgeHostClient(channel);
        }
        return _activeClient;
    }

    /// <summary>
    /// Defines the types of toast notifications.
    /// </summary>
    public enum NotificationType { Info, Success, Error, Warning }

    /// <summary>
    /// Shows a toast notification in the host application.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of notification.</param>
    public void ShowToast(string message, NotificationType type = NotificationType.Info)
    {
        
        Task.Run(async () => 
        {
            try
            {
                await GetClient().ShowToastAsync(new ShowToastRequest
                {
                    ModuleId = Id,
                    Title = Name,
                    Message = message,
                    Type = type.ToString()
                });
            }
            catch (Exception ex) { Console.WriteLine($"[Error] ShowToast: {ex.Message}"); }
        });
    }

    /// <summary>
    /// Shows a confirmation dialog to the user.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display.</param>
    /// <returns>True if the user accepted (Yes), false otherwise.</returns>
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        try
        {
            var response = await GetClient().ShowDialogAsync(new ShowDialogRequest
            {
                ModuleId = Id,
                Title = title,
                Message = message,
                Buttons = "YesNo"
            });
            return response.Result == "Yes";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] ShowConfirmationAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Provides access to persistent key-value storage for the module.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Retrieves a stored value by key.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve.</typeparam>
        /// <param name="key">The key identifier.</param>
        /// <returns>The stored value or default if not found.</returns>
        Task<T?> GetAsync<T>(string key);
        
        /// <summary>
        /// Saves a value to persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of value to store.</typeparam>
        /// <param name="key">The key identifier.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveAsync<T>(string key, T value);
    }

    private class InternalStorage(BridgeModule parent) : IStorage
    {
        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var response = await parent.GetClient().StorageGetAsync(new StorageGetRequest
                {
                    ModuleId = parent.Id,
                    Key = key
                });

                if (string.IsNullOrEmpty(response.Value)) return default;
                return System.Text.Json.JsonSerializer.Deserialize<T>(response.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Storage.GetAsync: {ex.Message}");
                return default;
            }
        }

        public async Task SaveAsync<T>(string key, T value)
        {
             try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(value);
                await parent.GetClient().StorageSetAsync(new StorageSetRequest
                {
                    ModuleId = parent.Id,
                    Key = key,
                    Value = json
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Storage.SaveAsync: {ex.Message}");
            }
        }
    }

    private INetwork? _network;
    public INetwork Network => _network ??= new InternalNetwork(this);

    /// <summary>
    /// Provides access to network and server management operations.
    /// </summary>
    public interface INetwork
    {
        /// <summary>
        /// Retrieves the list of available servers.
        /// </summary>
        /// <param name="filter">Optional filter by OS type.</param>
        /// <returns>A list of servers.</returns>
        Task<List<ServerInfo>> GetServersAsync(OsType? filter = null);

        /// <summary>
        /// Executes a command on a specific server.
        /// </summary>
        /// <param name="serverId">The ID of the target server.</param>
        /// <param name="command">The command to execute (e.g., shell command).</param>
        /// <param name="timeout">Optional timeout. defaults to 30s.</param>
        /// <returns>The result of the command execution.</returns>
        Task<CommandResult> ExecuteOnAsync(string serverId, string command, TimeSpan? timeout = null);

        /// <summary>
        /// Executes a command on multiple servers in parallel.
        /// </summary>
        /// <param name="serverIds">List of target server IDs.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A dictionary mapping server IDs to their respective command results.</returns>
        Task<Dictionary<string, CommandResult>> ExecuteOnAllAsync(IEnumerable<string> serverIds, string command);
    }

    /// <summary>
    /// Represents basic information about a server.
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// Gets or sets the unique server ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the server name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the server host address.
        /// </summary>
        public string Host { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the protocol used.
        /// </summary>
        public string Protocol { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the port number.
        /// </summary>
        public int Port { get; set; }
    }

    /// <summary>
    /// Represents the result of a command execution on a remote server.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the RPC call was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the standard output of the command.
        /// </summary>
        public string Stdout { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the standard error output of the command.
        /// </summary>
        public string Stderr { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the exit code of the process.
        /// </summary>
        public int ExitCode { get; set; }
    }

    private class InternalNetwork(BridgeModule parent) : INetwork
    {
        public async Task<List<ServerInfo>> GetServersAsync(OsType? filter = null)
        {
             try
            {
                var response = await parent.GetClient().GetServersAsync(new WinBridge.Core.Grpc.Empty());

                return response.Servers.Select(s => new ServerInfo 
                {
                    Id = s.Id.ToString(),
                    Name = s.Name,
                    Host = s.Host,
                    Protocol = s.Protocol,
                    Port = s.Port
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Network.GetServersAsync: {ex.Message}");
                return new List<ServerInfo>();
            }
        }

        public async Task<CommandResult> ExecuteOnAsync(string serverId, string command, TimeSpan? timeout = null)
        {
            if (!int.TryParse(serverId, out int id))
            {
                return new CommandResult { Success = false, Stderr = "Invalid Server ID format (must be integer)" };
            }

            try
            {
                var response = await parent.GetClient().ExecuteCommandAsync(new ExecuteCommandRequest
                {
                    ModuleId = parent.Id,
                    ServerId = id,
                    Command = command,
                    TimeoutSeconds = (int)(timeout?.TotalSeconds ?? 30)
                });

                return new CommandResult 
                {
                    Success = response.Success,
                    Stdout = response.Stdout,
                    Stderr = response.Stderr,
                    ExitCode = response.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Stderr = $"RPC Error: {ex.Message}" };
            }
        }

        public async Task<Dictionary<string, CommandResult>> ExecuteOnAllAsync(IEnumerable<string> serverIds, string command)
        {
            var results = new Dictionary<string, CommandResult>();
            
            var tasks = serverIds.Select(async id => 
            {
                var result = await ExecuteOnAsync(id, command);
                return (id, result);
            });

            var taskResults = await Task.WhenAll(tasks);
            foreach(var (id, res) in taskResults)
            {
                results[id] = res;
            }
            return results;
        }
    }

    private IClipboard? _clipboard;
    public IClipboard Clipboard => _clipboard ??= new InternalClipboard(this);

    /// <summary>
    /// Provides access to the system clipboard.
    /// </summary>
    public interface IClipboard
    {
        /// <summary>
        /// Sets the content of the clipboard.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        Task SetTextAsync(string text);

        /// <summary>
        /// Retrieves the current text content of the clipboard.
        /// </summary>
        /// <returns>The clipboard text.</returns>
        Task<string> GetTextAsync();
    }

    private class InternalClipboard(BridgeModule parent) : IClipboard
    {
        public async Task SetTextAsync(string text)
        {
            try
            {
                await parent.GetClient().ClipboardSetTextAsync(new ClipboardSetTextRequest
                {
                    ModuleId = parent.Id,
                    Text = text
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Clipboard.SetTextAsync: {ex.Message}");
            }
        }

        public async Task<string> GetTextAsync()
        {
            try
            {
                var response = await parent.GetClient().ClipboardGetTextAsync(new ClipboardGetTextRequest
                {
                    ModuleId = parent.Id
                });
                return response.Text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Clipboard.GetTextAsync: {ex.Message}");
                return string.Empty;
            }
        }
    }

    private IFileSystem? _fileSystem;
    public IFileSystem FileSystem => _fileSystem ??= new InternalFileSystem(this);

    /// <summary>
    /// Provides interaction with the local file system (where the app is running).
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Opens a file picker dialog for the user to select a file.
        /// </summary>
        /// <param name="extensions">Allowed file extensions (e.g., ".txt", ".json").</param>
        /// <returns>The full path of the selected file, or null if cancelled.</returns>
        Task<string?> PickFileAsync(params string[] extensions);

        /// <summary>
        /// Opens a save file picker dialog.
        /// </summary>
        /// <param name="suggestedName">The suggested filename.</param>
        /// <param name="extension">The default extension.</param>
        /// <returns>The full path where the file should be saved, or null if cancelled.</returns>
        Task<string?> PickSaveFileAsync(string suggestedName, string extension);
    }

    private class InternalFileSystem(BridgeModule parent) : IFileSystem
    {
        public async Task<string?> PickFileAsync(params string[] extensions)
        {
            try
            {
                var request = new PickFileRequest { ModuleId = parent.Id };
                if (extensions != null)
                {
                    request.AllowedExtensions.AddRange(extensions);
                }

                var response = await parent.GetClient().FileSystemPickFileAsync(request);
                return response.Success ? response.FilePath : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] FileSystem.PickFileAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> PickSaveFileAsync(string suggestedName, string extension)
        {
            try
            {
                var request = new PickSaveFileRequest 
                { 
                    ModuleId = parent.Id,
                    SuggestedName = suggestedName,
                    DefaultExtension = extension
                };

                var response = await parent.GetClient().FileSystemPickSaveFileAsync(request);
                return response.Success ? response.FilePath : null;
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[Error] FileSystem.PickSaveFileAsync: {ex.Message}");
                 return null;
            }
        }
    }

    private IVault? _vault;
    public IVault Vault => _vault ??= new InternalVault(this);

    /// <summary>
    /// Provides access to the secure credential vault.
    /// </summary>
    public interface IVault
    {
        /// <summary>
        /// Retrieves a secret (e.g., password, key) from the vault.
        /// </summary>
        /// <param name="key">The key or ID of the secret.</param>
        /// <returns>The secret value, or null if not found/unauthorized.</returns>
        Task<string?> GetSecretAsync(string key);
    }

    private class InternalVault(BridgeModule parent) : IVault
    {
        public async Task<string?> GetSecretAsync(string key)
        {
            try
            {
                var response = await parent.GetClient().VaultGetSecretAsync(new VaultGetSecretRequest
                {
                    ModuleId = parent.Id,
                    Key = key
                });
                return response.Success ? response.Value : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Vault.GetSecretAsync: {ex.Message}");
                return null;
            }
        }
    }

    private ITerminal? _terminal;
    public ITerminal Terminal => _terminal ??= new InternalTerminal(this);

    /// <summary>
    /// Provides interaction with the active terminal session.
    /// </summary>
    public interface ITerminal
    {
        /// <summary>
        /// Sends text input to the currently active terminal.
        /// </summary>
        /// <param name="text">The text to send.</param>
        /// <param name="autoEnter">If true, appends a newline character.</param>
        /// <returns>True if sent successfully.</returns>
        Task<bool> SendTextAsync(string text, bool autoEnter = false);
    }

    private class InternalTerminal(BridgeModule parent) : ITerminal
    {
        public async Task<bool> SendTextAsync(string text, bool autoEnter = false)
        {
            try
            {
                var response = await parent.GetClient().TerminalSendTextAsync(new TerminalSendTextRequest
                {
                    ModuleId = parent.Id,
                    Text = text,
                    AutoEnter = autoEnter
                });
                return response.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Terminal.SendTextAsync: {ex.Message}");
                return false;
            }
        }
    }

    private INavigation? _navigation;
    public INavigation Navigation => _navigation ??= new InternalNavigation(this);

    /// <summary>
    /// Provides methods to navigate within the host application.
    /// </summary>
    public interface INavigation
    {
        /// <summary>
        /// Navigates to the main Dashboard.
        /// </summary>
        Task<bool> GoToDashboardAsync();
        
        /// <summary>
        /// Navigates to the Settings page.
        /// </summary>
        Task<bool> GoToSettingsAsync();

        /// <summary>
        /// Opens the details view for a specific server (and connects if applicable).
        /// </summary>
        /// <param name="serverId">The ID of the server to open.</param>
        Task<bool> OpenServerAsync(int serverId);
    }

    private class InternalNavigation(BridgeModule parent) : INavigation
    {
        public Task<bool> GoToDashboardAsync() => NavigateAsync("dashboard");
        public Task<bool> GoToSettingsAsync() => NavigateAsync("settings");
        public Task<bool> OpenServerAsync(int serverId) => NavigateAsync($"server:{serverId}");

        private async Task<bool> NavigateAsync(string target)
        {
            try
            {
                var response = await parent.GetClient().NavigateToAsync(new NavigateToRequest
                {
                    ModuleId = parent.Id,
                    Target = target
                });
                return response.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Navigation.NavigateAsync: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Provides logging capabilities to the host's specialized log viewer.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        void Info(string message);
        /// <summary>
        /// Logs a warning message.
        /// </summary>
        void Warning(string message);
        /// <summary>
        /// Logs an error message.
        /// </summary>
        void Error(string message);
    }

    private class InternalLogger(BridgeModule parent) : ILogger
    {
        public void Info(string message) => LogAsync(message, "Info");
        public void Warning(string message) => LogAsync(message, "Warning");
        public void Error(string message) => LogAsync(message, "Error");

        private void LogAsync(string message, string level)
        {
            
            Task.Run(async () =>
            {
                try
                {
                    await parent.GetClient().LogMessageAsync(new LogMessageRequest
                    {
                        ModuleId = parent.Id,
                        Message = message,
                        Level = level
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Logger.LogAsync: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Provides access to the current application theme.
    /// </summary>
    public interface ITheme
    {
        /// <summary>
        /// Gets the current theme name (e.g., "Dark", "Light").
        /// </summary>
        string Current { get; }
        /// <summary>
        /// Event triggered when the application theme changes.
        /// </summary>
        event EventHandler<string>? OnThemeChanged;
    }

    private class InternalTheme : ITheme
    {
        private string _current = "Default";
        
        public string Current
        {
            get => _current;
            internal set
            {
                if (_current != value)
                {
                    _current = value;
                    OnThemeChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<string>? OnThemeChanged;
    }

    private async Task ListenToEventsAsync()
    {
        while (true)
        {
            try
            {
                var call = GetClient().ListenToEvents(new ListenToEventsRequest
                {
                    ModuleId = Id
                });

                await foreach (var evt in call.ResponseStream.ReadAllAsync())
                {
                    if (evt.Type == "ThemeChanged")
                    {
                        if (_theme is InternalTheme internalTheme)
                        {
                            internalTheme.Current = evt.Payload;
                            Console.WriteLine($"[Module {Name}] Theme changed to: {evt.Payload}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Module {Name}] Event stream disconnected: {ex.Message}. Reconnecting in 5s...");
                await Task.Delay(5000);
            }
        }
    }
}

