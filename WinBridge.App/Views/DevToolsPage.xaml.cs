using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;
using System.Runtime.Loader;
using WinBridge.App.Services;
using WinBridge.Core.Services;
using WinBridge.SDK;

namespace WinBridge.App.Views
{
    public sealed partial class DevToolsPage : Page
    {
        private readonly ModuleManager _moduleManager;
        private AssemblyLoadContext? _currentContext;

        public DevToolsPage()
        {
            this.InitializeComponent();
            _moduleManager = new ModuleManager();
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".dll");

            // Get Window Handle
            var window = (Application.Current as App)?.Window;
            if (window == null)
            {
                Log("Erreur: Fenêtre principale introuvable.");
                return;
            }
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                if (_currentContext != null)
                {
                    Log("Veuillez d'abord décharger le module actif.");
                    return;
                }

                StatusTextBlock.Text = $"Chargement de {file.Name}...";
                Log($"Tentative de chargement du module : {file.Path}");
                
                try
                {
                    var result = _moduleManager.LoadModule(file.Path);
                    if (result != null)
                    {
                        var module = result.Value.Module;
                        _currentContext = result.Value.Context;

                        ModuleInfoTextBlock.Text = $"Module: {module.Name} v{module.Version}";
                        Log($"Module chargé avec succès : {module.Name}");
                        
                        try 
                        {
                            // 1. Configurer l'Injection de Dépendances
                            var services = new ServiceCollection();
                            
                            // Enregistrer les services nécessaires aux modules
                            // Ici on enregistre SshService comme implémentation de ISshService
                            services.AddSingleton<ISshService, SshService>();
                            
                            // Construire le Provider
                            var serviceProvider = services.BuildServiceProvider();

                            // 2. Initialiser le module avec le Provider et ModuleUIProvider
                            module.Initialize(serviceProvider, new ModuleUIProvider(this.XamlRoot, null));
                            Log("Module initialisé avec ServiceProvider.");
                        }
                        catch (Exception initEx)
                        {
                            Log($"Erreur lors de l'initialisation du module : {initEx.Message}");
                        }

                        // Afficher la vue du module
                        var view = module.GetModulePage();
                        if (view != null)
                        {
                            ModuleContent.Content = view;
                        }
                        else
                        {
                             ModuleContent.Content = new TextBlock { Text = "Ce module ne fournit aucune vue." };
                        }

                        // 3. Inspecteur de Propriétés
                        InspectModuleProperties(module);

                        StatusTextBlock.Text = "Module chargé.";
                        UnloadButton.IsEnabled = true;
                        LoadButton.IsEnabled = false;
                    }
                    else
                    {
                        StatusTextBlock.Text = "Échec du chargement.";
                        Log("Aucun module valide (IWinBridgeModule) trouvé dans cette DLL ou erreur de chargement.");
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = "Erreur critique.";
                    Log($"Erreur critique lors du chargement : {ex.Message}");
                }
            }
            else
            {
                StatusTextBlock.Text = "Chargement annulé.";
            }
        }

        private void UnloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentContext == null) return;

            Log("Début du déchargement...");
            
            // Nettoyer l'interface
            ModuleContent.Content = null;
            PropertiesPanel.Children.Clear();
            // Réajouter le titre
            PropertiesPanel.Children.Add(new TextBlock { 
                Text = "Inspecteur de Propriétés", 
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0,0,0,5)
            });

            ModuleInfoTextBlock.Text = string.Empty;

            // Décharger le contexte
            _currentContext.Unload();
            _currentContext = null;

            // Forcer le GC pour libérer le fichier
            GC.Collect();
            GC.WaitForPendingFinalizers();

            UnloadButton.IsEnabled = false;
            LoadButton.IsEnabled = true;
            StatusTextBlock.Text = "Module déchargé.";
            Log("Module déchargé et GC forcé.");
        }

        private void InspectModuleProperties(object module)
        {
            try
            {
                var properties = module.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 5, 0, 5) };
                    
                    var label = new TextBlock { 
                        Text = prop.Name + ":", 
                        Width = 150, 
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    };

                    object? value = null;
                    try
                    {
                        value = prop.GetValue(module);
                    }
                    catch (Exception ex)
                    {
                        value = $"<Erreur: {ex.Message}>";
                    }

                    var valueBox = new TextBox { 
                        Text = value?.ToString() ?? "null", 
                        IsReadOnly = true, 
                        MinWidth = 200 
                    };

                    stack.Children.Add(label);
                    stack.Children.Add(valueBox);
                    PropertiesPanel.Children.Add(stack);
                }
            }
            catch (Exception ex)
            {
                Log($"Erreur lors de l'inspection des propriétés : {ex.Message}");
            }
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            DebugConsole.Text += $"[{timestamp}] {message}\n";
            // Auto-scroll simple (placer le caret à la fin)
            DebugConsole.Select(DebugConsole.Text.Length, 0);
        }
    }
}
