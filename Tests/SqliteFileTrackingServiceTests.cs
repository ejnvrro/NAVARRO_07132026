using FileProcessorApi.Data;
using FileProcessorApi.Models;
using FileProcessorApi.Services;
using FileProcessorApi.Services.FileTracking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FileProcessorApi.Tests;

public class SqliteFileTrackingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FileTrackingDbContext _db;
    private readonly SqliteFileTrackingService _service;

    public SqliteFileTrackingServiceTests()
    {
        // In-memory SQLite lives as long as the connection stays open
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FileTrackingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FileTrackingDbContext(options);
        _db.Database.EnsureCreated();
        _service = new SqliteFileTrackingService(_db);
    }

    [Fact]
    public async Task TrackAsync_ThenReport_ReturnsRecord()
    {
        await _service.TrackAsync(new ProcessedFileRecord
        {
            FileName = "test.csv",
            ProcessedAtUtc = DateTime.UtcNow,
            RecordCount = 3,
            Success = true,
            ClientName = "demo-client"
        });

        var report = await _service.GetReportAsync();

        Assert.Equal(1, report.TotalFilesProcessed);
        Assert.Equal(1, report.SuccessfulFiles);
        Assert.Equal(0, report.FailedFiles);
        Assert.Equal("demo-client", report.Files.First().ClientName);
    }

    [Fact]
    public async Task GetReportAsync_CountsSuccessAndFailureSeparately()
    {
        await _service.TrackAsync(new ProcessedFileRecord
        { FileName = "good.csv", ProcessedAtUtc = DateTime.UtcNow, Success = true, ClientName = "a" });
        await _service.TrackAsync(new ProcessedFileRecord
        {
            FileName = "bad.csv",
            ProcessedAtUtc = DateTime.UtcNow,
            Success = false,
            ErrorMessage = "boom",
            ClientName = "a"
        });

        var report = await _service.GetReportAsync();

        Assert.Equal(2, report.TotalFilesProcessed);
        Assert.Equal(1, report.SuccessfulFiles);
        Assert.Equal(1, report.FailedFiles);
    }

    [Fact]
    public async Task GetReportAsync_EmptyDatabase_ReturnsZeroCounts()
    {
        var report = await _service.GetReportAsync();

        Assert.Equal(0, report.TotalFilesProcessed);
        Assert.Empty(report.Files);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}