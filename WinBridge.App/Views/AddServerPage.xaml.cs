using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.Models.Entities;
using System;

namespace WinBridge.App.Views;

public sealed partial class AddServerPage : Page
{
    public AddServerPage()
    {
        this.InitializeComponent();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var newServer = new ServerModel
        {
            Name = TxtName.Text,
            Host = TxtHost.Text,
            Port = (int)NumPort.Value,
            Username = TxtUser.Text,
            Password = TxtPassword.Password
        };

        // Remplacement de ShowMessage par un ContentDialog simple
        _ = DisplayMessage("Succès", $"Serveur {newServer.Name} configuré !");
    }

    private async System.Threading.Tasks.Task DisplayMessage(string title, string content)
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}