namespace FileProcessorApi.Models
{
    public record FileReport(
        int TotalFilesProcessed,
        int SuccessfulFiles,
        int FailedFiles,
        IReadOnlyCollection<ProcessedFileRecord> Files);
}
