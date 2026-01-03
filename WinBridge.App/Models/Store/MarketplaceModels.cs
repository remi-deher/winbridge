using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinBridge.App.Models.Store;

public class MarketplaceRoot
{
    [JsonPropertyName("repository")]
    public RepositoryMetadata Repository { get; set; } = new();

    [JsonPropertyName("modules")]
    public List<MarketplaceModule> Modules { get; set; } = new();
}

public class RepositoryMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("maintainer")]
    public string Maintainer { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Stable"; 

    [JsonPropertyName("website")]
    public string Website { get; set; } = string.Empty;
}

public class MarketplaceModule : System.ComponentModel.INotifyPropertyChanged
{
    
    private string _id = string.Empty;
    [JsonPropertyName("id")]
    public string Id 
    { 
        get => _id; 
        set { if (_id != value) { _id = value; OnPropertyChanged(); } } 
    }

    private string _name = string.Empty;
    [JsonPropertyName("name")]
    public string Name 
    { 
        get => _name; 
        set { if (_name != value) { _name = value; OnPropertyChanged(); } } 
    }

    private string _author = string.Empty;
    [JsonPropertyName("author")]
    public string Author 
    { 
        get => _author; 
        set { if (_author != value) { _author = value; OnPropertyChanged(); } } 
    }

    private string _version = string.Empty;
    [JsonPropertyName("version")]
    public string Version 
    { 
        get => _version; 
        set { if (_version != value) { _version = value; OnPropertyChanged(); } } 
    }

    private string _shortDescription = string.Empty;
    [JsonPropertyName("shortDescription")]
    public string ShortDescription 
    { 
        get => _shortDescription; 
        set { if (_shortDescription != value) { _shortDescription = value; OnPropertyChanged(); } } 
    }

    private string _fullDescription = string.Empty;
    [JsonPropertyName("fullDescription")]
    public string FullDescription 
    { 
        get => _fullDescription; 
        set { if (_fullDescription != value) { _fullDescription = value; OnPropertyChanged(); } } 
    }

    private string _iconUrl = string.Empty;
    [JsonPropertyName("iconUrl")]
    public string IconUrl 
    { 
        get => _iconUrl; 
        set { if (_iconUrl != value) { _iconUrl = value; OnPropertyChanged(); } } 
    }

    [JsonPropertyName("screenshots")]
    public List<string> Screenshots { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("testedOn")]
    public List<string> TestedOn { get; set; } = new();

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
