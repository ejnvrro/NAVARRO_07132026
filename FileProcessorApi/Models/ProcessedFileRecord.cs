namespace FileProcessorApi.Models
{
    public record ProcessedFileRecord(
        string FileName,
        DateTime ProcessedAtUtc,
        long ProcessingTimeMs,
        int RowCount,
        bool Success,
        string? ErrorMessage);
}
