using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinBridge.Core.Models;
using WinBridge.App.Models; 

namespace WinBridge.App.Services;

/// <summary>
/// Provides access to the application's local SQLite database.
/// Manages persistence for servers, credentials, and module metadata.
/// </summary>
/// <param name="dbPath">The file path to the SQLite database.</param>
public class DataService(string dbPath)
{
    private readonly SQLiteAsyncConnection _database = new(dbPath);

    /// <summary>
    /// Initializes the database connection and creates required tables if they don't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _database.EnableWriteAheadLoggingAsync();

        await _database.CreateTableAsync<Server>();
        await _database.CreateTableAsync<CredentialMetadata>();
        await _database.CreateTableAsync<WinBridge.App.Models.ModuleInfo>(); 
    }

    /// <summary>
    /// Retrieves all server records from the database.
    /// </summary>
    /// <returns>A list of Server entities.</returns>
    public async Task<List<Server>> GetServersAsync()
    {
        return await _database.Table<Server>().ToListAsync();
    }

    public async Task<int> AddServerAsync(Server server)
    {
        return await _database.InsertAsync(server);
    }

    public async Task<int> UpdateServerAsync(Server server)
    {
        return await _database.UpdateAsync(server);
    }

    public async Task<int> DeleteServerAsync(Server server)
    {
        return await _database.DeleteAsync(server);
    }

    public async Task<Server?> GetServerByIdAsync(int serverId)
    {
        return await _database.Table<Server>().Where(s => s.Id == serverId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Retrieves a list of unique group names used by existing servers.
    /// </summary>
    /// <returns>A list of unique group strings.</returns>
    public async Task<List<string>> GetUniqueGroupsAsync()
    {
        var groups = await _database.QueryScalarsAsync<string>(
            "SELECT DISTINCT \"Group\" FROM Server WHERE \"Group\" IS NOT NULL AND \"Group\" != '' ORDER BY \"Group\""
        );
        return groups;
    }

    public async Task<List<CredentialMetadata>> GetCredentialsAsync()
    {
        return await _database.Table<CredentialMetadata>().ToListAsync();
    }

    public async Task<int> AddCredentialAsync(CredentialMetadata credential)
    {
        return await _database.InsertAsync(credential);
    }

    public async Task<int> UpdateCredentialAsync(CredentialMetadata credential)
    {
        return await _database.UpdateAsync(credential);
    }

    public async Task<int> DeleteCredentialAsync(CredentialMetadata credential)
    {
        return await _database.DeleteAsync(credential);
    }

    public async Task<CredentialMetadata?> GetCredentialByIdAsync(int credentialId)
    {
        return await _database.Table<CredentialMetadata>().Where(c => c.Id == credentialId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Reassigns the ownership of a credential to a different module.
    /// </summary>
    /// <param name="credentialId">The ID of the credential to update.</param>
    /// <param name="newOwnerModuleId">The new owner module ID.</param>
    /// <returns>The number of rows updated.</returns>
    public async Task<int> ReassignCredentialOwnerAsync(int credentialId, string newOwnerModuleId)
    {
        var cred = await _database.Table<CredentialMetadata>().Where(c => c.Id == credentialId).FirstOrDefaultAsync();
        if (cred != null)
        {
            cred.OwnerModuleId = newOwnerModuleId;
            return await _database.UpdateAsync(cred);
        }
        return 0;
    }

    /// <summary>
    /// Retrieves all registered modules.
    /// </summary>
    /// <returns>A list of ModuleInfo objects.</returns>
    public async Task<List<WinBridge.App.Models.ModuleInfo>> GetRegisteredModulesAsync()
    {
        return await _database.Table<WinBridge.App.Models.ModuleInfo>().ToListAsync();
    }

    public WinBridge.App.Models.ModuleInfo? GetModule(string moduleId)
    {

        return _database.Table<WinBridge.App.Models.ModuleInfo>().Where(m => m.Id == moduleId).FirstOrDefaultAsync().Result;
    }

    public async Task<WinBridge.App.Models.ModuleInfo?> GetModuleByIdAsync(string moduleId)
    {
        return await _database.Table<WinBridge.App.Models.ModuleInfo>().Where(m => m.Id == moduleId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saves or updates a module definition in the database.
    /// </summary>
    /// <param name="module">The module info to save.</param>
    public void SaveModule(WinBridge.App.Models.ModuleInfo module)
    {
        
        _database.InsertOrReplaceAsync(module).Wait();
    }

    public async Task<int> UpdateModuleAsync(WinBridge.App.Models.ModuleInfo module)
    {
        return await _database.UpdateAsync(module);
    }

    public async Task<int> DeleteModuleAsync(WinBridge.App.Models.ModuleInfo module)
    {
        return await _database.DeleteAsync(module);
    }

    public async Task<WinBridge.App.Models.ModuleInfo?> GetModuleByTokenAsync(string token)
    {
        return await _database.Table<WinBridge.App.Models.ModuleInfo>().Where(m => m.Token == token).FirstOrDefaultAsync();
    }
}

