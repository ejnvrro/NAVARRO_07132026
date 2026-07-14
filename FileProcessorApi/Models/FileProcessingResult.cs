namespace FileProcessorApi.Models;

public record FileProcessingResult(
    string FileName,
    string Processor,
    int RecordCount,
    object Result,
    long ProcessingTimeMs);