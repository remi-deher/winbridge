using System;

namespace WinBridge.SDK.Broadcasting
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Source { get; set; }
        public Guid? ContextId { get; set; }
    }

    public interface IBroadcastLogger
    {
        event Action<LogMessage> OnLogReceived;
        void Log(string message, LogLevel level = LogLevel.Info, string? source = null, Guid? contextId = null);
        void LogInfo(string message, string? source = null, Guid? contextId = null);
        void LogSuccess(string message, string? source = null, Guid? contextId = null);
        void LogWarning(string message, string? source = null, Guid? contextId = null);
        void LogError(string message, string? source = null, Guid? contextId = null);
    }
}
