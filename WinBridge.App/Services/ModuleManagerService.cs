using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WinBridge.App.Models;

namespace WinBridge.App.Services;

/// <summary>
/// Manages the lifecycle of external WinBridge modules.
/// Handles discovery, execution, and monitoring of module processes.
/// </summary>
/// <param name="dataService">The service for accessing application data.</param>
public class ModuleManagerService(DataService dataService)
{
    private readonly DataService _dataService = dataService;
    private readonly List<Process> _activeProcesses = [];
    private const string ModulesFolderName = "Modules";

    public static string ModulesDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModulesFolderName);

    public event EventHandler<string>? OnLog;

    private void Log(string message)
    {
        Debug.WriteLine(message);
        OnLog?.Invoke(this, message);
    }

    /// <summary>
    /// Scans the Modules directory for valid module manifests.
    /// </summary>
    /// <returns>A list of discovered module manifests.</returns>
    public List<LocalModuleManifest> DiscoverModules()
    {
        var manifests = new List<LocalModuleManifest>();

        if (!Directory.Exists(ModulesDirectory))
        {
            try
            {
                Directory.CreateDirectory(ModulesDirectory);
            }
            catch (Exception ex)
            {
                Log($"[ModuleManager] Cannot create modules directory: {ex.Message}");
                return manifests;
            }
        }

        var manifestFiles = Directory.GetFiles(ModulesDirectory, "manifest.json", SearchOption.AllDirectories);

        foreach (var file in manifestFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize<LocalModuleManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id) && !string.IsNullOrWhiteSpace(manifest.ExecutablePath))
                {
                    string moduleDir = Path.GetDirectoryName(file) ?? string.Empty;
                    manifest.ExecutablePath = Path.GetFullPath(Path.Combine(moduleDir, manifest.ExecutablePath));

                    manifests.Add(manifest);
                    Log($"[ModuleManager] Discovered module: {manifest.Name} ({manifest.Id})");
                }
            }
            catch (Exception ex)
            {
                Log($"[ModuleManager] Error reading manifest {file}: {ex.Message}");
            }
        }

        return manifests;
    }

    /// <summary>
    /// Discovers and starts all valid modules found in the Modules directory.
    /// </summary>
    public void StartModules()
    {
        var discoveredModules = DiscoverModules();

        foreach (var module in discoveredModules)
        {

            StartModule(module);
        }
    }

    /// <summary>
    /// Starts a specific module process based on its manifest.
    /// Redirects stdout and stderr to the internal log.
    /// </summary>
    /// <param name="module">The manifest of the module to start.</param>
    public void StartModule(LocalModuleManifest module)
    {
        try
        {
            if (!File.Exists(module.ExecutablePath))
            {
                Log($"[ModuleManager] Executable not found for {module.Id}: {module.ExecutablePath}");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = module.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(module.ExecutablePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) Log($"[{module.Id}] {e.Data}");
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) Log($"[{module.Id} ERROR] {e.Data}");
            };

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _activeProcesses.Add(process);
                Log($"[ModuleManager] Started module process: {module.Id} (PID: {process.Id})");
            }
        }
        catch (Exception ex)
        {
            Log($"[ModuleManager] Failed to start module {module.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts a module from a specific executable path (Side-loading).
    /// Creates a temporary manifest for the session.
    /// </summary>
    /// <param name="executablePath">The full path to the module executable.</param>
    public void StartSideLoadedModule(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            Log($"[ModuleManager] Side-load failed, file not found: {executablePath}");
            return;
        }

        var fileName = Path.GetFileNameWithoutExtension(executablePath);

        var tempManifest = new LocalModuleManifest
        {
            Id = $"sideload-{fileName.ToLower().Replace(" ", "-")}-{Guid.NewGuid().ToString()[..8]}",
            Name = $"{fileName} (Side-Loaded)",
            Version = "0.0.0-dev",
            ExecutablePath = executablePath
        };

        Log($"[ModuleManager] Side-loading module: {tempManifest.Name}");
        StartModule(tempManifest);
    }

    /// <summary>
    /// Stops and kills all active module processes.
    /// Should be called on application exit.
    /// </summary>
    public void StopAll()
    {
        Log($"[ModuleManager] Stopping {_activeProcesses.Count} modules...");

        foreach (var process in _activeProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    Log($"[ModuleManager] Killed process PID: {process.Id}");
                }
            }
            catch (Exception ex)
            {
                Log($"[ModuleManager] Error killing process {process.Id}: {ex.Message}");
            }
        }
        _activeProcesses.Clear();
    }
}
