using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WinBridge.Models.Entities
{
    public class ServerGroup
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = "Groupe par défaut";

        public string Color { get; set; } = "#FFFFFF"; // Hex color

        // Navigation property if needed, but simple FK on Server is enough for now
        // public ICollection<ServerModel> Servers { get; set; } = new List<ServerModel>();
    }
}
