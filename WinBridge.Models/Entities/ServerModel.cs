using System;

namespace WinBridge.Models.Entities;

public class ServerModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;

    // --- Stratégies d'authentification ---

    // 1. Clé stockée dans la base WinBridge (Vault)
    public bool UsePrivateKey { get; set; }
    public Guid? SshKeyId { get; set; }

    // 2. Agent SSH (1Password, OpenSSH, Pageant)
    public bool UseSshAgent { get; set; }

    // 3. Mot de passe (Repli)
    public string? Password { get; set; }
}