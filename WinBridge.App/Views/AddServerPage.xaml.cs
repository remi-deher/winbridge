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

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var newServer = new ServerModel
        {
            Name = TxtName.Text,
            Host = TxtHost.Text,
            Port = (int)NumPort.Value,
            Username = TxtUser.Text,
            Password = TxtPassword.Password
        };

        try
        {
            using var db = new WinBridge.Core.Data.AppDbContext();
            db.Servers.Add(newServer);
            await db.SaveChangesAsync();

            _ = DisplayMessage("Succès", $"Le serveur {newServer.Name} a été enregistré en base de données !");

            // Optionnel : Vider les champs après l'enregistrement
            TxtName.Text = TxtHost.Text = TxtUser.Text = TxtPassword.Password = "";
        }
        catch (Exception ex)
        {
            _ = DisplayMessage("Erreur", "Impossible de sauvegarder : " + ex.Message);
        }
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