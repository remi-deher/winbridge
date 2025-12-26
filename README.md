# WinBridge

**WinBridge** est une application d'administration système moderne conçue avec **WinUI 3** et **.NET 10**. Elle permet de gérer des serveurs Linux à distance avec une interface fluide et sécurisée.

## Fonctionnalités
- **Terminal Interactif** : Accès complet à la console SSH.
- **Explorateur de Fichiers** : Gestion via SFTP avec support du transfert inter-serveurs.
- **Commandes Rapides** : Actions en un clic (Update, Reboot, Shutdown).
- **Gestionnaire de Clés** : Import, génération (Ed25519) et déploiement automatisé de clés SSH.

## Stack Technique
- **Framework** : WinUI 3 (Windows App SDK)
- **Runtime** : .NET 10
- **Communication** : SSH.NET
- **Base de données** : SQLite (via EF Core) pour le stockage sécurisé des serveurs.

## Sécurité
- Chiffrement des données sensibles via Windows Data Protection API.
- Support des clés privées avec Passphrase.