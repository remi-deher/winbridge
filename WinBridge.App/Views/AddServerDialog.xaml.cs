using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.Core.Models;

namespace WinBridge.App.Views;

public sealed partial class AddServerDialog : ContentDialog
{
    private List<CredentialMetadata> _availableCredentials = [];
    private List<Server> _availableServers = [];
    private List<string> _existingGroups = [];
    private bool _isLoaded = false;

    public string ServerName => NameBox?.Text?.Trim() ?? string.Empty;
    public string ServerGroup => GroupBox?.Text?.Trim() ?? string.Empty;
    public string ServerHost => HostBox?.Text?.Trim() ?? string.Empty;
    public int ServerPort => (int)(PortBox?.Value ?? 22);
    public OsType SelectedOs => OsLinux?.IsChecked == true ? OsType.Linux :
                                OsWindows?.IsChecked == true ? OsType.Windows : OsType.Other;
    public ServerProtocol SelectedProtocol
    {
        get
        {
            if (ProtocolComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag switch
                {
                    "SSH" => ServerProtocol.SSH,
                    "WinRM" => ServerProtocol.WinRM,
                    "Telnet" => ServerProtocol.Telnet,
                    _ => ServerProtocol.SSH
                };
            }
            return ServerProtocol.SSH;
        }
    }
    public bool EnableFallback => FallbackCheckBox?.IsChecked == true;

    public int? SelectedCredentialId => CredentialComboBox?.SelectedItem is CredentialMetadata cred ? cred.Id : null;

    public bool IsManualAuth => AuthManual?.IsChecked == true;
    public string ManualUsername => ManualUsernameBox?.Text?.Trim() ?? string.Empty;
    public string ManualPassword => ManualPasswordBox?.Password ?? string.Empty;

    public bool IsNewCredential => AuthNew?.IsChecked == true;
    public string NewCredentialName => NewCredentialNameBox?.Text?.Trim() ?? string.Empty;
    public string NewUsername => NewUsernameBox?.Text?.Trim() ?? string.Empty;
    public string NewPassword => NewPasswordBox?.Password ?? string.Empty;

    public bool UseBastion => UseBastionToggle?.IsOn == true;
    public int? BastionServerId => BastionServerComboBox?.SelectedItem is Server s ? s.Id : null;
    public string BastionHost => BastionHostBox?.Text?.Trim() ?? string.Empty;
    public int BastionPort => (int)(BastionPortBox?.Value ?? 22);
    public int? BastionCredentialId => BastionCredentialComboBox?.SelectedItem is CredentialMetadata c ? c.Id : null;
    public bool IsBastionManual => BastionModeManual?.IsChecked == true;

    public AddServerDialog()
    {
        this.InitializeComponent();
        this.Loaded += AddServerDialog_Loaded;
    }

