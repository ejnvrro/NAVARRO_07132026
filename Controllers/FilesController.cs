using FileProcessorApi.Models;
using FileProcessorApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessorApi.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ICsvProcessingService _csvService;
    private readonly IFileTrackingService _trackingService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        ICsvProcessingService csvService,
        IFileTrackingService trackingService,
        ILogger<FilesController> logger)
    {
        _csvService = csvService;
        _trackingService = trackingService;
        _logger = logger;
    }

    /// <summary>Uploads a CSV file and returns the average of the specified column.</summary>
    [HttpPost("process")]
    [ProducesResponseType(typeof(CsvProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessFile(
        IFormFile file,
        [FromQuery] string column = "Amount",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 10 MB size limit." });

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .csv files are supported." });

        try
        {
            var result = await _csvService.ProcessAsync(file, column, ct);

            _trackingService.Track(new ProcessedFileRecord(
                file.FileName, DateTime.UtcNow, result.ProcessingTimeMs, result.RowCount,
                Success: true, ErrorMessage: null));

            _logger.LogInformation("Processed {FileName}: {Rows} rows in {Ms} ms",
                file.FileName, result.RowCount, result.ProcessingTimeMs);

            return Ok(result);
        }
        catch (InvalidDataException ex)
        {
            _trackingService.Track(new ProcessedFileRecord(
                file.FileName, DateTime.UtcNow, 0, 0, Success: false, ErrorMessage: ex.Message));

            _logger.LogWarning(ex, "Failed to process {FileName}", file.FileName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns a report of all files processed since the service started.</summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(FileReport), StatusCodes.Status200OK)]
    public IActionResult GetReport() => Ok(_trackingService.GetReport());
}