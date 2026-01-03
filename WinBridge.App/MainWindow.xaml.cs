using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.App.Views;

namespace WinBridge.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(AppShellPage));
    }
}