    private async void AddServerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadDataAsync();
            AttachEventHandlers();
            _isLoaded = true;
            ValidateForm();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddServerDialog] Error in Loaded: {ex}");
        }
    }

    private void AttachEventHandlers()
    {
        if (NameBox != null) NameBox.TextChanged += (s, e) => ValidateForm();
        if (HostBox != null) HostBox.TextChanged += (s, e) => ValidateForm();
        if (ManualUsernameBox != null) ManualUsernameBox.TextChanged += (s, e) => ValidateForm();
        if (NewUsernameBox != null) NewUsernameBox.TextChanged += (s, e) => ValidateForm();
        if (CredentialComboBox != null) CredentialComboBox.SelectionChanged += (s, e) => ValidateForm();
        if (GroupBox != null) GroupBox.TextChanged += GroupBox_TextChanged;

        if (BastionHostBox != null) BastionHostBox.TextChanged += (s, e) => ValidateForm();
        if (BastionServerComboBox != null) BastionServerComboBox.SelectionChanged += (s, e) => ValidateForm();
        if (BastionCredentialComboBox != null) BastionCredentialComboBox.SelectionChanged += (s, e) => ValidateForm();
    }

    public Server? ServerToEdit { get; set; }

    private async Task LoadDataAsync()
    {
        try
        {
            if (App.DataService == null)
            {
                System.Diagnostics.Debug.WriteLine("[AddServerDialog] DataService is null");
                return;
            }

            _availableCredentials = await App.DataService.GetCredentialsAsync();

            CredentialComboBox?.ItemsSource = _availableCredentials;
            BastionCredentialComboBox?.ItemsSource = _availableCredentials;

            var allServers = await App.DataService.GetServersAsync();

            if (ServerToEdit != null)
            {
                _availableServers = [.. allServers.Where(s => s.Id != ServerToEdit.Id)];
            }
            else
            {
                _availableServers = allServers;
            }

            BastionServerComboBox?.ItemsSource = _availableServers;

            _existingGroups = await App.DataService.GetUniqueGroupsAsync();

            if (ServerToEdit != null)
            {
                PopulateFields(ServerToEdit);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddServerDialog] Error loading data: {ex}");
        }
    }

    private void PopulateFields(Server server)
    {
        this.Title = "Modifier le serveur";
        this.PrimaryButtonText = "Mettre Ã  jour";

        NameBox?.Text = server.Name;
        HostBox?.Text = server.Host;
        PortBox?.Value = server.Port;
        GroupBox?.Text = server.Group ?? string.Empty;

        if (server.Os == OsType.Windows) OsWindows.IsChecked = true;
        else if (server.Os == OsType.Linux) OsLinux.IsChecked = true;
        else OsOther.IsChecked = true;

        UpdateProtocolOptions();

        if (ProtocolComboBox != null)
        {
            
            string targetTag = server.Protocol.ToString();
            foreach (ComboBoxItem item in ProtocolComboBox.Items.Cast<ComboBoxItem>())
            {
                if ((string)item.Tag == targetTag)
                {
                    ProtocolComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        FallbackCheckBox?.IsChecked = server.EnableFallback;

        if (server.CredentialId.HasValue)
        {
            AuthExisting.IsChecked = true;
            CredentialComboBox?.SelectedItem = _availableCredentials.FirstOrDefault(c => c.Id == server.CredentialId.Value);
        }
        else
        {
            
            AuthManual.IsChecked = true;
        }

        if (server.UseBastion)
        {
            UseBastionToggle.IsOn = true;

            if (server.BastionServerId.HasValue)
            {
                BastionModeExisting.IsChecked = true;
                BastionServerComboBox?.SelectedItem = _availableServers.FirstOrDefault(s => s.Id == server.BastionServerId.Value);
            }
            else
            {
                BastionModeManual.IsChecked = true;
                BastionHostBox?.Text = server.BastionHost;
                BastionPortBox?.Value = server.BastionPort;
                if (server.BastionCredentialId.HasValue && BastionCredentialComboBox != null)
                {
                    BastionCredentialComboBox.SelectedItem = _availableCredentials.FirstOrDefault(c => c.Id == server.BastionCredentialId.Value);
                }
            }
        }
        else
        {
            UseBastionToggle.IsOn = false;
        }

        ValidateForm();
    }

    private void Os_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        UpdateProtocolOptions();
        UpdateFallbackVisibility();
    }

    private void UpdateProtocolOptions()
    {
        if (ProtocolComboBox == null) return;

        ProtocolComboBox.Items.Clear();

        if (OsWindows?.IsChecked == true)
        {
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "WinRM", Content = "WinRM (RecommandÃ©)" });
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "SSH", Content = "SSH" });
            ProtocolComboBox.SelectedIndex = 0;
        }
        else if (OsLinux?.IsChecked == true)
        {
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "SSH", Content = "SSH (RecommandÃ©)" });
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "Telnet", Content = "Telnet" });
            ProtocolComboBox.SelectedIndex = 0;
        }
        else
        {
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "SSH", Content = "SSH" });
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "WinRM", Content = "WinRM" });
            ProtocolComboBox.Items.Add(new ComboBoxItem { Tag = "Telnet", Content = "Telnet" });
            ProtocolComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateFallbackVisibility()
    {
        if (FallbackCheckBox == null) return;

        FallbackCheckBox.Visibility = (OsWindows?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || PortBox == null || ProtocolComboBox?.SelectedItem == null) return;

        if (ProtocolComboBox.SelectedItem is ComboBoxItem item && item.Tag is string protocol)
        {
            PortBox.Value = protocol switch
            {
                "SSH" => 22,
                "WinRM" => 5985,
                "Telnet" => 23,
                _ => 22
            };
        }
    }

    private void AuthMode_Changed(object sender, RoutedEventArgs e)
    {
        if (ExistingCredentialPanel == null) return;

        ExistingCredentialPanel.Visibility = (AuthExisting?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        ManualCredentialPanel.Visibility = (AuthManual?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        NewCredentialPanel.Visibility = (AuthNew?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        if (_isLoaded) ValidateForm();
    }

    private void GroupBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (!_isLoaded || sender == null || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        var query = sender.Text?.ToLower() ?? string.Empty;
        var suggestions = _existingGroups.Where(g => g.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        sender.ItemsSource = suggestions;
    }

    private void UseBastionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (BastionConfigPanel == null) return;
        BastionConfigPanel.Visibility = (UseBastionToggle?.IsOn == true) ? Visibility.Visible : Visibility.Collapsed;
        if (_isLoaded) ValidateForm();
    }

    private void BastionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (BastionExistingPanel == null) return;
        BastionExistingPanel.Visibility = (BastionModeExisting?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        BastionManualPanel.Visibility = (BastionModeManual?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        if (_isLoaded) ValidateForm();
    }

    private void ValidateForm()
    {
        if (!_isLoaded) return;

        bool isValid = true;

        if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(ServerHost))
        {
            isValid = false;
        }

        if (AuthExisting?.IsChecked == true && SelectedCredentialId == null) isValid = false;
        else if (IsManualAuth && string.IsNullOrWhiteSpace(ManualUsername)) isValid = false;
        else if (IsNewCredential && string.IsNullOrWhiteSpace(NewUsername)) isValid = false;

        if (UseBastion)
        {
            if (IsBastionManual)
            {
                if (string.IsNullOrWhiteSpace(BastionHost) || BastionCredentialId == null) isValid = false;
            }
            else
            {
                if (BastionServerId == null) isValid = false;
            }
        }

        this.IsPrimaryButtonEnabled = isValid;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ErrorMessage?.IsOpen = false;

        if (string.IsNullOrWhiteSpace(ServerName))
        {
            ShowError("Le nom du serveur est requis.");
            args.Cancel = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerHost))
        {
            ShowError("L'hÃ´te est requis.");
            args.Cancel = true;
            return;
        }

        if (AuthExisting?.IsChecked == true && SelectedCredentialId == null)
        {
            ShowError("Veuillez sÃ©lectionner un identifiant.");
            args.Cancel = true;
            return;
        }

        if (IsManualAuth && string.IsNullOrWhiteSpace(ManualUsername))
        {
            ShowError("Le nom d'utilisateur est requis.");
            args.Cancel = true;
            return;
        }

        if (IsNewCredential && string.IsNullOrWhiteSpace(NewUsername))
        {
            ShowError("Le nom d'utilisateur est requis pour le nouvel identifiant.");
            args.Cancel = true;
            return;
        }

        if (UseBastion)
        {
            if (IsBastionManual)
            {
                if (string.IsNullOrWhiteSpace(BastionHost))
                {
                    ShowError("L'hÃ´te du bastion est requis.");
                    args.Cancel = true;
                    return;
                }
                if (BastionCredentialId == null)
                {
                    ShowError("L'identifiant du bastion est requis.");
                    args.Cancel = true;
                    return;
                }
            }
            else if (BastionServerId == null)
            {
                ShowError("Veuillez sÃ©lectionner un serveur bastion.");
                args.Cancel = true;
                return;
            }
        }
    }

    private void ShowError(string message)
    {
        if (ErrorMessage != null)
        {
            ErrorMessage.Message = message;
            ErrorMessage.IsOpen = true;
        }
    }
}

