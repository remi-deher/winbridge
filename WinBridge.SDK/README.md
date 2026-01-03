# WinBridge.SDK

## Description
**WinBridge.SDK** is the official development kit for building WinBridge modules. It abstracts the complexity of gRPC communication and provides a clean, event-driven API for interacting with the host application.

## Key Features
- **BridgeModule**: The base class for all modules.
- **Hybrid Reference Support**: Designed to work both as a local project reference (for monorepo dev) and as a standalone NuGet package.
- **Services**: Access to host features like IStorage, INetwork, IVault, and ITerminal.

## Usage
To create a module, inherit from various base classes. The simplest start is extending BridgeModule:

```csharp
using WinBridge.SDK;

public class MyCustomModule : BridgeModule
{
    public MyCustomModule() : base("my-module", "My Custom Module", "1.0.0")
    {
        // Register capabilities
        SupportsWindows();
        
        // Add UI integration
        AddTab("Server Stats", "\uE9D2", "index.html");
    }

    public override async Task RunAsync()
    {
        Logger.Info("Module started successfully.");
        await base.RunAsync();
    }
}
