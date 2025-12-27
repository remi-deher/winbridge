using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace WinBridge.SDK
{
    /// <summary>
    /// Provides mechanisms for modules to interact with the host application's UI.
    /// Acts as a bridge to display dialogs or control the terminal from within a module.
    /// </summary>
    public interface IModuleUIProvider
    {
        /// <summary>
        /// Displays a modal ContentDialog within the current server tab context.
        /// This method handles the XamlRoot association automatically.
        /// </summary>
        /// <param name="content">The XAML content to display inside the dialog.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <returns>A task representing the user's interaction result.</returns>
        Task<ContentDialogResult> ShowDialogAsync(UIElement content, string title = "");

        /// <summary>
        /// Executes a command in the active server's terminal session.
        /// If the terminal is visible, the command is typed and executed interactively.
        /// </summary>
        /// <param name="command">The command string to execute.</param>
        void OpenTerminalAndExecute(string command);
    }
}
