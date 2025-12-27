using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinBridge.SDK.Broadcasting;
using WinBridge.Core.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinBridge.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public Window? Window { get; private set; }
        public static IServiceProvider? Services { get; private set; }
        public static event EventHandler? ServerListChanged;
        public static void RaiseServerListChanged() => ServerListChanged?.Invoke(null, EventArgs.Empty);

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            
            ConfigureServices();

            // Créer la base de données si elle n'existe pas
            using var db = new WinBridge.Core.Data.AppDbContext();
            db.Database.EnsureCreated();
        }

        private void ConfigureServices()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            
            // Core Services
            services.AddSingleton<IBroadcastLogger, BroadcastLogger>();
            services.AddSingleton<RemoteSessionManager>();
            services.AddSingleton<SecurePipeService>();
            services.AddSingleton<SshAgentService>();
            
            services.AddTransient<VaultService>();
            services.AddTransient<SshService>();
            services.AddTransient<WinRmService>();
            services.AddSingleton<RemoteServiceFactory>();

            Services = services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Window = new MainWindow();
            Window.Activate();
        }
    }
}
