using FileProcessorApi.Models;

namespace FileProcessorApi.Services.FileTracking;

public interface IFileTrackingService
{
    Task TrackAsync(ProcessedFileRecord record, CancellationToken ct = default);
    Task<FileReport> GetReportAsync(CancellationToken ct = default);
}