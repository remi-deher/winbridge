using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinBridge.App.Services;
using WinBridge.SDK;

namespace WinBridge.App.Views
{
    public sealed partial class DevToolsPage : Page
    {
        private readonly ModuleManager _moduleManager;

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
                StatusTextBlock.Text = "Erreur: Fenêtre principale introuvable.";
                return;
            }
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusTextBlock.Text = $"Chargement de {file.Name}...";
                
                try
                {
                    var module = _moduleManager.LoadModule(file.Path);
                    if (module != null)
                    {
                        ModuleInfoTextBlock.Text = $"Module: {module.Name} v{module.Version}";
                        
                        // Initialize with null service provider for now
                        try 
                        {
                            module.Initialize(null!); 
                        }
                        catch (Exception initEx)
                        {
                            StatusTextBlock.Text = "Module chargé mais erreur d'initialisation: " + initEx.Message;
                        }

                        if (module.View != null)
                        {
                            ModuleContent.Content = module.View;
                        }
                        else
                        {
                             ModuleContent.Content = new TextBlock { Text = "Ce module ne fournit aucune vue." };
                        }

                        StatusTextBlock.Text = "Module chargé avec succès.";
                    }
                    else
                    {
                        StatusTextBlock.Text = "Aucun module valide (IWinBridgeModule) trouvé dans cette DLL.";
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Erreur critique: {ex.Message}";
                }
            }
            else
            {
                StatusTextBlock.Text = "Chargement annulé.";
            }
        }
    }
}
