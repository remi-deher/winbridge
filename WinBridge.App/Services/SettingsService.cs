using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;

namespace WinBridge.App.Services;

/// <summary>
/// Manages application settings, particularly the store source URLs.
/// </summary>
public class SettingsService
{
    private const string StoreSourcesKey = "StoreSourceUrls";
    private const string DefaultStoreUrl = "https://raw.githubusercontent.com/RemiDeher/WinBridge/main/store.json";

    /// <summary>
    /// Gets the list of configured store source URLs.
    /// </summary>
    public List<string> StoreSourceUrls { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// Loads settings from local storage.
    /// </summary>
    public SettingsService()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.TryGetValue(StoreSourcesKey, out object? value) && value is string json)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    StoreSourceUrls = list;
                    return;
                }
            }
            catch
            {
                
            }
        }

        StoreSourceUrls = new List<string> { DefaultStoreUrl };
        SaveSettings();
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(StoreSourceUrls);
        ApplicationData.Current.LocalSettings.Values[StoreSourcesKey] = json;
    }

    /// <summary>
    /// Adds a valid store source URL to the configuration.
    /// </summary>
    /// <param name="url">The URL to add.</param>
    public void AddStoreSource(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        url = url.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) || 
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        if (!StoreSourceUrls.Exists(u => u.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            StoreSourceUrls.Add(url);
            SaveSettings();
        }
    }

    /// <summary>
    /// Removes a store source URL from the configuration.
    /// </summary>
    /// <param name="url">The URL to remove.</param>
    public void RemoveStoreSource(string url)
    {
        if (StoreSourceUrls.Remove(url))
        {
            SaveSettings();
        }
    }
}
