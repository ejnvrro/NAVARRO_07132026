using FileProcessorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FileProcessorApi.Data;

public class FileTrackingDbContext : DbContext
{
    public FileTrackingDbContext(DbContextOptions<FileTrackingDbContext> options)
        : base(options) { }

    public DbSet<ProcessedFileRecord> ProcessedFiles => Set<ProcessedFileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedFileRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
            entity.HasIndex(e => e.ProcessedAtUtc);
        });
    }
}