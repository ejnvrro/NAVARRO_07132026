using System.Collections.Concurrent;
using FileProcessorApi.Models;

namespace FileProcessorApi.Services;

public interface IFileTrackingService
{
    void Track(ProcessedFileRecord record);
    FileReport GetReport();
}

public class InMemoryFileTrackingService : IFileTrackingService
{
    private readonly ConcurrentQueue<ProcessedFileRecord> _records = new();

    public void Track(ProcessedFileRecord record) => _records.Enqueue(record);

    public FileReport GetReport()
    {
        var snapshot = _records.ToArray();
        return new FileReport(
            TotalFilesProcessed: snapshot.Length,
            SuccessfulFiles: snapshot.Count(r => r.Success),
            FailedFiles: snapshot.Count(r => !r.Success),
            Files: snapshot);
    }
}