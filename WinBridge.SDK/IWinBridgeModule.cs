using Microsoft.UI.Xaml;
using System;
using WinBridge.Models.Entities;

namespace WinBridge.SDK
{
    /// <summary>
    /// Represents the core interface that all WinBridge modules must implement.
    /// Defines the lifecycle, action availability, and UI integration for a module.
    /// </summary>
    public interface IWinBridgeModule
    {
        /// <summary>
        /// Gets the unique identifier for the module.
        /// Must be globally unique. Recommended format: Reverse Domain Name (e.g., com.developer.module).
        /// </summary>
        string UniqueId { get; }

        /// <summary>
        /// Gets the display name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the module.
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Gets or sets the server context currently active for this module instance.
        /// </summary>
        ServerModel? CurrentServer { get; set; }

        /// <summary>
        /// Initializes the module with the necessary services and UI providers.
        /// This method is called once when the module is loaded for a specific server context.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve dependencies (e.g., IRemoteService).</param>
        /// <param name="uiProvider">The UI provider for interacting with the host application (e.g., Dialogs).</param>
        void Initialize(IServiceProvider serviceProvider, IModuleUIProvider uiProvider);
        
        /// <summary>
        /// Retrieves the list of actions this module exposes to the Command Palette.
        /// </summary>
        /// <returns>An enumerable collection of available actions.</returns>
        System.Collections.Generic.IEnumerable<ModuleAction> GetAvailableActions();
        
        /// <summary>
        /// Retrieves the main UI element (Page) for this module.
        /// This UI is displayed in the module drawer/sidebar.
        /// </summary>
        /// <returns>The root UI element of the module's interface.</returns>
        UIElement GetModulePage();
    }
}
