using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using WinBridge.Core.Models;

namespace WinBridge.App.Views;

public sealed partial class CredentialsPage : Page
{
    public ObservableCollection<CredentialMetadata> Credentials { get; } = [];

    public CredentialsPage()
    {
        this.InitializeComponent();
        this.Loaded += CredentialsPage_Loaded;
    }

    private async void CredentialsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCredentialsAsync();
    }

    private async Task LoadCredentialsAsync()
    {
        Credentials.Clear();
        var list = await App.DataService.GetCredentialsAsync();
        foreach (var item in list)
        {
            Credentials.Add(item);
        }
    }

    private async void AddCredential_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new AddCredentialDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var cred = new CredentialMetadata
                {
                    DisplayName = dialog.CredentialName,
                    UserName = dialog.UserName,
                    Type = dialog.CredentialType,
                    OwnerModuleId = "System"
                };

                var insertResult = await App.DataService.AddCredentialAsync(cred);

                if (cred.Id == 0)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Erreur",
                        Content = "Impossible de gÃ©nÃ©rer l'ID du credential.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                try
                {
                    string vaultKey = $"Credential_{cred.Id}";
                    Services.VaultService.StoreSecret(vaultKey, cred.UserName, dialog.Secret);

                    if (!string.IsNullOrEmpty(dialog.SudoPassword))
                    {
                        string sudoKey = $"Credential_{cred.Id}_Sudo";
                        Services.VaultService.StoreSecret(sudoKey, cred.UserName, dialog.SudoPassword);
                    }
                }
                catch (Exception ex)
                {
                    
                    await App.DataService.DeleteCredentialAsync(cred);

                    var errorDialog = new ContentDialog
                    {
                        Title = "Erreur Vault",
                        Content = $"Impossible de sauvegarder dans le coffre-fort : {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                Credentials.Add(cred);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in AddCredential_Click: {ex}");
            var errorDialog = new ContentDialog
            {
                Title = "Erreur",
                Content = $"Une erreur s'est produite : {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    public async void EditCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CredentialMetadata cred)
        {
            try
            {
                
                var dialog = new AddCredentialDialog
                {
                    XamlRoot = this.XamlRoot
                };

                dialog.SetEditMode(cred);

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    
                    cred.DisplayName = dialog.CredentialName;
                    cred.UserName = dialog.UserName;

                    await App.DataService.UpdateCredentialAsync(cred);

                    if (dialog.SecretChanged)
                    {
                        string vaultKey = $"Credential_{cred.Id}";
                        Services.VaultService.StoreSecret(vaultKey, cred.UserName, dialog.Secret);
                    }

                    if (!string.IsNullOrEmpty(dialog.SudoPassword))
                    {
                        string sudoKey = $"Credential_{cred.Id}_Sudo";
                        Services.VaultService.StoreSecret(sudoKey, cred.UserName, dialog.SudoPassword);
                    }

                    int index = Credentials.IndexOf(cred);
                    if (index >= 0)
                    {
                        Credentials[index] = cred; 
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Erreur",
                    Content = $"Impossible de modifier l'identifiant : {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    public async void DeleteCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CredentialMetadata cred)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Confirmer la suppression",
                Content = $"Voulez-vous vraiment supprimer '{cred.DisplayName}' ?",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string vaultKey = $"Credential_{cred.Id}";
                Services.VaultService.Remove(vaultKey);

                string sudoKey = $"Credential_{cred.Id}_Sudo";
                Services.VaultService.Remove(sudoKey);

                await App.DataService.DeleteCredentialAsync(cred);
                Credentials.Remove(cred);
            }
        }
    }
}

public partial class CredentialTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is CredentialType type)
        {
            
            return type == CredentialType.SshKey ? "\uE72E" : "\uE125";
        }
        return "\uE125";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public partial class OwnerToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string ownerId)
        {
            return (string.IsNullOrEmpty(ownerId) || ownerId == "System") ? "Utilisateur" : ownerId;
        }
        return "Utilisateur"; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

