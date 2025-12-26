using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class TerminalPage : Page
{
    private SshService _sshService = new SshService();

    public TerminalPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ServerModel server)
        {
            _sshService.DataReceived += (data) =>
            {
                // L'UI doit être mise à jour sur le thread principal
                DispatcherQueue.TryEnqueue(() =>
                {
                    TxtTerminalOutput.Text += data + "\n";
                    TerminalScroll.ChangeView(0, TerminalScroll.ScrollableHeight, 1);
                });
            };

            _sshService.Connect(server);
        }
    }

    private void TxtInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _sshService.SendCommand(TxtInput.Text);
            TxtInput.Text = "";
        }
    }
}