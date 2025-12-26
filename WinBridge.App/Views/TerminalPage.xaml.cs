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
        private SshService? _sshService;
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
                
                // 1. Start SSH Connection
                _sshService = new SshService();
                
                try 
                {
                    LoadingRing.IsActive = true;
                    LoadingRing.Visibility = Visibility.Visible;

                    // Run connection in background
                    await Task.Run(() => _sshService.Connect(server));
                    
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                    
                    _sshService.StartTerminal();

                    // 2. Load Modules for this server AFTER connection is established
                    // This allows passing the connected SshService to the modules
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

            // 1. Get List of enabled modules from DB
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

            // 2. Load and Instantiate each module
            foreach (var extSource in enabledModules)
            {
                if (string.IsNullOrEmpty(extSource.LocalPath)) continue;

                var result = _moduleManager.LoadModule(extSource.LocalPath);
                if (result != null)
                {
                    var module = result.Value.Module;
                    
                    // Initialize module with context
                    // We create a service provider that exposes the CURRENT SSH Service
                    var services = new ServiceCollection();
                    if (_sshService != null)
                    {
                        services.AddSingleton<ISshService>(_sshService);
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

                    // Create UI Container for the module
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
             _sshService?.Dispose();
        }
    }
}
