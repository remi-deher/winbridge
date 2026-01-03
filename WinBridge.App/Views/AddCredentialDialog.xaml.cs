using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.Core.Models;
using WinRT.Interop;

namespace WinBridge.App.Views;

public sealed partial class AddCredentialDialog : ContentDialog
{
    private bool _isEditMode = false;

    public string CredentialName => NameBox.Text.Trim();
    public string UserName => UserBox.Text.Trim();
    public CredentialType CredentialType => (CredentialType)Enum.Parse(typeof(CredentialType), (string)((ComboBoxItem)TypeComboBox.SelectedItem).Tag);

    public string Secret
    {
        get
        {
            if (CredentialType == CredentialType.Password)
            {
                return PasswordBox.Password;
            }
            else
            {
                return SshKeyBox.Text.Replace("\r\n", "\n").Trim(); 
            }
        }
    }

    public string SudoPassword => SudoPasswordBox.Password;

    public bool SecretChanged => !string.IsNullOrEmpty(Secret);

    public AddCredentialDialog()
    {
        this.InitializeComponent();
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PasswordPanel == null || SshKeyPanel == null) return;

        var tag = (string)((ComboBoxItem)TypeComboBox.SelectedItem).Tag;
        if (tag == "Password")
        {
            PasswordPanel.Visibility = Visibility.Visible;
            SshKeyPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            PasswordPanel.Visibility = Visibility.Collapsed;
            SshKeyPanel.Visibility = Visibility.Visible;
        }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.Desktop
        };

        picker.FileTypeFilter.Add(".pem");
        picker.FileTypeFilter.Add(".key");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");

        var window = App.MainWindow; 
        var hWnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hWnd);

        try
        {
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                
                var content = await Windows.Storage.FileIO.ReadTextAsync(file);
                SshKeyBox.Text = content.Trim();
                ValidateSshKey();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage.Text = $"Erreur lors de l'import : {ex.Message}";
            ErrorMessage.Visibility = Visibility.Visible;
        }
    }

    private void SshKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateSshKey();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorMessage.Text = "Le nom est requis.";
            isValid = false;
        }
        else if (string.IsNullOrWhiteSpace(UserBox.Text))
        {
            ErrorMessage.Text = "Le nom d'utilisateur est requis.";
            isValid = false;
        }
        else if (CredentialType == CredentialType.SshKey)
        {

            if (!ValidateSshKey() && (!_isEditMode || !string.IsNullOrWhiteSpace(SshKeyBox.Text)))
            {
                ErrorMessage.Text = "La clÃ© SSH est invalide.";
                isValid = false;
            }
        }
        else 
        {
            
            if (string.IsNullOrEmpty(PasswordBox.Password) && !_isEditMode)
            {
                ErrorMessage.Text = "Le mot de passe est requis.";
                isValid = false;
            }
        }

        if (!isValid)
        {
            ErrorMessage.Visibility = Visibility.Visible;
            args.Cancel = true; 
        }
    }

    private bool ValidateSshKey()
    {
        var text = SshKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SshValidationMessage.Visibility = Visibility.Collapsed;
            return false;
        }

        bool valid = text.Contains("-----BEGIN"); 

        if (!valid)
        {
            SshValidationMessage.Visibility = Visibility.Visible;
            return false;
        }
        else
        {
            SshValidationMessage.Visibility = Visibility.Collapsed;
            return true;
        }
    }

    public void SetEditMode(CredentialMetadata credential)
    {
        _isEditMode = true;
        NameBox.Text = credential.DisplayName;
        UserBox.Text = credential.UserName;

        string tagToSelect = credential.Type == CredentialType.SshKey ? "SshKey" : "Password";
        foreach (ComboBoxItem item in TypeComboBox.Items.Cast<ComboBoxItem>())
        {
            if (item.Tag as string == tagToSelect)
            {
                TypeComboBox.SelectedItem = item;
                break;
            }
        }

        TypeComboBox.IsEnabled = false;

        if (credential.Type == CredentialType.Password)
        {
            PasswordBox.Password = "";
            PasswordBox.PlaceholderText = "Laisser vide pour ne pas modifier";

            PasswordPanel.Visibility = Visibility.Visible;
            SshKeyPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SshKeyBox.Text = "";
            SshKeyBox.PlaceholderText = "Laisser vide pour ne pas modifier";

            PasswordPanel.Visibility = Visibility.Collapsed;
            SshKeyPanel.Visibility = Visibility.Visible;

            SudoPasswordBox.PlaceholderText = "Laisser vide pour ne pas modifier";
        }

        this.Title = "Modifier l'identifiant";
        this.PrimaryButtonText = "Enregistrer";
    }
}

