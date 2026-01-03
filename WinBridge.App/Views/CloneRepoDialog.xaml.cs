using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.App.Services;

namespace WinBridge.App.Views;

public sealed partial class CloneRepoDialog : ContentDialog
{
    private readonly GitHubService _gitHubService;

    public string RepoUrl => RepoUrlBox.Text;
    public string SelectedBranch => BranchBox.SelectedItem as string ?? string.Empty;
    public string DestinationPath => DestinationBox.Text;

    public CloneRepoDialog()
    {
        this.InitializeComponent();
        _gitHubService = new GitHubService();
    }

    private void RepoUrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckButton.IsEnabled = Uri.TryCreate(RepoUrlBox.Text, UriKind.Absolute, out var uri) 
                                && uri.Host.Contains("github.com");
        ValidateForm();
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingBar.Visibility = Visibility.Visible;
        StatusText.Text = string.Empty;
        BranchPanel.Visibility = Visibility.Collapsed;
        DestinationPanel.Visibility = Visibility.Collapsed;
        CheckButton.IsEnabled = false;

        try
        {
            var info = await _gitHubService.GetRepositoryDetails(RepoUrlBox.Text);
            
            BranchBox.ItemsSource = info.Branches;
            
            if (info.Branches.Contains(info.DefaultBranch))
            {
                BranchBox.SelectedItem = info.DefaultBranch;
            }
            else if (info.Branches.Any())
            {
                BranchBox.SelectedIndex = 0;
            }

            DefaultBranchText.Text = $"Branche par défaut : {info.DefaultBranch}";
            
            BranchPanel.Visibility = Visibility.Visible;
            DestinationPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur : {ex.Message}";
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            CheckButton.IsEnabled = true;
            ValidateForm();
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            
            var repoName = RepoUrlBox.Text.TrimEnd('/').Split('/').Last().Replace(".git", "");
            var fullPath = System.IO.Path.Combine(folder.Path, repoName);
            
            DestinationBox.Text = fullPath;
            ValidateForm();
        }
    }

    private void BranchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ValidateForm();
    }

    private void ValidateForm()
    {
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(RepoUrlBox.Text)
                                 && !string.IsNullOrWhiteSpace(SelectedBranch)
                                 && !string.IsNullOrWhiteSpace(DestinationBox.Text);
    }
}
