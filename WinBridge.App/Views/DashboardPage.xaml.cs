using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;
using Windows.Foundation;

namespace WinBridge.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly List<Windows.UI.Color> _chartColors = new()
    {
        Colors.CornflowerBlue,
        Colors.MediumSeaGreen,
        Colors.Orange,
        Colors.BlueViolet,
        Colors.Crimson,
        Colors.Teal
    };

    public DashboardPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdateDashboard();
    }

    private void UpdateDashboard()
    {
        var hour = DateTime.Now.Hour;
        TxtGreeting.Text = (hour >= 5 && hour < 18) ? "Bonjour," : "Bonsoir,";
        TxtDate.Text = DateTime.Now.ToString("dddd d MMMM yyyy");

        using var db = new AppDbContext();
        var servers = db.Servers.ToList();
        var keysCount = db.Keys.Count();

        TxtServerCount.Text = servers.Count.ToString();
        TxtKeyCount.Text = keysCount.ToString();

        GridServers.ItemsSource = servers;
        TxtNoServers.Visibility = servers.Any() ? Visibility.Collapsed : Visibility.Visible;

        DrawOsChart(servers);
        CheckServerHealth(servers);
    }

    private void DrawOsChart(List<ServerModel> servers)
    {
        ChartCanvas.Children.Clear();
        LegendStack.Children.Clear();

        if (!servers.Any())
        {
            TxtNoOsData.Visibility = Visibility.Visible;
            GridChartContainer.Visibility = Visibility.Collapsed;
            return;
        }

        TxtNoOsData.Visibility = Visibility.Collapsed;
        GridChartContainer.Visibility = Visibility.Visible;
        TxtTotalServers.Text = servers.Count.ToString();

        var stats = new Dictionary<string, int>();
        foreach (var s in servers)
        {
            var osName = "Inconnu";
            if (!string.IsNullOrEmpty(s.CachedOsInfo))
            {
                var parts = s.CachedOsInfo.Split(' ');
                if (parts.Length > 0) osName = parts[0].Replace("\"", "");
            }
            if (stats.ContainsKey(osName)) stats[osName]++;
            else stats[osName] = 1;
        }

        double total = servers.Count;
        double startAngle = -90;

        double centerX = 75;
        double centerY = 75;
        double radius = 60;
        double innerRadius = 45;
        int colorIndex = 0;

        foreach (var item in stats)
        {
            double sweepAngle = (item.Value / total) * 360;
            if (sweepAngle >= 360) sweepAngle = 359.99;

            var color = _chartColors[colorIndex % _chartColors.Count];

            var path = CreateArc(centerX, centerY, radius, innerRadius, startAngle, sweepAngle, color);
            ChartCanvas.Children.Add(path);

            var legendItem = CreateLegendItem(item.Key, item.Value, color, (item.Value / total));
            LegendStack.Children.Add(legendItem);

            startAngle += sweepAngle;
            colorIndex++;
        }
    }

    private Path CreateArc(double cx, double cy, double rOut, double rIn, double startAngle, double sweepAngle, Windows.UI.Color color)
    {
        double endAngle = startAngle + sweepAngle;
        double startRad = Math.PI * startAngle / 180.0;
        double endRad = Math.PI * endAngle / 180.0;

        Point pOutStart = new Point(cx + rOut * Math.Cos(startRad), cy + rOut * Math.Sin(startRad));
        Point pOutEnd = new Point(cx + rOut * Math.Cos(endRad), cy + rOut * Math.Sin(endRad));
        Point pInStart = new Point(cx + rIn * Math.Cos(startRad), cy + rIn * Math.Sin(startRad));
        Point pInEnd = new Point(cx + rIn * Math.Cos(endRad), cy + rIn * Math.Sin(endRad));

        bool isLargeArc = sweepAngle > 180.0;
        Size outerSize = new Size(rOut, rOut);
        Size innerSize = new Size(rIn, rIn);

        PathFigure figure = new PathFigure { StartPoint = pOutStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment { Point = pOutEnd, Size = outerSize, IsLargeArc = isLargeArc, SweepDirection = SweepDirection.Clockwise, RotationAngle = 0 });
        figure.Segments.Add(new LineSegment { Point = pInEnd });
        figure.Segments.Add(new ArcSegment { Point = pInStart, Size = innerSize, IsLargeArc = isLargeArc, SweepDirection = SweepDirection.Counterclockwise, RotationAngle = 0 });

        PathGeometry geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        // --- CORRECTION 1 : Création de l'objet Path PUIS assignation du ToolTip ---
        var path = new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Colors.Transparent),
            StrokeThickness = 0
        };

        // En C#, on utilise la méthode statique pour les propriétés attachées
        ToolTipService.SetToolTip(path, $"{sweepAngle / 3.6:F1}%");

        return path;
    }

    private Grid CreateLegendItem(string name, int count, Windows.UI.Color color, double percentage)
    {
        Grid grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };

        // --- CORRECTION 2 : Utilisation de GridLength.Auto ---
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Ellipse dot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        TextBlock txtName = new TextBlock { Text = name, FontSize = 12, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };
        TextBlock txtPercent = new TextBlock { Text = $"{percentage:P0}", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(txtName, 1);
        Grid.SetColumn(txtPercent, 2);

        grid.Children.Add(dot);
        grid.Children.Add(txtName);
        grid.Children.Add(txtPercent);

        return grid;
    }

    private void CheckServerHealth(List<ServerModel> servers)
    {
        PanelAlerts.Children.Clear();
        bool hasAlerts = false;

        foreach (var s in servers)
        {
            if (s.LastSeen == null)
            {
                AddAlert(s.Name, "Jamais connecté", Colors.Orange, "\uE7ba");
                hasAlerts = true;
            }
            else if ((DateTime.Now - s.LastSeen.Value).TotalDays > 7)
            {
                var days = (int)(DateTime.Now - s.LastSeen.Value).TotalDays;
                AddAlert(s.Name, $"Hors ligne depuis {days} jours", Colors.Crimson, "\uE7e8");
                hasAlerts = true;
            }
        }
        TxtAllGood.Visibility = hasAlerts ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddAlert(string serverName, string message, Windows.UI.Color color, string iconGlyph)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };

        // --- CORRECTION 2 : Utilisation de GridLength.Auto ---
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = iconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = new SolidColorBrush(color),
            FontSize = 16,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = serverName, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = "-", Opacity = 0.5 });
        stack.Children.Add(new TextBlock { Text = message, Opacity = 0.8, Foreground = new SolidColorBrush(color) });

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(stack, 1);
        grid.Children.Add(icon);
        grid.Children.Add(stack);
        PanelAlerts.Children.Add(grid);
    }

    private void CardServers_Tapped(object sender, TappedRoutedEventArgs e) => this.Frame.Navigate(typeof(ServerListPage));
    private void CardKeys_Tapped(object sender, TappedRoutedEventArgs e) => this.Frame.Navigate(typeof(KeysPage));
    private void GridServers_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ServerModel server) this.Frame.Navigate(typeof(ServerDashboardPage), server);
    }

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card)
        {
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            card.Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"];
            card.BorderThickness = new Thickness(2);
        }
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card)
        {
            this.ProtectedCursor = null;
            card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            card.BorderThickness = new Thickness(0);
        }
    }
}