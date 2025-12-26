using Microsoft.UI.Xaml;
using System;

namespace WinBridge.SDK
{
    public interface IWinBridgeModule
    {
        string Name { get; }
        string Version { get; }
        UIElement? View { get; }
        void Initialize(IServiceProvider serviceProvider);
    }
}
