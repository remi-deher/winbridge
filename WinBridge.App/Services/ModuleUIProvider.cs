using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.SDK;

namespace WinBridge.App.Services
{
    /// <summary>
    /// Implementation of the SDK UI Provider, bridging Module requests to the App Shell.
    /// </summary>
    public class ModuleUIProvider : IModuleUIProvider
    {
        private readonly XamlRoot _xamlRoot;
        private readonly Action<string> _terminalAction;

        public ModuleUIProvider(XamlRoot xamlRoot, Action<string> terminalAction)
        {
            _xamlRoot = xamlRoot;
            _terminalAction = terminalAction;
        }

        public async Task<ContentDialogResult> ShowDialogAsync(UIElement content, string title = "")
        {
            if (_xamlRoot == null) throw new InvalidOperationException("UI Context (XamlRoot) is missing.");

            var dialog = new ContentDialog
            {
                Content = content,
                Title = title,
                CloseButtonText = "Fermer",
                XamlRoot = _xamlRoot
            };
            return await dialog.ShowAsync();
        }

        public void OpenTerminalAndExecute(string command)
        {
            _terminalAction?.Invoke(command);
        }
    }
}
