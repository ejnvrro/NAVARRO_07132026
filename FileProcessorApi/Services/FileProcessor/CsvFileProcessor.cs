using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using FileProcessorApi.Models;

namespace FileProcessorApi.Services.FileProcessor;

public class CsvFileProcessor : IFileProcessor
{
    private readonly ILogger<CsvFileProcessor> _logger;

    public CsvFileProcessor(ILogger<CsvFileProcessor> logger)
    {
        _logger = logger;
    }

    public string SupportedExtension => ".csv";

    public async Task<FileProcessingResult> ProcessAsync(IFormFile file, string parameter, CancellationToken ct)
    {
        var column = string.IsNullOrWhiteSpace(parameter) ? "Amount" : parameter;
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

        return new FileProcessingResult(
            FileName: file.FileName,
            Processor: "csv-average",
            RecordCount: rowCount,
            Result: new { Column = column, Average = Math.Round(values.Average(), 4) },
            ProcessingTimeMs: stopwatch.ElapsedMilliseconds);
    }
}