using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.App.Components
{
    public sealed partial class LogsControl : UserControl
    {
        public ObservableCollection<LogMessage> AllLogs { get; } = new();
        public ObservableCollection<LogMessage> ServerLogs { get; } = new();

        public static readonly DependencyProperty ServerIdProperty =
            DependencyProperty.Register(nameof(ServerId), typeof(Guid?), typeof(LogsControl), new PropertyMetadata(null, OnServerIdChanged));

        public Guid? ServerId
        {
            get => (Guid?)GetValue(ServerIdProperty);
            set => SetValue(ServerIdProperty, value);
        }

        private IBroadcastLogger? _logger;

        public LogsControl()
        {
            this.InitializeComponent();
            _logger = App.Services?.GetService<IBroadcastLogger>();

            if (_logger != null)
            {
                _logger.OnLogReceived += Logger_OnLogReceived;
            }

            this.Unloaded += LogsControl_Unloaded;
        }

        private void Logger_OnLogReceived(LogMessage msg)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AllLogs.Add(msg);
                if (ServerId.HasValue && msg.ContextId == ServerId.Value)
                {
                    ServerLogs.Add(msg);
                    ScrollToBottom(ListServerLogs);
                }
                ScrollToBottom(ListGlobalLogs);
            });
        }

        private static void OnServerIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogsControl ctrl)
            {
                ctrl.ServerLogs.Clear();
                // We could re-filter historical logs here if we kept them all in memory,
                // but for now we just start showing fresh logs for the new server.
                // Or filtered from AllLogs:
                var newId = (Guid?)e.NewValue;
                if (newId.HasValue)
                {
                    foreach(var log in ctrl.AllLogs.Where(l => l.ContextId == newId.Value))
                    {
                        ctrl.ServerLogs.Add(log);
                    }
                }
            }
        }

        private void ScrollToBottom(ListView list)
        {
            if (list.Items.Count > 0)
            {
                list.ScrollIntoView(list.Items[list.Items.Count - 1]);
            }
        }

        private void LogsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_logger != null)
            {
                _logger.OnLogReceived -= Logger_OnLogReceived;
            }
        }
    }

    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => new SolidColorBrush(Colors.White),
                    LogLevel.Success => new SolidColorBrush(Colors.LightGreen),
                    LogLevel.Warning => new SolidColorBrush(Colors.Yellow),
                    LogLevel.Error => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    
    public class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
             if (value is DateTime dt) return dt.ToString("HH:mm:ss");
             return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
