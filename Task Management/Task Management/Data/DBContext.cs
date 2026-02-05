using Microsoft.EntityFrameworkCore;
using System;
using Task_Management.Models;

namespace Task_Management.Data
{
    public class TaskManagementDbContext : DbContext
    {
        public DbSet<StatusTask> TaskStatuses { get; set; }
        public DbSet<TaskPriority> TaskPriorities { get; set; }
        public DbSet<CurrentTask> CurrentTasks { get; set; }
        public DbSet<ArchivedTask> ArchivedTasks { get; set; }
        
        
        public TaskManagementDbContext(DbContextOptions<TaskManagementDbContext> options)
            : base(options)
        {
        }
    }
}
