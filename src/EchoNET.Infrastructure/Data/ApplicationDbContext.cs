using System;
using System.Collections.Generic;
using System.Text;
using EchoNET.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoNET.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(20);
            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.Bio).HasMaxLength(280);
        });
    }
}