using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using WinBridge.App.Services;
using WinBridge.App.Services.Files;
using WinBridge.App.Services.Grpc;

namespace WinBridge.App;

public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;
    public static DataService DataService { get; private set; } = null!;
    public static VaultService VaultService { get; private set; } = null!;
    public static BridgeService BridgeService { get; private set; } = null!;
    public static SftpService SftpService { get; private set; } = null!;
    public static FileSystemManager FileSystemManager { get; private set; } = null!;
    public static TransferManager TransferManager { get; private set; } = null!;
    public static ModuleManagerService ModuleManagerService { get; private set; } = null!;

    private WebApplication? _grpcApp;

    public App()
    {
        InitializeComponent();
    }

    public static event Action<bool>? DeveloperModeChanged;

    public static bool IsDeveloperMode
    {
        get
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                return localSettings.Values.TryGetValue("IsDeveloperMode", out var value) && (bool)value;
            }
            catch
            {
                return false;
            }
        }
        set
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values["IsDeveloperMode"] = value;
                DeveloperModeChanged?.Invoke(value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting developer mode: {ex.Message}");
            }
        }
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        string dbPath = System.IO.Path.Combine(localFolder, "data.db");

        System.Diagnostics.Debug.WriteLine($"[App] Database path: {dbPath}");

        DataService = new DataService(dbPath);
        VaultService = new VaultService();
        BridgeService = new BridgeService(DataService);
        SftpService = new SftpService(VaultService, DataService);
        FileSystemManager = new FileSystemManager();
        TransferManager = new TransferManager();
        ModuleManagerService = new ModuleManagerService(DataService);

        try
        {
            await DataService.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Database initialization error: {ex.Message}");
            throw;
        }

        _ = StartGrpcServerAsync();

        _ = Task.Run(() => ModuleManagerService.StartModules());

        MainWindow = new MainWindow();
        MainWindow.Activate();

        MainWindow.Closed += async (s, e) =>
        {
            ModuleManagerService.StopAll();
            if (_grpcApp != null)
            {
                await _grpcApp.StopAsync();
                await _grpcApp.DisposeAsync();
            }
        };
    }

    private async Task StartGrpcServerAsync()
    {
        try
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {

                if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                {
                    serverOptions.ListenNamedPipe(WinBridge.Core.WinBridgeConstants.PipeName, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                }

                serverOptions.ListenLocalhost(5000, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddSingleton(DataService);
            builder.Services.AddSingleton(BridgeService);

            builder.Services.AddGrpc(options =>
            {
                options.Interceptors.Add<AuthInterceptor>();
            });

            _grpcApp = builder.Build();
            _grpcApp.MapGrpcService<BridgeService>();

            await _grpcApp.StartAsync();
            System.Diagnostics.Debug.WriteLine("[App] gRPC Server started (Named Pipe + Port 5000)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to start gRPC server: {ex.Message}");
        }
    }
}

