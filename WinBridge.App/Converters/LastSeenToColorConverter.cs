using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;

namespace WinBridge.App.Converters;

public class LastSeenToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime lastSeen)
        {
            // Vert Vif si vu il y a moins de 10 minutes
            if ((DateTime.Now - lastSeen).TotalMinutes < 10)
                return new SolidColorBrush(Colors.LimeGreen);

            // Orange si vu il y a moins d'une heure
            if ((DateTime.Now - lastSeen).TotalHours < 1)
                return new SolidColorBrush(Colors.Orange);
        }
        // Gris (Inactif) sinon
        return new SolidColorBrush(Colors.LightGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}