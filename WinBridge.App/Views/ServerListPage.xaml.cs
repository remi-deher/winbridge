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
    private ComboBox? _cmbKeysRef;

    // Liste complète en mémoire pour le filtrage rapide
    private List<ServerModel> _allServers = new();

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
        // On charge tout dans la liste mémoire, trié par nom
        _allServers = db.Servers.OrderBy(s => s.Name).ToList();

        // On applique les filtres actuels
        ApplyFilters();
    }

    // --- LOGIQUE DE FILTRAGE ---

    private void OnFilterChanged(object sender, object e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        // ---------------------------------------------------------
        // CORRECTION DU PLANTAGE (NullReferenceException)
        // On vérifie que tous les contrôles XAML sont bien chargés
        // avant d'essayer de lire leurs propriétés.
        // ---------------------------------------------------------
        if (TxtSearch == null || CmbOsFilter == null || ServerGrid == null || _allServers == null)
            return;

        // 1. On part de la liste complète en mémoire
        IEnumerable<ServerModel> query = _allServers;

        // 2. Filtre Texte (Recherche dans Nom ou Host/IP)
        var searchText = TxtSearch.Text?.Trim().ToLower();
        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(s =>
                (s.Name != null && s.Name.ToLower().Contains(searchText)) ||
                (s.Host != null && s.Host.ToLower().Contains(searchText))
            );
        }

        // 3. Filtre OS (Basé sur la ComboBox)
        if (CmbOsFilter.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "All")
        {
            if (tag == "Windows")
            {
                query = query.Where(s => s.CachedOsInfo != null && s.CachedOsInfo.Contains("Windows", StringComparison.OrdinalIgnoreCase));
            }
            else if (tag == "Linux")
            {
                query = query.Where(s => s.CachedOsInfo == null || !s.CachedOsInfo.Contains("Windows", StringComparison.OrdinalIgnoreCase));
            }
            else if (tag == "CentOS")
            {
                query = query.Where(s => s.CachedOsInfo != null &&
                    (s.CachedOsInfo.Contains("CentOS", StringComparison.OrdinalIgnoreCase) ||
                     s.CachedOsInfo.Contains("Alma", StringComparison.OrdinalIgnoreCase) ||
                     s.CachedOsInfo.Contains("Rocky", StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                // Debian, Ubuntu, Arch...
                query = query.Where(s => s.CachedOsInfo != null && s.CachedOsInfo.Contains(tag, StringComparison.OrdinalIgnoreCase));
            }
        }

        // 4. Mise à jour de l'interface
        var filteredList = query.ToList();
        ServerGrid.ItemsSource = filteredList;

        if (TxtEmpty != null)
        {
            TxtEmpty.Visibility = filteredList.Any() ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpty.Text = filteredList.Any() ? "" : "Aucun serveur ne correspond à votre recherche.";
        }
    }

    // --- ACTIONS BOUTONS ---

    private void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowServerDialog(null);
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServerModel server)
        {
            _ = ShowServerDialog(server);
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServerModel server)
        {
            var dialog = new ContentDialog
            {
                Title = "Supprimer le serveur ?",
                Content = $"Voulez-vous vraiment supprimer \"{server.Name}\" ?\nCette action est irréversible.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                using var db = new AppDbContext();
                db.Servers.Remove(server);
                await db.SaveChangesAsync();
                LoadServers(); // Recharger la liste
            }
        }
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ServerModel server)
        {
            this.Frame.Navigate(typeof(ServerDashboardPage), server);
        }
    }

    // --- LOGIQUE DIALOGUE (AJOUT / EDIT) ---

    private async Task ShowServerDialog(ServerModel? serverToEdit)
    {
        bool isEdit = serverToEdit != null;
        var stack = new StackPanel { Spacing = 12, MinWidth = 350 };

        var txtName = new TextBox { Header = "Nom du serveur", Text = serverToEdit?.Name ?? "" };
        var txtHost = new TextBox { Header = "IP ou Domaine", Text = serverToEdit?.Host ?? "" };
        var numPort = new NumberBox { Header = "Port SSH", Value = serverToEdit?.Port ?? 22, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var txtUser = new TextBox { Header = "Utilisateur", Text = serverToEdit?.Username ?? "root" };

        var lblAuth = new TextBlock { Text = "Méthode d'authentification", Margin = new Thickness(0, 10, 0, 0), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var radioPassword = new RadioButton { Content = "Mot de passe", GroupName = "Auth" };
        var radioKey = new RadioButton { Content = "Clé Privée (Vault)", GroupName = "Auth" };
        var radioAgent = new RadioButton { Content = "Agent SSH (1Password / OpenSSH)", GroupName = "Auth" };
        var txtPassword = new PasswordBox { Header = "Mot de passe" };

        _cmbKeysRef = new ComboBox
        {
            Header = "Sélectionner une clé",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id"
        };

        // Configuration initiale (Edit vs New)
        if (isEdit)
        {
            if (serverToEdit!.UseSshAgent) radioAgent.IsChecked = true;
            else if (serverToEdit.UsePrivateKey) radioKey.IsChecked = true;
            else radioPassword.IsChecked = true;
            txtPassword.Password = serverToEdit.Password ?? "";
        }
        else
        {
            radioPassword.IsChecked = true;
        }

        void UpdateVisibility()
        {
            txtPassword.Visibility = radioPassword.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            _cmbKeysRef.Visibility = radioKey.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        radioPassword.Checked += (s, a) => UpdateVisibility();
        radioKey.Checked += (s, a) => UpdateVisibility();
        radioAgent.Checked += (s, a) => UpdateVisibility();
        UpdateVisibility();

        // Chargement des clés
        RefreshKeyList(serverToEdit?.SshKeyId);

        stack.Children.Add(txtName);
        stack.Children.Add(txtHost);
        stack.Children.Add(numPort);
        stack.Children.Add(txtUser);
        stack.Children.Add(lblAuth);
        stack.Children.Add(radioPassword);
        stack.Children.Add(radioKey);
        stack.Children.Add(radioAgent);
        stack.Children.Add(txtPassword);
        stack.Children.Add(_cmbKeysRef);

        bool wantsToAddKey = false;

        // Gestionnaire pour détecter "Ajouter une clé"
        _cmbKeysRef.SelectionChanged += (s, args) =>
        {
            if (_cmbKeysRef.SelectedItem is SshKeyModel selected && selected.Id == Guid.Empty)
            {
                _cmbKeysRef.SelectedItem = null;
                wantsToAddKey = true;
                // On cache le dialogue parent pour éviter le conflit WinUI "Double Dialog"
                if (stack.Parent is ContentDialog parentDialog) parentDialog.Hide();
            }
        };

        var dialog = new ContentDialog
        {
            Title = isEdit ? "Modifier le serveur" : "Nouveau Serveur",
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
            XamlRoot = this.XamlRoot
        };

        // Boucle pour gérer l'interruption "Ajouter une clé"
        while (true)
        {
            var result = await dialog.ShowAsync();

            if (wantsToAddKey)
            {
                wantsToAddKey = false;
                await ShowAddKeyDialog(); // Ouvrir le dialogue de clé
                RefreshKeyList();         // Rafraîchir la liste
                if (_cmbKeysRef.Items.Count > 1) _cmbKeysRef.SelectedIndex = _cmbKeysRef.Items.Count - 1;
                continue; // On réaffiche le dialogue serveur
            }

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtHost.Text)) break;

                using var db = new AppDbContext();
                ServerModel server;

                if (isEdit)
                    server = db.Servers.FirstOrDefault(s => s.Id == serverToEdit!.Id) ?? new ServerModel();
                else
                {
                    server = new ServerModel();
                    db.Servers.Add(server);
                }

                server.Name = txtName.Text;
                server.Host = txtHost.Text;
                server.Port = (int)numPort.Value;
                server.Username = txtUser.Text;
                server.UseSshAgent = radioAgent.IsChecked == true;
                server.UsePrivateKey = radioKey.IsChecked == true;
                server.Password = (!server.UseSshAgent && !server.UsePrivateKey) ? txtPassword.Password : null;
                server.SshKeyId = server.UsePrivateKey && _cmbKeysRef.SelectedValue is Guid kId ? kId : null;

                await db.SaveChangesAsync();
                LoadServers(); // Rafraîchir la liste principale
                break;
            }
            else break; // Annuler
        }
    }

    private void RefreshKeyList(Guid? selectedId = null)
    {
        if (_cmbKeysRef == null) return;
        using var db = new AppDbContext();
        var keys = db.Keys.OrderBy(k => k.Name).ToList();
        keys.Insert(0, new SshKeyModel { Id = Guid.Empty, Name = "+ Ajouter une nouvelle clé..." });
        _cmbKeysRef.ItemsSource = keys;

        if (selectedId.HasValue) _cmbKeysRef.SelectedValue = selectedId;
        else if (keys.Count > 1 && _cmbKeysRef.SelectedIndex == -1) _cmbKeysRef.SelectedIndex = 1;
    }

    private async Task ShowAddKeyDialog()
    {
        var stack = new StackPanel { Spacing = 12, MinWidth = 400 };
        var txtName = new TextBox { Header = "Nom de la clé" };
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
            }
            catch { }
        }
    }
}