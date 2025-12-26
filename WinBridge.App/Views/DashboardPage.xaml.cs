using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using WinBridge.Core.Data;

namespace WinBridge.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadStats();
    }

    private void LoadStats()
    {
        using var db = new AppDbContext();
        int srvCount = db.Servers.Count();
        int keyCount = db.Keys.Count();

        TxtServerCount.Text = $"{srvCount} enregistrés";
        TxtKeyCount.Text = $"{keyCount} configurées";
    }
}