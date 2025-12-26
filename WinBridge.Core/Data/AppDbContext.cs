using Microsoft.EntityFrameworkCore;
using WinBridge.Models.Entities;
using System.IO;
using Windows.Storage;

namespace WinBridge.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<ServerModel> Servers { get; set; }
    public DbSet<SshKeyModel> Keys { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // On place la base de données dans le dossier local de l'application
        string dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "winbridge.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}