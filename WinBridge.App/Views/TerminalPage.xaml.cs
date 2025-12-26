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
                
                // Initialization script for xterm.js would go here
                // For now we set a placeholder to confirm it renders
                TermWebView.NavigateToString(@"
                    <html>
                        <body style='background-color:#1e1e1e; color:white; font-family: Segoe UI, sans-serif; height: 100vh; display: flex; align-items: center; justify-content: center;'>
                            <h2>Terminal Ready</h2>
                        </body>
                    </html>");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView Init Error: {ex.Message}");
            }
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
                         _remoteService = new SshService();
                         ((SshService)_remoteService).Connect(server);
                    }
                    
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                    
                    // Show current protocol
                    System.Diagnostics.Debug.WriteLine($"Connected using: {_remoteService.Protocol}");

                    if (_remoteService is SshService sshStarted) sshStarted.StartTerminal();

                    LoadModulesForServer(server.Id);
                }
                catch (Exception ex)
                {
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;

                    var dialog = new ContentDialog
                    {
                        Title = "Erreur de Connexion",
                        Content = $"Impossible de se connecter à {_server.Host}: {ex.Message}",
                        CloseButtonText = "Ok",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
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
             (_remoteService as IDisposable)?.Dispose();
        }
    }
}
