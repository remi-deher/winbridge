using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WinBridge.App.Models.Store;

namespace WinBridge.App.Services;

/// <summary>
/// Handles interactions with the online extension store.
/// Fetches catalogs and manages module installation.
/// </summary>
public class StoreService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly ModuleManagerService _moduleManagerService;

    public StoreService(HttpClient httpClient, SettingsService settingsService, ModuleManagerService moduleManagerService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _moduleManagerService = moduleManagerService;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WinBridge-App");
        }
    }

    /// <summary>
    /// Retrieves extension catalogs from configured store sources.
    /// </summary>
    /// <returns>A list of catalog roots containing available modules.</returns>
    public async Task<List<MarketplaceRoot>> GetCatalogsAsync()
    {
        var catalogs = new List<MarketplaceRoot>();

        foreach (var url in _settingsService.StoreSourceUrls)
        {
            try
            {
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                var response = await _httpClient.GetAsync(url, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var root = JsonSerializer.Deserialize<MarketplaceRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (root != null)
                    {
                        catalogs.Add(root);
                    }
                }
                else
                {
                    Debug.WriteLine($"[StoreService] Failed to fetch catalog from {url}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreService] Error fetching catalog from {url}: {ex.Message}");
                
            }
        }

        return catalogs;
    }

    /// <summary>
    /// Downloads and installs a specific module from the marketplace.
    /// </summary>
    /// <param name="module">The marketplace module definition.</param>
    public async Task InstallModuleAsync(MarketplaceModule module)
    {
        if (string.IsNullOrWhiteSpace(module.DownloadUrl) || string.IsNullOrWhiteSpace(module.Id))
        {
            throw new ArgumentException("Invalid module data for installation.");
        }

        try
        {
            
            var zipData = await _httpClient.GetByteArrayAsync(module.DownloadUrl);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string modulesDir = Path.Combine(appData, "WinBridge", "Modules");
            string targetDir = Path.Combine(modulesDir, module.Id);
            string tempZipPath = Path.Combine(Path.GetTempPath(), $"{module.Id}_{Guid.NewGuid()}.zip");

            if (!Directory.Exists(modulesDir))
            {
                Directory.CreateDirectory(modulesDir);
            }

            await File.WriteAllBytesAsync(tempZipPath, zipData);

            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            ZipFile.ExtractToDirectory(tempZipPath, targetDir);

            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            Debug.WriteLine($"[StoreService] Module {module.Id} installed successfully to {targetDir}");

            _moduleManagerService.DiscoverModules(); 
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StoreService] Failed to install module {module.Id}: {ex.Message}");
            throw;
        }
    }
}
