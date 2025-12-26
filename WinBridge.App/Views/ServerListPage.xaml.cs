using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinBridge.Core.Data;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class ServerListPage : Page
{
    // Référence locale pour pouvoir rafraîchir la liste des clés dynamiquement
    private ComboBox? _cmbKeysRef;

    public ServerListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadServers();
    }

    private void LoadServers()
    {
        using var db = new AppDbContext();
        var list = db.Servers.ToList();
        ServerGrid.ItemsSource = list;

        // Affiche le message "Vide" s'il n'y a pas de serveurs
        TxtEmpty.Visibility = list.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServerModel server)
        {
            this.Frame.Navigate(typeof(TerminalPage), server);
        }
    }

    // C'est ici que toute la magie de l'UI se passe
    private async void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 12, MinWidth = 350 };

        // Champs classiques
        var txtName = new TextBox { Header = "Nom du serveur (ex: Prod)" };
        var txtHost = new TextBox { Header = "IP ou Domaine" };
        var numPort = new NumberBox { Header = "Port SSH", Value = 22, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var txtUser = new TextBox { Header = "Utilisateur", Text = "root" };

        // --- SECTION AUTHENTIFICATION ---
        var lblAuth = new TextBlock { Text = "Méthode d'authentification", Margin = new Thickness(0, 10, 0, 0), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };

        // Les 3 options
        var radioPassword = new RadioButton { Content = "Mot de passe", IsChecked = true, GroupName = "Auth" };
        var radioKey = new RadioButton { Content = "Clé Privée (Vault)", GroupName = "Auth" };
        var radioAgent = new RadioButton { Content = "Agent SSH (1Password / OpenSSH)", GroupName = "Auth" }; // <--- NOUVEAU

        // Les contrôles conditionnels
        var txtPassword = new PasswordBox { Header = "Mot de passe" };

        _cmbKeysRef = new ComboBox
        {
            Header = "Sélectionner une clé stockée",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed, // Masqué par défaut
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id"
        };

        // Chargement des clés pour la liste déroulante
        RefreshKeyList();

        // Gestionnaire pour l'option "+ Ajouter une clé"
        _cmbKeysRef.SelectionChanged += async (s, args) =>
        {
            if (_cmbKeysRef.SelectedItem is SshKeyModel selected && selected.Id == Guid.Empty)
            {
                _cmbKeysRef.SelectedItem = null; // Reset
                await ShowAddKeyDialog();
            }
        };

        // --- LOGIQUE D'AFFICHAGE DYNAMIQUE ---

        // 1. Si Mot de passe : On affiche le champ Password, on cache les clés
        radioPassword.Checked += (s, a) => {
            txtPassword.Visibility = Visibility.Visible;
            _cmbKeysRef.Visibility = Visibility.Collapsed;
        };

        // 2. Si Clé Vault : On cache le Password, on affiche la liste des clés
        radioKey.Checked += (s, a) => {
            txtPassword.Visibility = Visibility.Collapsed;
            _cmbKeysRef.Visibility = Visibility.Visible;
        };

        // 3. Si Agent SSH : On cache TOUT (pas besoin de mdp ni de clé fichier)
        radioAgent.Checked += (s, a) => {
            txtPassword.Visibility = Visibility.Collapsed;
            _cmbKeysRef.Visibility = Visibility.Collapsed;
        };

        // Ajout à la vue
        stack.Children.Add(txtName);
        stack.Children.Add(txtHost);
        stack.Children.Add(numPort);
        stack.Children.Add(txtUser);
        stack.Children.Add(lblAuth);
        stack.Children.Add(radioPassword);
        stack.Children.Add(radioKey);
        stack.Children.Add(radioAgent); // <--- Ajout du bouton radio
        stack.Children.Add(txtPassword);
        stack.Children.Add(_cmbKeysRef);

        var dialog = new ContentDialog
        {
            Title = "Nouveau Serveur",
            PrimaryButtonText = "Ajouter",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        // --- SAUVEGARDE ---
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtHost.Text)) return;

            var newServer = new ServerModel
            {
                Name = txtName.Text,
                Host = txtHost.Text,
                Port = (int)numPort.Value,
                Username = txtUser.Text,

                // MAPPING DES NOUVELLES OPTIONS
                UseSshAgent = radioAgent.IsChecked == true,
                UsePrivateKey = radioKey.IsChecked == true
            };

            // Logique spécifique selon le choix
            if (newServer.UsePrivateKey)
            {
                // Si Clé Vault, il faut qu'une clé soit sélectionnée
                if (_cmbKeysRef.SelectedValue is Guid keyId && keyId != Guid.Empty)
                    newServer.SshKeyId = keyId;
                else
                    return; // Erreur silencieuse ou afficher message
            }
            else if (newServer.UseSshAgent)
            {
                // Rien à faire de spécial, le booléen suffit
                newServer.Password = null;
                newServer.SshKeyId = null;
            }
            else
            {
                // Mode Mot de passe par défaut
                newServer.Password = txtPassword.Password;
            }

            using var db = new AppDbContext();
            db.Servers.Add(newServer);
            await db.SaveChangesAsync();

            LoadServers();
        }
    }

    // --- (Le reste ne change pas : RefreshKeyList, ShowAddKeyDialog) ---

    private void RefreshKeyList(Guid? selectedId = null)
    {
        if (_cmbKeysRef == null) return;

        using var db = new AppDbContext();
        var keys = db.Keys.OrderBy(k => k.Name).ToList();

        // Option factice pour l'ajout rapide
        keys.Insert(0, new SshKeyModel { Id = Guid.Empty, Name = "+ Ajouter une nouvelle clé..." });

        _cmbKeysRef.ItemsSource = keys;

        if (selectedId.HasValue)
            _cmbKeysRef.SelectedValue = selectedId;
        else if (keys.Count > 1)
            _cmbKeysRef.SelectedIndex = 1;
    }

    private async Task ShowAddKeyDialog()
    {
        var stack = new StackPanel { Spacing = 12, MinWidth = 400 };
        var txtName = new TextBox { Header = "Nom de la clé (ex: Clé Github)" };
        var txtUser = new TextBox { Header = "User par défaut", Text = "root" };
        var txtContent = new TextBox { Header = "Contenu Privé (PEM)", AcceptsReturn = true, Height = 150, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
        var txtPass = new PasswordBox { Header = "Passphrase (Optionnel)" };

        stack.Children.Add(txtName);
        stack.Children.Add(txtUser);
        stack.Children.Add(txtContent);
        stack.Children.Add(txtPass);

        var dialog = new ContentDialog
        {
            Title = "Ajout rapide de clé SSH",
            PrimaryButtonText = "Sauvegarder",
            CloseButtonText = "Annuler",
            Content = stack,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtContent.Text)) return;

            try
            {
                var newKey = new SshKeyModel
                {
                    Name = txtName.Text,
                    DefaultUsername = txtUser.Text,
                    HasPassphrase = !string.IsNullOrEmpty(txtPass.Password)
                };

                using var db = new AppDbContext();
                db.Keys.Add(newKey);
                await db.SaveChangesAsync();

                var vault = new VaultService();
                vault.SaveKeyContent(newKey.Id.ToString(), txtContent.Text, txtPass.Password);

                RefreshKeyList(newKey.Id);
            }
            catch (Exception ex)
            {
                // Gérer erreur
            }
        }
        else
        {
            _cmbKeysRef.SelectedIndex = _cmbKeysRef.Items.Count > 1 ? 1 : -1;
        }
    }
}