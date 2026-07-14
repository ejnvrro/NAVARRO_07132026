using FileProcessorApi.Models;

namespace FileProcessorApi.Services;

public interface IFileProcessor
{
    /// <summary>File extension this processor handles, including the dot (e.g. ".csv").</summary>
    string SupportedExtension { get; }

    /// <summary>
    /// Processes the uploaded file. The meaning of <paramref name="parameter"/> depends on the processor:
    /// for CSV it is the column to average, for JSON it is a filter expression like "Amount>100".
    /// </summary>
    Task<FileProcessingResult> ProcessAsync(IFormFile file, string parameter, CancellationToken ct);
}