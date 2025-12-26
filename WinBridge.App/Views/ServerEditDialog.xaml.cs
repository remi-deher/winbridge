using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.Core.Data;

namespace WinBridge.App.Views
{
    public sealed partial class ServerEditDialog : ContentDialog
    {
        public ServerModel Result { get; private set; }

        public ServerEditDialog(ServerModel? existing = null)
        {
            this.InitializeComponent();
            LoadKeys();

            if (existing != null)
            {
                Result = existing;
                NameBox.Text = existing.Name;
                HostBox.Text = existing.Host;
                SshPortBox.Value = existing.SshPort > 0 ? existing.SshPort : 22;
                UserBox.Text = existing.Username;
                PassBox.Password = existing.Password;
                
                // Select OS Category
                foreach (ComboBoxItem item in OsCategoryCombo.Items)
                {
                    if (item.Tag?.ToString() == existing.OSFamily.ToString())
                    {
                        OsCategoryCombo.SelectedItem = item;
                        break;
                    }
                }

                // Win Settings
                foreach (ComboBoxItem item in PrimaryProtocolCombo.Items)
                {
                    if (item.Tag?.ToString() == existing.PrimaryProtocol.ToString())
                    {
                        PrimaryProtocolCombo.SelectedItem = item;
                        break;
                    }
                }
                
                DomainBox.Text = existing.Domain ?? "";
                FallbackCheck.IsChecked = existing.IsFallbackEnabled;
                WinRmPortBox.Value = existing.WinRmPort > 0 ? existing.WinRmPort : 5985;

                if (existing.SshKeyId.HasValue) KeyCombo.SelectedValue = existing.SshKeyId;
            }
            else
            {
                Result = new ServerModel();
                OsCategoryCombo.SelectedIndex = 0; // Default Linux
                PrimaryProtocolCombo.SelectedIndex = 0; // Default SSH
            }

            UpdateUiState();
            this.PrimaryButtonClick += ServerEditDialog_PrimaryButtonClick;
        }

        private void LoadKeys()
        {
            using var db = new AppDbContext();
            KeyCombo.ItemsSource = db.Keys.ToList();
            KeyCombo.SelectedValuePath = "Id";
        }

        private void OsCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiState();
        }

        private void PrimaryProtocolCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           UpdateFallbackText();
        }

        private void UpdateUiState()
        {
            if (OsCategoryCombo == null || WindowsSettingsPanel == null) return;

            if (OsCategoryCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Windows")
            {
                WindowsSettingsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                WindowsSettingsPanel.Visibility = Visibility.Collapsed;
            }
            UpdateFallbackText();
        }

        private void UpdateFallbackText()
        {
            if (PrimaryProtocolCombo == null || FallbackCheck == null) return;

            if (PrimaryProtocolCombo.SelectedItem is ComboBoxItem item)
            {
                var protocol = item.Tag?.ToString();
                if (protocol == "SSH")
                {
                    FallbackCheck.Content = "Utiliser WinRM si SSH échoue";
                }
                else if (protocol == "WinRM")
                {
                    FallbackCheck.Content = "Utiliser SSH si WinRM échoue";
                }
            }
        }

        private void ServerEditDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(HostBox.Text) || string.IsNullOrWhiteSpace(UserBox.Text))
            {
                args.Cancel = true;
                return;
            }

            // Update Model
            Result.Name = NameBox.Text;
            Result.Host = HostBox.Text;
            Result.SshPort = (int)SshPortBox.Value;
            Result.Username = UserBox.Text;
            Result.Password = PassBox.Password;
            
            if (OsCategoryCombo.SelectedItem is ComboBoxItem item && Enum.TryParse<OSCategory>(item.Tag?.ToString(), out var os))
            {
                Result.OSFamily = os;
            }

            // If Linux, force SSH Primary
            if (Result.OSFamily == OSCategory.Linux)
            {
                Result.PrimaryProtocol = RemoteProtocol.SSH;
                Result.IsFallbackEnabled = false;
                Result.Domain = null;
            }
            else
            {
                if (PrimaryProtocolCombo.SelectedItem is ComboBoxItem protoItem && Enum.TryParse<RemoteProtocol>(protoItem.Tag?.ToString(), out var proto))
                {
                    Result.PrimaryProtocol = proto;
                }
                Result.IsFallbackEnabled = FallbackCheck.IsChecked ?? false;
                Result.Domain = DomainBox.Text;
                Result.WinRmPort = (int)WinRmPortBox.Value;
            }

            if (KeyCombo.SelectedValue is Guid keyId)
            {
                Result.SshKeyId = keyId;
                Result.UsePrivateKey = true;
            }
            else
            {
                Result.SshKeyId = null;
                Result.UsePrivateKey = false;
            }
        }
    }
}
