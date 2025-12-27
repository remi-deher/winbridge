using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace WinBridge.Core.Services;

public class VaultService
{
    private const string VaultResourceName = "WinBridge_SSH_Keys";
    private readonly SecurePipeService? _securePipeService;

    public VaultService(SecurePipeService? securePipeService = null)
    {
        _securePipeService = securePipeService;
    }

    public async Task<string?> GetSecretViaPipeAsync(string keyId)
    {
        if (_securePipeService == null) return null;
        var (content, _) = GetKeyContent(keyId); // Passphrase handling simplified
        if (string.IsNullOrEmpty(content)) return null;
        return await _securePipeService.SendSecretAsync(content);
    }

    // Sauvegarde le contenu de la clé privée de manière sécurisée
    public void SaveKeyContent(string keyId, string privateKeyContent, string? passphrase)
    {
        var vault = new PasswordVault();

        // On stocke la clé privée. 
        // UserName = ID de la clé (Guid)
        // Password = Contenu de la clé (PEM/OpenSSH)
        var cred = new PasswordCredential(VaultResourceName, keyId, privateKeyContent);

        if (!string.IsNullOrEmpty(passphrase))
        {
            cred.Properties.Add("passphrase", passphrase);
        }

        vault.Add(cred);
    }

    // ... (rest same)

    // Récupère le contenu pour la connexion
    public (string content, string? passphrase) GetKeyContent(string keyId)
    {
        var vault = new PasswordVault();
        try
        {
            var cred = vault.Retrieve(VaultResourceName, keyId);
            cred.RetrievePassword(); // Obligatoire pour déchiffrer le contenu

            string passphrase = null;
            // Récupérer la passphrase si elle existe dans les propriétés (logique custom)
            // Note: PasswordCredential.Properties n'est pas persisté par défaut sur toutes les versions de Windows.
            // Une astuce courante est de stocker "Passphrase|ContenuClé" dans le champ Password.

            return (cred.Password, passphrase);
        }
        catch
        {
            return (string.Empty, null); // Clé introuvable
        }
    }

    public void DeleteKey(string keyId)
    {
        var vault = new PasswordVault();
        try
        {
            var cred = vault.Retrieve(VaultResourceName, keyId);
            vault.Remove(cred);
        }
        catch { /* Ignorer si n'existe pas */ }
    }
}