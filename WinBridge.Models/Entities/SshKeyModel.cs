using System;

namespace WinBridge.Models.Entities;

public class SshKeyModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Le nom affiché dans l'interface (ex: "Clé Prod", "Clé AWS")
    public string Name { get; set; } = string.Empty;

    // L'identifiant utilisateur associé par défaut (ex: "root", "admin")
    public string DefaultUsername { get; set; } = string.Empty;

    // Indique si la clé est protégée par une passphrase (stockée aussi dans le vault)
    public bool HasPassphrase { get; set; }
}