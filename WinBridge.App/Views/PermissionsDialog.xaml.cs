using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinBridge.App.Views;

public sealed partial class PermissionsDialog : ContentDialog
{
    private string _remotePath = "";
    public string OctalPermissions { get; private set; } = "755";

    public PermissionsDialog()
    {
        InitializeComponent();

        OwnerRead.Checked += UpdateOctalDisplay;
        OwnerRead.Unchecked += UpdateOctalDisplay;
        OwnerWrite.Checked += UpdateOctalDisplay;
        OwnerWrite.Unchecked += UpdateOctalDisplay;
        OwnerExecute.Checked += UpdateOctalDisplay;
        OwnerExecute.Unchecked += UpdateOctalDisplay;

        GroupRead.Checked += UpdateOctalDisplay;
        GroupRead.Unchecked += UpdateOctalDisplay;
        GroupWrite.Checked += UpdateOctalDisplay;
        GroupWrite.Unchecked += UpdateOctalDisplay;
        GroupExecute.Checked += UpdateOctalDisplay;
        GroupExecute.Unchecked += UpdateOctalDisplay;

        OthersRead.Checked += UpdateOctalDisplay;
        OthersRead.Unchecked += UpdateOctalDisplay;
        OthersWrite.Checked += UpdateOctalDisplay;
        OthersWrite.Unchecked += UpdateOctalDisplay;
        OthersExecute.Checked += UpdateOctalDisplay;
        OthersExecute.Unchecked += UpdateOctalDisplay;
    }

    public void SetPermissions(string remotePath, string octalPerms)
    {
        _remotePath = remotePath;
        FilePathText.Text = remotePath;

        if (octalPerms.Length == 3)
        {
            
            char owner = octalPerms[0];
            char group = octalPerms[1];
            char others = octalPerms[2];

            OwnerRead.IsChecked = (owner - '0') >= 4;
            OwnerWrite.IsChecked = ((owner - '0') % 4) >= 2;
            OwnerExecute.IsChecked = ((owner - '0') % 2) == 1;

            GroupRead.IsChecked = (group - '0') >= 4;
            GroupWrite.IsChecked = ((group - '0') % 4) >= 2;
            GroupExecute.IsChecked = ((group - '0') % 2) == 1;

            OthersRead.IsChecked = (others - '0') >= 4;
            OthersWrite.IsChecked = ((others - '0') % 4) >= 2;
            OthersExecute.IsChecked = ((others - '0') % 2) == 1;
        }

        UpdateOctalDisplay(null, null);
    }

    private void UpdateOctalDisplay(object? sender, RoutedEventArgs? e)
    {
        int owner = (OwnerRead.IsChecked == true ? 4 : 0) +
                    (OwnerWrite.IsChecked == true ? 2 : 0) +
                    (OwnerExecute.IsChecked == true ? 1 : 0);

        int group = (GroupRead.IsChecked == true ? 4 : 0) +
                    (GroupWrite.IsChecked == true ? 2 : 0) +
                    (GroupExecute.IsChecked == true ? 1 : 0);

        int others = (OthersRead.IsChecked == true ? 4 : 0) +
                     (OthersWrite.IsChecked == true ? 2 : 0) +
                     (OthersExecute.IsChecked == true ? 1 : 0);

        OctalPermissions = $"{owner}{group}{others}";
        OctalDisplay.Text = OctalPermissions;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        
        if (string.IsNullOrEmpty(OctalPermissions))
        {
            ErrorMessage.Text = "Permissions invalides.";
            ErrorMessage.Visibility = Visibility.Visible;
            args.Cancel = true;
        }
    }
}
