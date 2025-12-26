using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinBridge.Core.Data;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class KeysPage : Page
{
    private readonly VaultService _vaultService; // Idéalement injecté

    public KeysPage()
    {
        this.InitializeComponent();
        _vaultService = new VaultService();
        LoadKeys();
    }

    private void LoadKeys()
    {
        using var db = new AppDbContext();
        var keys = db.Keys.OrderBy(k => k.Name).ToList();
        KeysList.ItemsSource = keys;
        TxtEmpty.Visibility = keys.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        // On crée la boîte de dialogue
        var dialog = new ContentDialog
        {
            Title = "Importer une clé privée",
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        // Contenu du formulaire (Généré dynamiquement ou via un UserControl séparé)
        var stack = new StackPanel { Spacing = 12, MinWidth = 400 };
        var txtName = new TextBox { Header = "Nom de la clé (ex: Serveurs Prod)" };
        var txtUser = new TextBox { Header = "Utilisateur par défaut", PlaceholderText = "root" };
        var txtContent = new TextBox
        {
            Header = "Contenu de la clé privée (PEM / OpenSSH)",
            AcceptsReturn = true,
            Height = 200,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
        };
        var txtPassphrase = new PasswordBox { Header = "Passphrase (laisser vide si aucune)" };

        stack.Children.Add(txtName);
        stack.Children.Add(txtUser);
        stack.Children.Add(txtContent);
        stack.Children.Add(txtPassphrase);
        dialog.Content = stack;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtContent.Text))
            {
                // Gestion d'erreur basique
                return;
            }

            await SaveKeyAsync(txtName.Text, txtUser.Text, txtContent.Text, txtPassphrase.Password);
        }
    }

    private async Task SaveKeyAsync(string name, string user, string content, string passphrase)
    {
        try
        {
            // 1. Sauvegarder les métadonnées en BDD
            var newKey = new SshKeyModel
            {
                Name = name,
                DefaultUsername = string.IsNullOrWhiteSpace(user) ? "root" : user,
                HasPassphrase = !string.IsNullOrEmpty(passphrase)
            };

            using var db = new AppDbContext();
            db.Keys.Add(newKey);
            await db.SaveChangesAsync();

            // 2. Sauvegarder le secret dans le Vault Windows
            _vaultService.SaveKeyContent(newKey.Id.ToString(), content, passphrase);

            // 3. Rafraîchir l'interface
            LoadKeys();
        }
        catch (Exception ex)
        {
            // Afficher une erreur
            var errDialog = new ContentDialog
            {
                Title = "Erreur",
                Content = $"Impossible de sauvegarder la clé : {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errDialog.ShowAsync();
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SshKeyModel key)
        {
            // Confirmation
            var confirm = new ContentDialog
            {
                Title = "Supprimer la clé ?",
                Content = $"Voulez-vous vraiment supprimer la clé '{key.Name}' ? Cette action est irréversible.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                // Suppression BDD
                using var db = new AppDbContext();
                db.Keys.Remove(key);
                await db.SaveChangesAsync();

                // Suppression Vault
                _vaultService.DeleteKey(key.Id.ToString());

                LoadKeys();
            }
        }
    }
}