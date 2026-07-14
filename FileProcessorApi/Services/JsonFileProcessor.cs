using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FileProcessorApi.Models;

namespace FileProcessorApi.Services;

public class JsonFileProcessor : IFileProcessor
{
    private static readonly char[] Operators = ['>', '<', '='];

    public string SupportedExtension => ".json";

    public async Task<FileProcessingResult> ProcessAsync(IFormFile file, string parameter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new InvalidDataException(
                "A filter expression is required for JSON files, e.g. 'Amount>100' or 'Name=Alice'.");
        }

        var (field, op, value) = ParseFilter(parameter);
        var stopwatch = Stopwatch.StartNew();

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(file.OpenReadStream(), cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The file is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The JSON root must be an array of objects.");
            }

            var matches = new List<JsonElement>();
            var total = 0;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                total++;

                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty(field, out var prop) &&
                    Matches(prop, op, value))
                {
                    matches.Add(element.Clone());
                }
            }

            stopwatch.Stop();

            return new FileProcessingResult(
                FileName: file.FileName,
                Processor: "json-filter",
                RecordCount: total,
                Result: new { Filter = parameter, MatchCount = matches.Count, Items = matches },
                ProcessingTimeMs: stopwatch.ElapsedMilliseconds);
        }
    }

    private static (string Field, char Op, string Value) ParseFilter(string filter)
    {
        var opIndex = filter.IndexOfAny(Operators);
        if (opIndex <= 0 || opIndex == filter.Length - 1)
        {
            throw new InvalidDataException(
                $"Invalid filter '{filter}'. Expected format: field>value, field<value, or field=value.");
        }

        return (filter[..opIndex].Trim(), filter[opIndex], filter[(opIndex + 1)..].Trim());
    }

    private static bool Matches(JsonElement prop, char op, string value)
    {
        // Numeric comparison when both sides are numeric
        if (prop.ValueKind == JsonValueKind.Number &&
            double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var target))
        {
            var actual = prop.GetDouble();
            return op switch
            {
                '>' => actual > target,
                '<' => actual < target,
                '=' => Math.Abs(actual - target) < 0.000001,
                _ => false
            };
        }

        // String comparison only supports equality
        if (prop.ValueKind == JsonValueKind.String && op == '=')
        {
            return string.Equals(prop.GetString(), value, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}