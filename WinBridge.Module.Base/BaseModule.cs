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
        
        private UIElement? _view;
        
        public ServerModel? CurrentServer { get; set; }

        public void Initialize(IServiceProvider serviceProvider, IModuleUIProvider uiProvider)
        {
            var remoteService = serviceProvider.GetService<IRemoteService>();
            
            if (remoteService != null && CurrentServer != null)
            {
                _view = new BaseModuleView(remoteService, CurrentServer);
            }
            else
            {
                _view = new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Erreur: Service distant ou Serveur non disponible." };
            }
        }

        public System.Collections.Generic.IEnumerable<ModuleAction> GetAvailableActions()
        {
             return new System.Collections.Generic.List<ModuleAction>();
        }

        public UIElement GetModulePage()
        {
            return _view ?? new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Module non initialisé" };
        }
    }
}
