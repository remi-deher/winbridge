using Microsoft.UI.Xaml;
using System;
using WinBridge.Models.Entities;

namespace WinBridge.SDK
{
    public interface IWinBridgeModule
    {
        string Name { get; }
        string Version { get; }
        
        ServerModel? CurrentServer { get; set; }

        void Initialize(IServiceProvider serviceProvider, IModuleUIProvider uiProvider);
        
        System.Collections.Generic.IEnumerable<ModuleAction> GetAvailableActions();
        
        UIElement GetModulePage();
    }
}
