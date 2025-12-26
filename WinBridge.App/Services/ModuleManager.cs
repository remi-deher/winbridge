using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using WinBridge.SDK;

namespace WinBridge.App.Services
{
    public class ModuleManager
    {
        public (IWinBridgeModule Module, AssemblyLoadContext Context)? LoadModule(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                // Create a collectible context
                var context = new AssemblyLoadContext(
                    name: Path.GetFileNameWithoutExtension(path), 
                    isCollectible: true
                );

                // Load the assembly
                var assembly = context.LoadFromAssemblyPath(path);
                
                // Find the implementation of IWinBridgeModule
                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IWinBridgeModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null)
                {
                    context.Unload();
                    return null;
                }

                // Create instance
                var module = Activator.CreateInstance(moduleType) as IWinBridgeModule;
                if (module != null)
                {
                    return (module, context);
                }
                
                context.Unload();
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading module from {path}: {ex.Message}");
                throw; // Re-throw to let the caller handle logging
            }
        }
    }
}
