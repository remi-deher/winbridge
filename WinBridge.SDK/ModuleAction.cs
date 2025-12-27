using System;
using System.Windows.Input;

namespace WinBridge.SDK
{
    public class ModuleAction
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE700"; // Default generic icon
        
        /// <summary>
        /// Command to execute when action is triggered.
        /// </summary>
        public ICommand? Command { get; set; }

        /// <summary>
        /// Simple Action to submit if Command is overkill.
        /// </summary>
        public Action? Action { get; set; }

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
