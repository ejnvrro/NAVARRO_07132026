namespace FileProcessorApi.Models;

public class ProcessedFileRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
    public long ProcessingTimeMs { get; set; }
    public int RecordCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ClientName { get; set; } = string.Empty;
}