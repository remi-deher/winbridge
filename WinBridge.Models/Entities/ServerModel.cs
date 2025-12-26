using System;

namespace WinBridge.Models.Entities;

public class ServerModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // Nom d'affichage
    public string Host { get; set; } = string.Empty; // IP ou domaine
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;

    // Type d'authentification : Password ou PrivateKey
    public bool UsePrivateKey { get; set; }
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
}