using System;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services
{
    public class BroadcastLogger : IBroadcastLogger
    {
        public event Action<LogMessage>? OnLogReceived;

        public void Log(string message, LogLevel level = LogLevel.Info, string? source = null, Guid? contextId = null)
        {
            var log = new LogMessage
            {
                Message = message,
                Level = level,
                Source = source,
                ContextId = contextId,
                Timestamp = DateTime.Now
            };

            // Invoke event (UI will subscribe to this)
            OnLogReceived?.Invoke(log);

            // Also maintain Debug output for VS
            System.Diagnostics.Debug.WriteLine($"[{log.Timestamp:HH:mm:ss}] [{level.ToString().ToUpper()}] {(source != null ? $"[{source}] " : "")}{message}");
        }

        public void LogInfo(string message, string? source = null, Guid? contextId = null) => Log(message, LogLevel.Info, source, contextId);
        public void LogSuccess(string message, string? source = null, Guid? contextId = null) => Log(message, LogLevel.Success, source, contextId);
        public void LogWarning(string message, string? source = null, Guid? contextId = null) => Log(message, LogLevel.Warning, source, contextId);
        public void LogError(string message, string? source = null, Guid? contextId = null) => Log(message, LogLevel.Error, source, contextId);
    }
}
