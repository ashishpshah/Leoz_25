using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Leoz_25.Models_Temp;

public partial class PadhyasoLeozContext : DbContext
{
    public PadhyasoLeozContext()
    {
    }

    public PadhyasoLeozContext(DbContextOptions<PadhyasoLeozContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ProjectDailyUpdate> ProjectDailyUpdates { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DataConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("padhyaso_Leoz");

        modelBuilder.Entity<ProjectDailyUpdate>(entity =>
        {
            entity.ToTable("Project_Daily_Update", "dbo");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
