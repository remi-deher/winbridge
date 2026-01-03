using System;
using Windows.Security.Credentials;

namespace WinBridge.App.Services;

/// <summary>
/// Provides secure storage for sensitive credentials using Windows PasswordVault.
/// </summary>
public class VaultService
{
    private const string ResourcePrefix = "WinBridge.Credentials";

    /// <summary>
    /// Stores or updates a secret in the vault.
    /// </summary>
    /// <param name="key">The unique key for the secret.</param>
    /// <param name="username">The username associated with the secret.</param>
    /// <param name="secret">The sensitive value to store.</param>
    /// <exception cref="InvalidOperationException">Thrown if vault access is denied.</exception>
    public static void StoreSecret(string key, string username, string secret)
    {
        try
        {
            var vault = new PasswordVault();

            try
            {
                var existing = vault.Retrieve(ResourcePrefix, key);
                if (existing != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VaultService] Removing existing credential: {key}");
                    vault.Remove(existing);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VaultService] No existing credential to remove: {key} ({ex.Message})");
            }

            System.Diagnostics.Debug.WriteLine($"[VaultService] Adding new credential: {key}");
            var vaultCred = new PasswordCredential(ResourcePrefix, key, secret);
            vault.Add(vaultCred);
            System.Diagnostics.Debug.WriteLine($"[VaultService] Successfully stored: {key}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VaultService] ACCESS DENIED - Permission manquante: {ex.Message}");
            throw new InvalidOperationException(
                "Impossible d'accéder au coffre-fort Windows. Vérifiez que la capacité 'enterpriseAuthentication' est activée dans Package.appxmanifest.",
                ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VaultService] CRITICAL ERROR storing secret: {ex}");
            throw new InvalidOperationException($"Impossible d'obtenir des informations d'identification du coffre: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves a secret from the vault.
    /// </summary>
    /// <param name="key">The key of the secret to retrieve.</param>
    /// <returns>The secret value, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if vault access is denied.</exception>
    public static string? RetrieveSecret(string key)
    {
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(ResourcePrefix, key);
            cred.RetrievePassword();
            System.Diagnostics.Debug.WriteLine($"[VaultService] Retrieved secret: {key}");
            return cred.Password;
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VaultService] ACCESS DENIED - Permission manquante: {ex.Message}");
            throw new InvalidOperationException(
                "Impossible d'accéder au coffre-fort Windows. Vérifiez que la capacité 'enterpriseAuthentication' est activée dans Package.appxmanifest.",
                ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VaultService] Secret not found: {key} ({ex.Message})");
            return null;
        }
    }

    /// <summary>
    /// Removes a secret from the vault.
    /// </summary>
    /// <param name="key">The key of the secret to remove.</param>
    public static void Remove(string key)
    {
        var vault = new PasswordVault();
        try
        {
            var cred = vault.Retrieve(ResourcePrefix, key);
            if (cred != null)
            {
                vault.Remove(cred);
                System.Diagnostics.Debug.WriteLine($"[VaultService] Removed secret: {key}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VaultService] Could not remove (may not exist): {key} ({ex.Message})");
        }
    }
}
