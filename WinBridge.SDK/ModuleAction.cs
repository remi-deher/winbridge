using System;
using System.Windows.Input;

namespace WinBridge.SDK
{
    /// <summary>
    /// Represents a single executable action exposed by a module.
    /// Actions are displayed in the Command Palette and Contextual Menus.
    /// </summary>
    public class ModuleAction
    {
        /// <summary>
        /// Gets or sets the display title of the action.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a brief description of what the action does.
        /// Displayed as a subtitle in the Command Palette.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Segoe MDL2 Assets glyph code for the icon.
        /// Default is \uE700.
        /// </summary>
        public string IconGlyph { get; set; } = "\uE700";
        
        /// <summary>
        /// Gets or sets the ICommand to execute. 
        /// Use this for complex MVVM-style interactions.
        /// </summary>
        public ICommand? Command { get; set; }

        /// <summary>
        /// Gets or sets a simple Delegate Action to execute.
        /// Preferred for simple, one-off logic.
        /// </summary>
        public Action? Action { get; set; }

        /// <summary>
        /// Executes the associated Action or ICommand.
        /// </summary>
        public void Execute()
        {
            Action?.Invoke();
            if (Command != null && Command.CanExecute(null))
            {
                Command.Execute(null);
            }
        }
    }
}
