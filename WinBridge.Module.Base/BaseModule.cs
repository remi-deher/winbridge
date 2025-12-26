using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using WinBridge.Models.Entities;
using WinBridge.SDK;

namespace WinBridge.Module.Base
{
    public class BaseModule : IWinBridgeModule
    {
        public string Name => "Gestionnaire Système (Base)";
        public string Version => "1.0.0";
        public UIElement? View { get; private set; }
        
        public ServerModel? CurrentServer { get; set; }

        public void Initialize(IServiceProvider serviceProvider)
        {
            var remoteService = serviceProvider.GetService<IRemoteService>();
            
            if (remoteService != null && CurrentServer != null)
            {
                View = new BaseModuleView(remoteService, CurrentServer);
            }
            else
            {
                // Fallback or error view
                View = new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Erreur: Service distant ou Serveur non disponible." };
            }
        }
    }
}
