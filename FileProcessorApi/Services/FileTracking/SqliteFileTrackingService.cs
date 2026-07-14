using FileProcessorApi.Data;
using FileProcessorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FileProcessorApi.Services.FileTracking;

public class SqliteFileTrackingService : IFileTrackingService
{
    private readonly FileTrackingDbContext _db;

    public SqliteFileTrackingService(FileTrackingDbContext db)
    {
        _db = db;
    }

    public async Task TrackAsync(ProcessedFileRecord record, CancellationToken ct = default)
    {
        _db.ProcessedFiles.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FileReport> GetReportAsync(CancellationToken ct = default)
    {
        var files = await _db.ProcessedFiles
            .AsNoTracking()
            .OrderByDescending(f => f.ProcessedAtUtc)
            .ToListAsync(ct);

        return new FileReport(
            TotalFilesProcessed: files.Count,
            SuccessfulFiles: files.Count(f => f.Success),
            FailedFiles: files.Count(f => !f.Success),
            Files: files);
    }
}