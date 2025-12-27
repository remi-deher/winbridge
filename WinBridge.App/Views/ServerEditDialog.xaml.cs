using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.Core.Data;
using System.Collections.Generic;

namespace WinBridge.App.Views
{
    public sealed partial class ServerEditDialog : ContentDialog
    {
        public ServerModel Result { get; private set; }

        public ServerEditDialog(ServerModel? existing = null)
        {
            this.InitializeComponent();
            LoadKeys();
            LoadGroups();

            if (existing != null)
            {
                Result = existing;
                NameBox.Text = existing.Name;
                HostBox.Text = existing.Host;
                SshPortBox.Value = existing.SshPort > 0 ? existing.SshPort : 22;
                UserBox.Text = existing.Username;
                PassBox.Password = existing.Password;
                TagsBox.Text = existing.Tags;
                
                if (existing.ServerGroupId.HasValue)
                {
                    GroupCombo.SelectedValue = existing.ServerGroupId;
                }

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

                UseAgentSwitch.IsOn = existing.UseSshAgent;
                AgentPipeBox.Text = existing.SshAgentPipePath ?? "";
                if (existing.SshKeyId.HasValue) KeyCombo.SelectedValue = existing.SshKeyId;
            }
            else
            {
                Result = new ServerModel();
                OsCategoryCombo.SelectedIndex = 0; // Default Linux
                PrimaryProtocolCombo.SelectedIndex = 0; // Default SSH
            }

            UpdateUiState();
            UpdateAgentUi();
            this.PrimaryButtonClick += ServerEditDialog_PrimaryButtonClick;
        }

        private void LoadKeys()
        {
            using var db = new AppDbContext();
            KeyCombo.ItemsSource = db.Keys.ToList();
            KeyCombo.SelectedValuePath = "Id";
        }

        private void LoadGroups()
        {
            using var db = new AppDbContext();
            GroupCombo.ItemsSource = db.ServerGroups.ToList();
            GroupCombo.SelectedValuePath = "Id";
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

        private void UseAgentSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateAgentUi();
        }

        private void UpdateAgentUi()
        {
             if (UseAgentSwitch == null || AgentPipeBox == null || KeyCombo == null) return;
        
             bool isAgent = UseAgentSwitch.IsOn;
             AgentPipeBox.Visibility = isAgent ? Visibility.Visible : Visibility.Collapsed;
             KeyCombo.IsEnabled = !isAgent;
        }

        private async void BtnConfirmAddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (NewGroupBox == null || string.IsNullOrWhiteSpace(NewGroupBox.Text)) return;

            string groupName = NewGroupBox.Text.Trim();

            try
            {
                using var db = new AppDbContext();
                var newGroup = new ServerGroup { Name = groupName };
                db.ServerGroups.Add(newGroup);
                await db.SaveChangesAsync();

                LoadGroups();
                GroupCombo.SelectedValue = newGroup.Id;
                
                NewGroupBox.Text = "";
                NewGroupFlyout?.Hide();
            }
            catch (Exception ex)
            {
                 // Log or showing error might be tricky inside a Dialog without causing issues, 
                 // but we can set the text to error or ignore.
                 // For now, let's just proceed.
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
            Result.Tags = TagsBox.Text ?? "";
            Result.ServerGroupId = (Guid?)GroupCombo.SelectedValue;
            
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

            Result.UseSshAgent = UseAgentSwitch.IsOn;
            Result.SshAgentPipePath = UseAgentSwitch.IsOn ? AgentPipeBox.Text : null;

            if (!Result.UseSshAgent && KeyCombo.SelectedValue is Guid keyId)
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
