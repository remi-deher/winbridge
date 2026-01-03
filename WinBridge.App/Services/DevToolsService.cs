using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using WinBridge.App.Models.Dev;
using WinBridge.App.Models.Store;

namespace WinBridge.App.Services;

/// <summary>
/// Provides tools for module development, including project scanning, packaging, and debugging helpers.
/// </summary>
public class DevToolsService
{
    private const string ManifestFileName = "winbridge.manifest.json";

    /// <summary>
    /// Loads a development project from a specified folder path.
    /// Scans for manifests and module definitions.
    /// </summary>
    /// <param name="folderPath">The root folder of the project.</param>
    /// <returns>A populated DevProject model.</returns>
    public async Task<DevProject> LoadProjectAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Project directory not found: {folderPath}");

        var project = new DevProject
        {
            ProjectPath = folderPath
        };

        var modules = await ScanRepositoryAsync(folderPath);
        foreach (var m in modules) project.Modules.Add(m);

        return project;
    }

    /// <summary>
    /// Recursively scans a directory for 'winbridge.manifest.json' files.
    /// </summary>
    /// <param name="rootPath">The root directory to scan.</param>
    /// <returns>A list of found development modules.</returns>
    public async Task<System.Collections.Generic.List<DevModule>> ScanRepositoryAsync(string rootPath)
    {
        var modules = new System.Collections.Generic.List<DevModule>();

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        try 
        {
            var files = Directory.GetFiles(rootPath, ManifestFileName, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var manifest = JsonSerializer.Deserialize<MarketplaceModule>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (manifest != null)
                    {
                        modules.Add(new DevModule 
                        { 
                            ModulePath = Path.GetDirectoryName(file)!,
                            Manifest = manifest 
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading manifest {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error scanning repo {rootPath}: {ex.Message}");
        }

        return modules;
    }

    /// <summary>
    /// Writes the module's manifest back to disk.
    /// </summary>
    /// <param name="module">The module containing the manifest to save.</param>
    public async Task SaveModuleAsync(DevModule module)
    {
        if (module == null) return;

        var manifestPath = Path.Combine(module.ModulePath, ManifestFileName);
        var json = JsonSerializer.Serialize(module.Manifest, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(manifestPath, json);
        module.IsDirty = false;
    }

    /// <summary>
    /// Builds and packages the module into a ZIP archive suitable for distribution.
    /// Computes the SHA256 hash of the package.
    /// </summary>
    /// <param name="module">The module to package.</param>
    /// <returns>A tuple containing the Zip path, Hash, and a JSON snippet for the marketplace.</returns>
    public async Task<(string ZipPath, string Hash, string JsonSnippet)> PackageModuleAsync(DevModule module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));

        string publishDir = Path.Combine(module.ModulePath, "bin", "publish");
        string distDir = Path.Combine(module.ModulePath, "dist");

        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);
        if (!Directory.Exists(distDir)) Directory.CreateDirectory(distDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish -c Release -o \"{publishDir}\"",
            WorkingDirectory = module.ModulePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputLog = string.Empty;
        var errorLog = string.Empty;

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputLog += e.Data + "\n"; };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorLog += e.Data + "\n"; };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Build failed with ExitCode: {process.ExitCode}.\nErrors:\n{errorLog}\nOutput:\n{outputLog}");
        }

        string zipName = $"{module.Manifest.Id}-{module.Manifest.Version}.zip";
        string zipPath = Path.Combine(distDir, zipName);
        if (File.Exists(zipPath)) File.Delete(zipPath);

        ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, false);

        string hash = await ComputeSha256Async(zipPath);

        var snippetManifest = JsonSerializer.Deserialize<MarketplaceModule>(JsonSerializer.Serialize(module.Manifest)); 
        if (snippetManifest != null)
        {
            snippetManifest.Hash = hash;
            
            snippetManifest.DownloadUrl = $"https://github.com/user/repo/releases/download/v{module.Manifest.Version}/{zipName}";

            string snippet = JsonSerializer.Serialize(snippetManifest, new JsonSerializerOptions { WriteIndented = true });
            return (zipPath, hash, snippet);
        }

        throw new InvalidOperationException("Failed to generate manifest snippet.");
    }

    private async Task<string> ComputeSha256Async(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var bytes = await sha256.ComputeHashAsync(stream);
        
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
    private const string KnownProjectsKey = "KnownDevProjects";

    /// <summary>
    /// Retrieves the list of known/recent project paths from local settings.
    /// </summary>
    /// <returns>A list of file paths.</returns>
    public List<string> GetKnownProjects()
    {
        try
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(KnownProjectsKey, out var value) && value is string json)
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading known projects: {ex.Message}");
        }
        return new List<string>();
    }

    /// <summary>
    /// Adds a project path to the known projects list.
    /// </summary>
    /// <param name="path">The full path to the project directory.</param>
    public void AddKnownProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var list = GetKnownProjects();
        
        var cleanPath = Path.GetFullPath(path);
        
        if (!list.Contains(cleanPath, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(cleanPath);
            SaveKnownProjects(list);
        }
    }

    /// <summary>
    /// Removes a project path from the known projects list.
    /// </summary>
    /// <param name="path">The path to remove.</param>
    public void RemoveKnownProject(string path)
    {
        var list = GetKnownProjects();
        if (list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            SaveKnownProjects(list);
        }
    }

    private void SaveKnownProjects(List<string> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list);
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[KnownProjectsKey] = json;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving known projects: {ex.Message}");
        }
    }

    private const string DefaultProjectKey = "DefaultDevProject";

    /// <summary>
    /// Sets the default project to open on startup.
    /// </summary>
    /// <param name="path">The project path.</param>
    public void SetDefaultProject(string path)
    {
        try
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[DefaultProjectKey] = path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving default project: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves the default project path if set.
    /// </summary>
    /// <returns>The path or null.</returns>
    public string? GetDefaultProject()
    {
        try
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(DefaultProjectKey, out var value) && value is string path)
            {
                return path;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting default project: {ex.Message}");
        }
        return null;
    }
}
