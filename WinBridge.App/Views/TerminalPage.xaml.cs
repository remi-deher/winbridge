using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using WinBridge.App.Services;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;
using WinBridge.SDK;
using System.Threading.Tasks;

namespace WinBridge.App.Views
{
    public sealed partial class TerminalPage : Page
    {
        private ServerModel? _server;
        private IRemoteService? _remoteService;
        private readonly ModuleManager _moduleManager;

        public TerminalPage()
        {
            this.InitializeComponent();
            _moduleManager = new ModuleManager();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await TermWebView.EnsureCoreWebView2Async();
                TermWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                var html = @"
<!DOCTYPE html>
<html>
<head>
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.css"" />
    <script src=""https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.js""></script>
    <style>body { margin: 0; background: #1e1e1e; overflow: hidden; height: 100vh; }</style>
</head>
<body>
    <div id=""terminal"" style=""height: 100%; width: 100%;""></div>
    <script>
        var term = new Terminal({
            cursorBlink: true,
            theme: {
                background: '#1e1e1e',
                foreground: '#ffffff'
            },
            fontFamily: 'Consolas, monospace',
            fontSize: 14
        });
        
        var fitAddon = new FitAddon.FitAddon();
        term.loadAddon(fitAddon);
        
        term.open(document.getElementById('terminal'));
        fitAddon.fit();
        
        window.addEventListener('resize', () => fitAddon.fit());

        term.onData(e => {
            window.chrome.webview.postMessage(e);
        });

        // Expose term for external calls
        window.term = term;
    </script>
</body>
</html>";
                TermWebView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView Init Error: {ex.Message}");
            }
        }

        private void CoreWebView2_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
             var input = args.TryGetWebMessageAsString();
             if (!string.IsNullOrEmpty(input) && _remoteService != null)
             {
                 _remoteService.SendData(input);
             }
        }

        private void OnRemoteDataReceived(string data)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                if (TermWebView.CoreWebView2 == null) return;
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    await TermWebView.ExecuteScriptAsync($"if(window.term) window.term.write({json});");
                }
                catch { }
            });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ServerModel server)
            {
                _server = server;
                
                var factory = App.Services?.GetRequiredService<Core.Services.RemoteServiceFactory>();
                
                try 
                {
                    LoadingRing.IsActive = true;
                    LoadingRing.Visibility = Visibility.Visible;

                    if (factory != null)
                    {
                        // Factory now handles Connection + Fallback
                        _remoteService = await factory.ConnectAsync(_server);
                    }
                    else
                    {
                         // Emergency Fallback (should not happen)
                         if (App.Services != null)
                         {
                             _remoteService = App.Services.GetRequiredService<SshService>();
                             await _remoteService.ConnectAsync(server);
                         }
                    }
                    
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                    
                    // Show current protocol
                    System.Diagnostics.Debug.WriteLine($"Connected using: {_remoteService.Protocol}");

                    // Subscribe to stream
                    if (_remoteService != null)
                    {
                        _remoteService.DataReceived += OnRemoteDataReceived;
                    }

                    if (_remoteService is SshService sshStarted) sshStarted.StartTerminal();

                    LoadModulesForServer(server.Id);
                }
                catch (Exception ex)
                {
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                         var root = this.XamlRoot;
                         if (root == null && (Application.Current as App)?.Window?.Content is FrameworkElement fe)
                         {
                             root = fe.XamlRoot;
                         }

                         if (root != null)
                         {
                            var dialog = new ContentDialog
                            {
                                Title = "Erreur de Connexion",
                                Content = $"Impossible de se connecter à {_server.Host}: {ex.Message}",
                                CloseButtonText = "Ok",
                                XamlRoot = root
                            };
                            await dialog.ShowAsync();
                         }
                         else
                         {
                             System.Diagnostics.Debug.WriteLine($"Impossible d'afficher le dialogue d'erreur (XamlRoot null): {ex.Message}");
                         }
                    });
                }
            }
        }

        private void LoadModulesForServer(Guid serverId)
        {
            ModulesPanel.Children.Clear();

            var enabledModules = ModulesManagementPage.GetEnabledModulesForServer(serverId);

            if (!enabledModules.Any())
            {
                ModulesPanel.Children.Add(new TextBlock { 
                    Text = "Aucun module activé.", 
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0,10,0,0)
                });
                return;
            }

            foreach (var extSource in enabledModules)
            {
                if (string.IsNullOrEmpty(extSource.LocalPath)) continue;

                var result = _moduleManager.LoadModule(extSource.LocalPath);
                if (result != null)
                {
                    var module = result.Value.Module;
                    
                    // Inject Global Context
                    module.CurrentServer = _server;

                    // Inject Services
                    var services = new ServiceCollection();
                    if (_remoteService != null)
                    {
                        services.AddSingleton<IRemoteService>(_remoteService);
                        // Also register specific types just in case
                        if (_remoteService is SshService ssh) services.AddSingleton<ISshService>(ssh);
                    }
                    
                    var provider = services.BuildServiceProvider();

                    try
                    {
                        module.Initialize(provider);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error init module {module.Name}: {ex.Message}");
                        ModulesPanel.Children.Add(new TextBlock { Text = $"Erreur {module.Name}: {ex.Message}", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] });
                        continue;
                    }

                    var expander = new Expander
                    {
                        Header = module.Name,
                        IsExpanded = true,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Content = module.View ?? new TextBlock { Text = "No View provided" },
                        Margin = new Thickness(0,0,0,10)
                    };

                    ModulesPanel.Children.Add(expander);
                }
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
             base.OnNavigatingFrom(e);
             // Le service est géré par le RemoteSessionManager (Singleton), on ne le dispose pas ici.
        }
    }
}
