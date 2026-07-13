namespace FileProcessorApi.Models
{
    public record CsvProcessingResult(
        string FileName,
        string Column,
        int RowCount,
        double Average,
        long ProcessingTimeMs);
}
