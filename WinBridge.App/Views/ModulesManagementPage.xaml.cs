using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views
{
    // Simple helper class for binding
    public class ServerAssignmentViewModel
    {
        public Guid ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        
        // Context to help call back, or we handle Toggled event in code behind with Tag
    }

    public sealed partial class ModulesManagementPage : Page
    {
        private ExtensionSource? _selectedModule;

        public ModulesManagementPage()
        {
            this.InitializeComponent();
            LoadModules();
        }

        private void LoadModules()
        {
            using var db = new AppDbContext();
            ModulesList.ItemsSource = db.ExtensionSources.ToList();
        }

        private void ModulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModulesList.SelectedItem is ExtensionSource module)
            {
                _selectedModule = module;
                SelectedModuleName.Text = module.Name;
                DetailHeader.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
                LoadServerAssignments(module.Id);
            }
            else
            {
                _selectedModule = null;
                DetailHeader.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
                ServerAssignmentList.ItemsSource = null;
            }
        }

        private void LoadServerAssignments(Guid moduleId)
        {
            using var db = new AppDbContext();
            var servers = db.Servers.ToList();
            var assignments = db.ModuleAssignments.Where(ma => ma.ExtensionSourceId == moduleId).ToList();

            var viewModels = new List<ServerAssignmentViewModel>();

            foreach (var server in servers)
            {
                var assignment = assignments.FirstOrDefault(a => a.ServerId == server.Id);
                viewModels.Add(new ServerAssignmentViewModel
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    IsEnabled = assignment?.IsEnabled ?? false
                });
            }

            ServerAssignmentList.ItemsSource = viewModels;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_selectedModule == null) return;
            if (sender is ToggleSwitch toggle && toggle.Tag is Guid serverId)
            {
                SaveAssignment(serverId, _selectedModule.Id, toggle.IsOn);
            }
        }

        private void SaveAssignment(Guid serverId, Guid moduleId, bool isEnabled)
        {
            using var db = new AppDbContext();
            
            var assignment = db.ModuleAssignments
                .FirstOrDefault(ma => ma.ExtensionSourceId == moduleId && ma.ServerId == serverId);

            if (assignment == null)
            {
                if (isEnabled)
                {
                    db.ModuleAssignments.Add(new ModuleAssignment
                    {
                        ServerId = serverId,
                        ExtensionSourceId = moduleId,
                        IsEnabled = true
                    });
                }
            }
            else
            {
                assignment.IsEnabled = isEnabled;
                db.ModuleAssignments.Update(assignment);
            }

            db.SaveChanges();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
             if (_selectedModule == null) return;

             // Confirmation Alert (Optional, skipping for brevity but recommended)

             using var db = new AppDbContext();
             var moduleToDelete = db.ExtensionSources.Find(_selectedModule.Id);
             
             if (moduleToDelete != null)
             {
                 // 1. Remove from DB (Assignments will cascade delete if configured, else manual cleanup)
                 // Manually cleaning assignments just in case
                 var assignments = db.ModuleAssignments.Where(ma => ma.ExtensionSourceId == moduleToDelete.Id);
                 db.ModuleAssignments.RemoveRange(assignments);
                 
                 db.ExtensionSources.Remove(moduleToDelete);
                 await db.SaveChangesAsync();

                 // 2. Remove file from disk
                 if (!string.IsNullOrEmpty(moduleToDelete.LocalPath) && System.IO.File.Exists(moduleToDelete.LocalPath))
                 {
                     try 
                     {
                         System.IO.File.Delete(moduleToDelete.LocalPath);
                     }
                     catch (Exception ex)
                     {
                         // Handle or log file lock issues
                         System.Diagnostics.Debug.WriteLine($"Error deleting file: {ex.Message}");
                     }
                 }
             }

             // Refresh Grid
             LoadModules();
             _selectedModule = null;
             DetailHeader.Visibility = Visibility.Collapsed;
             NoSelectionText.Visibility = Visibility.Visible;
             ServerAssignmentList.ItemsSource = null;
        }
        
        // Method proposed for TerminalPage consumption (as requested by prompt)
        public static List<ExtensionSource> GetEnabledModulesForServer(Guid serverId)
        {
            using var db = new AppDbContext();
            
            // Query extensions that have an assignment for this server with IsEnabled = true
            var query = from ext in db.ExtensionSources
                        join assign in db.ModuleAssignments on ext.Id equals assign.ExtensionSourceId
                        where assign.ServerId == serverId && assign.IsEnabled
                        select ext;
                        
            return query.ToList();
        }
    }
}
