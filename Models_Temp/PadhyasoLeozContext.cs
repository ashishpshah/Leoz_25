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

    public virtual DbSet<CustomerProjectMapping> CustomerProjectMappings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=103.143.46.143;Initial Catalog=Padhyaso_Leoz;User ID=Padhyaso_Leoz;Password=1E*t0y5j4NyHeqtrr;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("padhyaso_Leoz");

        modelBuilder.Entity<CustomerProjectMapping>(entity =>
        {
            entity.ToTable("CustomerProjectMapping", "dbo");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
