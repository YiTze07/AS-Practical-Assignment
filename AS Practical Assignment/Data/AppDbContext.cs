using AS_Practical_Assignment.Models;
using AS_Practical_Assignment.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace AS_Practical_Assignment.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // --- Tables ---
        public DbSet<Member> Members { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PasswordHistory> PasswordHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enforce unique constraint on Email
            modelBuilder.Entity<Member>()
                .HasIndex(m => m.Email)
                .IsUnique()
                .HasName("IX_Members_Email_Unique");

            // Relationship: Member -> AuditLogs (one-to-many)
            modelBuilder.Entity<AuditLog>()
                .HasOne<Member>(a => a.Member)
                .WithMany(m => m.AuditLogs)
                .HasForeignKey(a => a.MemberId);

            // Relationship: Member -> PasswordHistories (one-to-many)
            modelBuilder.Entity<PasswordHistory>()
                .HasOne<Member>(ph => ph.Member)
                .WithMany(m => m.PasswordHistories)
                .HasForeignKey(ph => ph.MemberId);

            base.OnModelCreating(modelBuilder);
        }
    }
}