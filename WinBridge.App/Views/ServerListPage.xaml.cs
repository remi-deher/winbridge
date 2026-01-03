using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.App.ViewModels;
using WinBridge.Core.Models;

namespace WinBridge.App.Views;

public sealed partial class ServerListPage : Page
{
    public ServerListViewModel ViewModel { get; }

    public ServerListPage()
    {
        ViewModel = new ServerListViewModel();
        this.InitializeComponent();
    }

    private async void AddServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new AddServerDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var server = new Server
                {
                    Name = dialog.ServerName,
                    Host = dialog.ServerHost,
                    Port = dialog.ServerPort,
                    Protocol = dialog.SelectedProtocol,
                    Os = dialog.SelectedOs,
                    Group = dialog.ServerGroup,
                    EnableFallback = dialog.EnableFallback
                };

                if (dialog.UseBastion)
                {
                    server.UseBastion = true;
                    if (dialog.IsBastionManual)
                    {
                        server.BastionHost = dialog.BastionHost;
                        server.BastionPort = dialog.BastionPort;
                        server.BastionCredentialId = dialog.BastionCredentialId;
                    }
                    else
                    {
                        server.BastionServerId = dialog.BastionServerId;
                    }
                }

                if (dialog.SelectedCredentialId.HasValue)
                {
                    server.CredentialId = dialog.SelectedCredentialId.Value;
                }
                else if (dialog.IsManualAuth)
                {
                    var tempCred = new CredentialMetadata
                    {
                        DisplayName = $"{server.Name} - Compte manuel",
                        UserName = dialog.ManualUsername,
                        Type = CredentialType.Password,
                        OwnerModuleId = "System"
                    };

                    await App.DataService.AddCredentialAsync(tempCred);

                    if (tempCred.Id > 0)
                    {
                        try
                        {
                            string vaultKey = $"Credential_{tempCred.Id}";
                            Services.VaultService.StoreSecret(vaultKey, tempCred.UserName, dialog.ManualPassword);
                            server.CredentialId = tempCred.Id;
                        }
                        catch (Exception vaultEx)
                        {
                            await App.DataService.DeleteCredentialAsync(tempCred);

                            var errorDialog = new ContentDialog
                            {
                                Title = "Erreur Vault",
                                Content = $"Impossible de sauvegarder le mot de passe : {vaultEx.Message}",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            await errorDialog.ShowAsync();
                            return;
                        }
                    }
                }
                else if (dialog.IsNewCredential)
                {
                    var newCred = new CredentialMetadata
                    {
                        DisplayName = string.IsNullOrWhiteSpace(dialog.NewCredentialName)
                            ? $"{server.Name} - Compte"
                            : dialog.NewCredentialName,
                        UserName = dialog.NewUsername,
                        Type = CredentialType.Password,
                        OwnerModuleId = "System"
                    };

                    await App.DataService.AddCredentialAsync(newCred);

                    if (newCred.Id > 0)
                    {
                        try
                        {
                            string vaultKey = $"Credential_{newCred.Id}";
                            Services.VaultService.StoreSecret(vaultKey, newCred.UserName, dialog.NewPassword);
                            server.CredentialId = newCred.Id;
                        }
                        catch (Exception vaultEx)
                        {
                            await App.DataService.DeleteCredentialAsync(newCred);

                            var errorDialog = new ContentDialog
                            {
                                Title = "Erreur Vault",
                                Content = $"Impossible de sauvegarder le mot de passe : {vaultEx.Message}",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            await errorDialog.ShowAsync();
                            return;
                        }
                    }
                }

                await App.DataService.AddServerAsync(server);
                ViewModel.Servers.Add(server);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ServerListPage] Error in AddServer_Click: {ex}");

            var errorDialog = new ContentDialog
            {
                Title = "Erreur",
                Content = $"Impossible d'ajouter le serveur : {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async void OnEditServerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Server serverToEdit)
        {
            try
            {
                var dialog = new AddServerDialog
                {
                    XamlRoot = this.XamlRoot,
                    ServerToEdit = serverToEdit
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    
                    serverToEdit.Name = dialog.ServerName;
                    serverToEdit.Host = dialog.ServerHost;
                    serverToEdit.Port = dialog.ServerPort;
                    serverToEdit.Protocol = dialog.SelectedProtocol;
                    serverToEdit.Os = dialog.SelectedOs;
                    serverToEdit.Group = dialog.ServerGroup;
                    serverToEdit.EnableFallback = dialog.EnableFallback;

                    if (dialog.UseBastion)
                    {
                        serverToEdit.UseBastion = true;
                        if (dialog.IsBastionManual)
                        {
                            serverToEdit.BastionHost = dialog.BastionHost;
                            serverToEdit.BastionPort = dialog.BastionPort;
                            serverToEdit.BastionCredentialId = dialog.BastionCredentialId;
                            serverToEdit.BastionServerId = null;
                        }
                        else
                        {
                            serverToEdit.BastionServerId = dialog.BastionServerId;
                            
                            serverToEdit.BastionHost = null;
                            serverToEdit.BastionCredentialId = null;
                            serverToEdit.BastionPort = 0;
                        }
                    }
                    else
                    {
                        serverToEdit.UseBastion = false;
                        serverToEdit.BastionServerId = null;
                        serverToEdit.BastionHost = null;
                        serverToEdit.BastionCredentialId = null;
                        serverToEdit.BastionPort = 0;
                    }

                    if (dialog.SelectedCredentialId.HasValue)
                    {
                        serverToEdit.CredentialId = dialog.SelectedCredentialId.Value;
                    }
                    else if (dialog.IsManualAuth)
                    {
                        var tempCred = new CredentialMetadata
                        {
                            DisplayName = $"{serverToEdit.Name} - Compte manuel",
                            UserName = dialog.ManualUsername,
                            Type = CredentialType.Password,
                            OwnerModuleId = "System"
                        };

                        await App.DataService.AddCredentialAsync(tempCred);

                        if (tempCred.Id > 0)
                        {
                            try
                            {
                                string vaultKey = $"Credential_{tempCred.Id}";
                                Services.VaultService.StoreSecret(vaultKey, tempCred.UserName, dialog.ManualPassword);
                                serverToEdit.CredentialId = tempCred.Id;
                            }
                            catch (Exception vaultEx)
                            {
                                await App.DataService.DeleteCredentialAsync(tempCred);

                                var errorDialog = new ContentDialog
                                {
                                    Title = "Erreur Vault",
                                    Content = $"Impossible de sauvegarder le mot de passe : {vaultEx.Message}",
                                    CloseButtonText = "OK",
                                    XamlRoot = this.XamlRoot
                                };
                                await errorDialog.ShowAsync();
                                return;
                            }
                        }
                    }
                    else if (dialog.IsNewCredential)
                    {
                        var newCred = new CredentialMetadata
                        {
                            DisplayName = string.IsNullOrWhiteSpace(dialog.NewCredentialName)
                                ? $"{serverToEdit.Name} - Compte"
                                : dialog.NewCredentialName,
                            UserName = dialog.NewUsername,
                            Type = CredentialType.Password,
                            OwnerModuleId = "System"
                        };

                        await App.DataService.AddCredentialAsync(newCred);

                        if (newCred.Id > 0)
                        {
                            try
                            {
                                string vaultKey = $"Credential_{newCred.Id}";
                                Services.VaultService.StoreSecret(vaultKey, newCred.UserName, dialog.NewPassword);
                                serverToEdit.CredentialId = newCred.Id;
                            }
                            catch (Exception vaultEx)
                            {
                                await App.DataService.DeleteCredentialAsync(newCred);

                                var errorDialog = new ContentDialog
                                {
                                    Title = "Erreur Vault",
                                    Content = $"Impossible de sauvegarder le mot de passe : {vaultEx.Message}",
                                    CloseButtonText = "OK",
                                    XamlRoot = this.XamlRoot
                                };
                                await errorDialog.ShowAsync();
                                return;
                            }
                        }
                    }

                    try
                    {
                        await App.DataService.UpdateServerAsync(serverToEdit);

                        var index = ViewModel.Servers.IndexOf(serverToEdit);
                        if (index >= 0)
                        {
                            ViewModel.Servers[index] = serverToEdit;
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Erreur de sauvegarde",
                            Content = $"Impossible de mettre Ã  jour le serveur : {ex.Message}",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServerListPage] Error in OnEditServerClicked: {ex}");

                var errorDialog = new ContentDialog
                {
                    Title = "Erreur",
                    Content = $"Impossible de modifier le serveur : {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    private void OnManageServerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Server server)
        {
            AppShellPage.Current.OpenTab(server.Name ?? "Serveur", typeof(ServerDetailsPage), server, "\uE7F4");
        }
    }
}

