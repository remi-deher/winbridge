using System;
using System.Collections.ObjectModel;
using WinBridge.App.Models.Store;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinBridge.App.Models.Dev;

public class DevModule
{
    public string ModulePath { get; set; } = string.Empty;
    public MarketplaceModule Manifest { get; set; } = new();
    public bool IsDirty { get; set; }
}

public class DevProject : INotifyPropertyChanged
{
    public string ProjectPath { get; set; } = string.Empty;
    public ObservableCollection<DevModule> Modules { get; set; } = new();

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault != value)
            {
                _isDefault = value;
                OnPropertyChanged();
            }
        }
    }

    public string Name => System.IO.Path.GetFileName(ProjectPath);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
