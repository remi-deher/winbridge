using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using WinBridge.Core.Services;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace WinBridge.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadExportData();
        }

        private void LoadExportData()
        {
            try 
            {
                using var db = new AppDbContext();
                var servers = db.Servers.OrderBy(s => s.Name).ToList();
                var groups = db.ServerGroups.OrderBy(g => g.Name).ToList();
                
                // Extract unique tags
                var tags = servers
                    .Select(s => s.Tags)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .SelectMany(t => t.Split(','))
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();

                ListServers.ItemsSource = servers;
                ListGroups.ItemsSource = groups;
                ListTags.ItemsSource = tags;
            }
            catch { }
        }

        private void ChkExportAll_Changed(object sender, RoutedEventArgs e)
        {
            if (FilterPanel == null) return;
            FilterPanel.Visibility = (ChkExportAll.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Sens_Checked(object sender, RoutedEventArgs e)
        {
            // Optional : Visual feedback
        }

        private void Sens_Unchecked(object sender, RoutedEventArgs e)
        {
            // Optional : Visual feedback
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            bool securityRequired = (ChkSensitive.IsChecked == true || ChkKeys.IsChecked == true);

            if (securityRequired && string.IsNullOrEmpty(TxtExportPass.Password))
            {
                var dialog = new ContentDialog
                {
                    Title = "Sécurité",
                    Content = "Un mot de passe est obligatoire pour exporter des données sensibles (Clés ou Mots de passe).",
                    CloseButtonText = "Ok",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Fichier WinBridge", new List<string>() { ".wb" });
            savePicker.SuggestedFileName = $"Backup_WinBridge_{DateTime.Now:yyyyMMdd}";

            var window = (Application.Current as App)?.Window; 
            if (window != null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(savePicker, hwnd);
            }

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    using var randomAccess = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    using var stream = randomAccess.AsStreamForWrite();
                    
                    stream.SetLength(0);

                    // Build Filter Lists
                    List<Guid>? filterGroupIds = null;
                    List<Guid>? filterServerIds = null;
                    List<string>? filterTags = null;

                    if (ChkExportAll.IsChecked != true)
                    {
                        if (ListGroups.SelectedItems.Count > 0)
                            filterGroupIds = ListGroups.SelectedItems.Cast<ServerGroup>().Select(g => g.Id).ToList();
                        
                        if (ListServers.SelectedItems.Count > 0)
                            filterServerIds = ListServers.SelectedItems.Cast<ServerModel>().Select(s => s.Id).ToList();

                        if (ListTags.SelectedItems.Count > 0)
                            filterTags = ListTags.SelectedItems.Cast<string>().ToList();
                    }

                    var service = new BackupService();
                    var options = new BackupOptions
                    {
                        IncludeServers = true, // Always true if we are in this UI flow
                        IncludeExtensions = ChkExtensions.IsChecked ?? false,
                        IncludeKeys = ChkKeys.IsChecked ?? false,
                        IncludeSensitiveData = ChkSensitive.IsChecked ?? false,
                        FilterGroupIds = filterGroupIds,
                        FilterServerIds = filterServerIds,
                        FilterTags = filterTags
                    };

                    await service.ExportBackupAsync(stream, options, TxtExportPass.Password);
                    
                    var dlg = new ContentDialog
                    {
                        Title = "Succès",
                        Content = "Sauvegarde effectuée avec succès.",
                        CloseButtonText = "Ok",
                        XamlRoot = this.XamlRoot
                    };
                    await dlg.ShowAsync();
                }
                catch (Exception ex)
                {
                    var dlg = new ContentDialog
                    {
                        Title = "Erreur",
                        Content = $"Erreur lors de l'export: {ex.Message}",
                        CloseButtonText = "Ok",
                        XamlRoot = this.XamlRoot
                    };
                    await dlg.ShowAsync();
                }
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".wb");

            var window = (Application.Current as App)?.Window; 
            if (window != null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(openPicker, hwnd);
            }

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    using var randomAccess = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    using var stream = randomAccess.AsStreamForRead();
                    var service = new BackupService();
                    await service.ImportBackupAsync(stream, TxtImportPass.Password);

                    TxtImportStatus.Text = $"Import réussi : {file.Name}";
                }
                catch (Exception ex)
                {
                    var dlg = new ContentDialog
                    {
                        Title = "Erreur",
                        Content = $"Erreur lors de l'import: {ex.Message} (Mot de passe incorrect ?)",
                        CloseButtonText = "Ok",
                        XamlRoot = this.XamlRoot
                    };
                    await dlg.ShowAsync();
                }
            }
        }
    }
}
