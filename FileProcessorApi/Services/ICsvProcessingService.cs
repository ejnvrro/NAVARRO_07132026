using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using FileProcessorApi.Models;

namespace FileProcessorApi.Services;

public interface ICsvProcessingService
{
    Task<CsvProcessingResult> ProcessAsync(IFormFile file, string column, CancellationToken ct);
}

public class CsvProcessingService : ICsvProcessingService
{
    private readonly ILogger<CsvProcessingService> _logger;

    public CsvProcessingService(ILogger<CsvProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<CsvProcessingResult> ProcessAsync(IFormFile file, string column, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await csv.ReadAsync();
        csv.ReadHeader();

        if (csv.HeaderRecord is null || !csv.HeaderRecord.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Column '{column}' was not found in the CSV header.");
        }

        var values = new List<double>();
        var rowCount = 0;

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            rowCount++;

            var raw = csv.GetField(column);
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(value);
            }
            else
            {
                _logger.LogWarning("Row {Row}: value '{Value}' in column '{Column}' is not numeric, skipping",
                    rowCount, raw, column);
            }
        }

        if (values.Count == 0)
        {
            throw new InvalidDataException($"No numeric values found in column '{column}'.");
        }

        stopwatch.Stop();

        return new CsvProcessingResult(
            FileName: file.FileName,
            Column: column,
            RowCount: rowCount,
            Average: Math.Round(values.Average(), 4),
            ProcessingTimeMs: stopwatch.ElapsedMilliseconds);
    }
}