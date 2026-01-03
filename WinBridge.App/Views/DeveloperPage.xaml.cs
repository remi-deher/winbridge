using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using System.Net.Http;
using System.IO.Compression;
using WinBridge.App.Models.Dev;
using WinBridge.App.Services;
using System.Diagnostics;
using System.IO;

namespace WinBridge.App.Views;

public sealed partial class DeveloperPage : Page, INotifyPropertyChanged
{
    private readonly DevToolsService _devService;
    private DevProject? _selectedProject;

    private string _generatedZipPath = string.Empty;
    private string _generatedHash = string.Empty;
    private string _generatedSnippet = string.Empty;

    public ObservableCollection<DevProject> Projects { get; } = new();

    public ObservableCollection<SelectableItem> AvailableCategories { get; } = new();
    public ObservableCollection<SelectableItem> AvailableOS { get; } = new();

    public ObservableCollection<ScreenshotViewModel> ScreenshotList { get; } = new();

    private DevModule? _selectedModule;

    public DevModule? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (_selectedModule != value)
            {
                _selectedModule = value;
                OnPropertyChanged();
                
                if (value != null)
                {
                    PopulateEditors(value);
                    RepoGrid.Visibility = Visibility.Collapsed;
                    EditorGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    RepoGrid.Visibility = Visibility.Visible;
                    EditorGrid.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    public DevProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject != value)
            {
                _selectedProject = value;
                OnPropertyChanged();

                SelectedModule = null;
            }
        }
    }

    public string GeneratedZipPath { get => _generatedZipPath; set { _generatedZipPath = value; OnPropertyChanged(); } }
    public string GeneratedHash { get => _generatedHash; set { _generatedHash = value; OnPropertyChanged(); } }
    public string GeneratedSnippet { get => _generatedSnippet; set { _generatedSnippet = value; OnPropertyChanged(); } }

    private string _consoleOutput = "Initialisation de la console...\nPrêt.";
    public string ConsoleOutput
    {
        get => _consoleOutput;
        set { _consoleOutput = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeveloperPage()
    {
        this.InitializeComponent();
        _devService = new DevToolsService();
        InitializeLists();
        
        if (App.ModuleManagerService != null)
        {
            App.ModuleManagerService.OnLog += ModuleManager_OnLog;
        }
    }
    
    private void ModuleManager_OnLog(object? sender, string message)
    {
        DispatcherQueue.TryEnqueue(() => 
        {
            ConsoleOutput += $"{DateTime.Now:HH:mm:ss} > {message}\n";
        });
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        var knownPaths = _devService.GetKnownProjects();
        var defaultPath = _devService.GetDefaultProject();

        foreach (var path in knownPaths)
        {
            
            if (Projects.Any(p => p.ProjectPath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;

            try
            {
                var project = await _devService.LoadProjectAsync(path);

                if (!string.IsNullOrEmpty(defaultPath) && path.Equals(defaultPath, StringComparison.OrdinalIgnoreCase))
                {
                    project.IsDefault = true;
                }

                Projects.Add(project);
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not load known project {path}: {ex.Message}");
            }
        }

        if (SelectedProject == null && Projects.Count > 0)
        {
            var defaultProj = Projects.FirstOrDefault(p => p.IsDefault);
            SelectedProject = defaultProj ?? Projects[0];
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void InitializeLists()
    {
        
        var categories = new[] { "Utility", "Network", "System", "DevOps", "Security", "Cloud", "Productivity", "IoT", "Database", "Monitoring" };
        foreach (var c in categories) AvailableCategories.Add(new SelectableItem(c));

        var osList = new[] { "Windows 10", "Windows 11", "Windows Server 2019", "Windows Server 2022", "Debian 11", "Debian 12", "Ubuntu 22.04", "Ubuntu 24.04", "RHEL 9", "macOS" };
        foreach (var os in osList) AvailableOS.Add(new SelectableItem(os));
    }

    private void PopulateEditors(DevModule module)
    {
        
        foreach (var item in AvailableCategories)
        {
            item.IsSelected = module.Manifest.Categories != null && module.Manifest.Categories.Contains(item.Name);
        }

        foreach (var item in AvailableOS)
        {
            item.IsSelected = module.Manifest.TestedOn != null && module.Manifest.TestedOn.Contains(item.Name);
        }

        ScreenshotList.Clear();
        if (module.Manifest.Screenshots != null)
        {
            foreach (var url in module.Manifest.Screenshots)
            {
                ScreenshotList.Add(new ScreenshotViewModel { Url = url });
            }
        }

        GeneratedZipPath = string.Empty;
        GeneratedHash = string.Empty;
        GeneratedSnippet = string.Empty;
        BuildResultsPanel.Visibility = Visibility.Collapsed;
    }

    private void SyncBackToManifest()
    {
        if (SelectedModule == null) return;

        SelectedModule.Manifest.Categories.Clear();
        foreach (var item in AvailableCategories.Where(x => x.IsSelected))
        {
            SelectedModule.Manifest.Categories.Add(item.Name);
        }

        SelectedModule.Manifest.TestedOn.Clear();
        foreach (var item in AvailableOS.Where(x => x.IsSelected))
        {
            SelectedModule.Manifest.TestedOn.Add(item.Name);
        }

        SelectedModule.Manifest.Screenshots.Clear();
        foreach (var item in ScreenshotList.Where(x => !string.IsNullOrWhiteSpace(x.Url)))
        {
            SelectedModule.Manifest.Screenshots.Add(item.Url);
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var project = await _devService.LoadProjectAsync(folder.Path);
                _devService.AddKnownProject(folder.Path);

                var existing = Projects.FirstOrDefault(p => p.ProjectPath == project.ProjectPath);
                if (existing != null)
                {
                    SelectedProject = existing;
                }
                else
                {
                    Projects.Add(project);
                    SelectedProject = project;
                }
            }
        }
        catch (Exception ex)
        {
            ShowDialog("Erreur", $"Impossible d'ouvrir le projet : {ex.Message}");
        }
    }
    
    private void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        
        OpenFolder_Click(sender, e);
    }

    private async void OnNewProjectWizardClick(object sender, RoutedEventArgs e)
    {
        
        var dialog = new ContentDialog
        {
            Title = "Création de Projet",
            Content = "Choisissez le mode d'initialisation du projet :",
            PrimaryButtonText = "Standard",
            SecondaryButtonText = "Prêt pour Git",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return;

        bool keepGit = result == ContentDialogResult.Secondary;

        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        await CreateModuleOrProject(folder.Path, keepGit, isRepoRoot: true);
    }

    private async void OnNewModuleWizardClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null) return;

        var nameDialog = new ContentDialog
        {
            Title = "Nouveau Module",
            Content = new TextBox { Name = "ModuleNameBox", PlaceholderText = "Nom du module (ex: AuditNetwork)" },
            PrimaryButtonText = "Créer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await nameDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var modName = ((TextBox)nameDialog.Content).Text;
        if (string.IsNullOrWhiteSpace(modName)) return;

        string srcPath = System.IO.Path.Combine(SelectedProject.ProjectPath, "src");
        if (!Directory.Exists(srcPath)) Directory.CreateDirectory(srcPath);

        string modulePath = System.IO.Path.Combine(srcPath, modName);
        if (Directory.Exists(modulePath))
        {
            ShowDialog("Erreur", "Un module avec ce nom existe déjà.");
            return;
        }
        Directory.CreateDirectory(modulePath);

        await CreateModuleOrProject(modulePath, false, isRepoRoot: false);
    }

    private async Task CreateModuleOrProject(string targetPath, bool keepGit, bool isRepoRoot)
    {
        try
        {
            BuildResultsPanel.Visibility = Visibility.Collapsed; 
            
            string templateZipUrl = "https://github.com/RemiDeher/WinBridge-Template/archive/refs/heads/main.zip";
            string tempZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winbridge_template.zip");
            string tempExtractPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winbridge_template_" + Guid.NewGuid());

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WinBridge-App");
                var bytes = await client.GetByteArrayAsync(templateZipUrl);
                await System.IO.File.WriteAllBytesAsync(tempZipPath, bytes);
            }

            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

            var extractedDirs = Directory.GetDirectories(tempExtractPath);
            string sourceDir = extractedDirs.Length > 0 ? extractedDirs[0] : tempExtractPath;

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = System.IO.Path.Combine(targetPath, System.IO.Path.GetFileName(file));
                
                if (Path.GetFileName(file).Equals("module.json", StringComparison.OrdinalIgnoreCase))
                {
                    destFile = System.IO.Path.Combine(targetPath, "winbridge.manifest.json");
                }
                
                if (!File.Exists(destFile)) File.Move(file, destFile);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = new DirectoryInfo(dir).Name;
                if (dirName.Equals(".git") || dirName.Equals(".github")) continue; 

                string destDir = System.IO.Path.Combine(targetPath, dirName);
                if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                Directory.Move(dir, destDir);
            }

            File.Delete(tempZipPath);
            Directory.Delete(tempExtractPath, true);

            if (isRepoRoot)
            {
                 
            }

            if (isRepoRoot)
            {
                var project = await _devService.LoadProjectAsync(targetPath);
                _devService.AddKnownProject(targetPath);
                Projects.Add(project);
                SelectedProject = project;
                SuccessTip.Tag = targetPath; 
            }
            else
            {
                
                if (SelectedProject != null)
                {
                    var project = await _devService.LoadProjectAsync(SelectedProject.ProjectPath);

                    var index = Projects.IndexOf(SelectedProject);
                    if (index >= 0) Projects[index] = project;
                    SelectedProject = project;
                }

                SuccessTip.Tag = targetPath;
            }

            SuccessTip.IsOpen = true;
        }
        catch (Exception ex)
        {
            ShowDialog("Erreur de création", ex.Message);
        }
    }

    private void SuccessTip_ActionButtonClick(TeachingTip sender, object args)
    {
        if (sender.Tag is string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
            sender.IsOpen = false;
        }
    }

    private async void OnCloneProjectClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CloneRepoDialog { XamlRoot = this.XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var repoUrl = dialog.RepoUrl;
                var branch = dialog.SelectedBranch;
                var destination = dialog.DestinationPath;

                SuccessTip.IsOpen = false;
                var tip = new TeachingTip 
                { 
                    Title = "Clonage en cours...", 
                    Subtitle = $"Téléchargement de {repoUrl} ({branch})",
                    IsOpen = true
                };

                var ghService = new GitHubService();
                await ghService.DownloadBranchZip(repoUrl, branch, destination);

                tip.IsOpen = false;

                var newProject = await _devService.LoadProjectAsync(destination);
                _devService.AddKnownProject(destination);

                var existing = Projects.FirstOrDefault(p => p.ProjectPath == newProject.ProjectPath);
                if (existing != null)
                {
                    SelectedProject = existing;
                }
                else
                {
                    Projects.Add(newProject);
                    SelectedProject = newProject;
                }

                ShowDialog("Clonage Réussi", $"Le projet a été cloné dans :\n{destination}");
            }
            catch (Exception ex)
            {
                ShowDialog("Erreur de Clonage", ex.Message);
            }
        }
    }

    private async void OnLoadModuleClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add(".exe");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                App.ModuleManagerService.StartSideLoadedModule(file.Path);
                ShowDialog("Module Démarré", $"Le module '{file.Name}' a été exécuté en mode Side-Load.\nIl devrait se connecter automatiquement à WinBridge.");
            }
            catch (Exception ex)
            {
                ShowDialog("Erreur", $"Impossible de démarrer le module : {ex.Message}");
            }
        }
    }

    private async void OnVerifyManifestClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var text = await Windows.Storage.FileIO.ReadTextAsync(file);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<WinBridge.App.Models.Store.MarketplaceModule>(text);
                if (manifest != null && !string.IsNullOrEmpty(manifest.Id))
                {
                    ShowDialog("Manifeste Valide", $"Le manifeste '{manifest.Name}' (v{manifest.Version}) est valide.");
                }
                else
                {
                    ShowDialog("Manifeste Invalide", "Le fichier JSON ne semble pas être un manifeste de module valide.");
                }
            }
            catch (Exception ex)
            {
                ShowDialog("Erreur de Validation", $"Erreur lors de la lecture du manifeste : {ex.Message}");
            }
        }
    }

    private void RepoSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is DevProject project)
        {
            SelectedProject = project;
        }
    }

    private void OnOpenLocalProjectClick(object sender, RoutedEventArgs e)
    {
        OpenFolder_Click(sender, e);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedModule == null) return;
        
        SyncBackToManifest();

        try
        {
            await _devService.SaveModuleAsync(SelectedModule);
        }
        catch (Exception ex)
        {
            ShowDialog("Échec de l'enregistrement", ex.Message);
        }
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedModule == null) return;

        SyncBackToManifest();

        await _devService.SaveModuleAsync(SelectedModule);

        BuildButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        BuildResultsPanel.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _devService.PackageModuleAsync(SelectedModule);
            
            GeneratedZipPath = result.ZipPath;
            GeneratedHash = result.Hash;
            GeneratedSnippet = result.JsonSnippet;
            
            BuildResultsPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowDialog("Échec du Build", ex.Message);
        }
        finally
        {
            BuildButton.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ModuleList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DevModule module)
        {
            SelectedModule = module;
        }
    }

    private void BackToRepo_Click(object sender, RoutedEventArgs e)
    {
        SelectedModule = null; 
    }

    private void PinDefault_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null) return;

        _devService.SetDefaultProject(SelectedProject.ProjectPath);

        foreach (var p in Projects)
        {
            p.IsDefault = false;
        }
        SelectedProject.IsDefault = true;

        var tip = new TeachingTip
        {
            Title = "Projet par défaut",
            Subtitle = $"Le projet '{SelectedProject.Name}' sera chargé automatiquement au démarrage.",
            IsOpen = true,
            Target = sender as FrameworkElement
        };

    }

    private void CopyHash_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(GeneratedHash))
        {
            var package = new DataPackage();
            package.SetText(GeneratedHash);
            Clipboard.SetContent(package);
        }
    }

    private void AddScreenshot_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotList.Add(new ScreenshotViewModel());
    }

    private void RemoveScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScreenshotViewModel item)
        {
            ScreenshotList.Remove(item);
        }
    }

    private async void ShowDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

public class SelectableItem : INotifyPropertyChanged
{
    public string Name { get; }
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public SelectableItem(string name) => Name = name;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ScreenshotViewModel : INotifyPropertyChanged
{
    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set { _url = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Url))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
