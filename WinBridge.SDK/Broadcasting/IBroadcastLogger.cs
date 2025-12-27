using System;

namespace WinBridge.SDK.Broadcasting
{
    /// <summary>
    /// Represents the severity level of a log message.
    /// </summary>
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Represents a message broadcasted to the logging system.
    /// </summary>
    public class LogMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Source { get; set; }
        public Guid? ContextId { get; set; }
    }

    /// <summary>
    /// Defines a service for broadcasting logs across the application.
    /// Allows modules to report events to the host console.
    /// </summary>
    public interface IBroadcastLogger
    {
        /// <summary>
        /// Event triggered when a new log message is received.
        /// </summary>
        event Action<LogMessage> OnLogReceived;

        /// <summary>
        /// Broadcasts a log message.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="level">The severity level.</param>
        /// <param name="source">The source of the log (e.g., Module Name).</param>
        /// <param name="contextId">The ID of the server context, if applicable.</param>
        void Log(string message, LogLevel level = LogLevel.Info, string? source = null, Guid? contextId = null);
        
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        void LogInfo(string message, string? source = null, Guid? contextId = null);
        
        /// <summary>
        /// Logs a success message.
        /// </summary>
        void LogSuccess(string message, string? source = null, Guid? contextId = null);
        
        /// <summary>
        /// Logs a warning message.
        /// </summary>
        void LogWarning(string message, string? source = null, Guid? contextId = null);
        
        /// <summary>
        /// Logs an error message.
        /// </summary>
        void LogError(string message, string? source = null, Guid? contextId = null);
    }
}
