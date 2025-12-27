using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace WinBridge.SDK
{
    public interface IModuleUIProvider
    {
        /// <summary>
        /// Displays a ContentDialog within the current context (Server Tab).
        /// </summary>
        /// <param name="content">The content to display inside the dialog.</param>
        /// <param name="title">Optional dialog title.</param>
        Task<ContentDialogResult> ShowDialogAsync(UIElement content, string title = "");

        /// <summary>
        /// Opens a terminal or uses the existing one to execute a command.
        /// </summary>
        void OpenTerminalAndExecute(string command);
    }
}
