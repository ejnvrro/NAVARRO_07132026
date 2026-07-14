using FileProcessorApi.Models;
using FileProcessorApi.Services.FileProcessor;
using FileProcessorApi.Services.FileTracking;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessorApi.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IEnumerable<IFileProcessor> _processors;
    private readonly IFileTrackingService _trackingService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IEnumerable<IFileProcessor> processors,
        IFileTrackingService trackingService,
        ILogger<FilesController> logger)
    {
        _processors = processors;
        _trackingService = trackingService;
        _logger = logger;
    }

    /// <param name="parameter">
    /// CSV: column to average (defaults to "Amount").
    /// JSON: filter expression, e.g. "Amount&gt;100" or "Name=Alice" (required).
    /// </param>
    [HttpPost("process")]
    [ProducesResponseType(typeof(FileProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessFile(
        IFormFile file,
        [FromQuery] string? parameter = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 10 MB size limit." });

        var extension = Path.GetExtension(file.FileName);
        var processor = _processors.FirstOrDefault(p =>
            p.SupportedExtension.Equals(extension, StringComparison.OrdinalIgnoreCase));

        if (processor is null)
        {
            var supported = string.Join(", ", _processors.Select(p => p.SupportedExtension));
            return BadRequest(new { error = $"Unsupported file type '{extension}'. Supported: {supported}." });
        }

        try
        {
            var result = await processor.ProcessAsync(file, parameter, ct);

            await _trackingService.TrackAsync(new ProcessedFileRecord
            {
                FileName = file.FileName,
                ProcessedAtUtc = DateTime.UtcNow,
                ProcessingTimeMs = result.ProcessingTimeMs,
                RecordCount = result.RecordCount,
                Success = true
            }, ct);

            _logger.LogInformation("Processed {FileName} with {Processor}: {Records} records in {Ms} ms",
                file.FileName, result.Processor, result.RecordCount, result.ProcessingTimeMs);

            return Ok(result);
        }
        catch (InvalidDataException ex)
        {
            await _trackingService.TrackAsync(new ProcessedFileRecord
            {
                FileName = file.FileName,
                ProcessedAtUtc = DateTime.UtcNow,
                Success = false,
                ErrorMessage = ex.Message
            }, ct);

            _logger.LogWarning(ex, "Failed to process {FileName}", file.FileName);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("report")]
    [ProducesResponseType(typeof(FileReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(CancellationToken ct = default) =>
        Ok(await _trackingService.GetReportAsync(ct));
}