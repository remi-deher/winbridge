using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views
{
    public sealed partial class ExtensionsPage : Page
    {
        public ExtensionsPage()
        {
            this.InitializeComponent();
            LoadExtensions();
        }

        private void LoadExtensions()
        {
            using var db = new AppDbContext();
            // Ensure DB is created (in case this is the first run with new entities)
            db.Database.EnsureCreated();
            
            ExtensionsListView.ItemsSource = db.ExtensionSources.OrderByDescending(e => e.InstalledAt).ToList();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var url = GitHubUrlTextBox.Text;
            if (string.IsNullOrWhiteSpace(url)) return;

            InstallButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.IsIndeterminate = true;
            StatusTextBlock.Text = "Recherche des releases...";

            try
            {
                // Simulation of GitHub API check and download
                await Task.Delay(1500); 
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = 30;
                StatusTextBlock.Text = "Téléchargement de module.dll...";

                await Task.Delay(1500);
                DownloadProgressBar.Value = 100;
                StatusTextBlock.Text = "Installation...";

                // Save simulated result to DB
                using var db = new AppDbContext();
                
                // Parse a fake name from URL
                var parts = url.TrimEnd('/').Split('/');
                var repoName = parts.LastOrDefault() ?? "UnknownModule";

                var extension = new ExtensionSource
                {
                    Name = repoName,
                    GitHubUrl = url,
                    Version = "1.0.0",
                    LocalPath = $"fake/path/to/{repoName}.dll",
                    InstalledAt = DateTime.Now
                };

                db.ExtensionSources.Add(extension);
                await db.SaveChangesAsync();

                StatusTextBlock.Text = "Extension installée avec succès.";
                GitHubUrlTextBox.Text = "";
                LoadExtensions();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Erreur: {ex.Message}";
            }
            finally
            {
                InstallButton.IsEnabled = true;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
