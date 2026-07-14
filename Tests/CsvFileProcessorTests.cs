using FileProcessorApi.Services;
using FileProcessorApi.Services.FileProcessor;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileProcessorApi.Tests;

public class CsvFileProcessorTests
{
    private readonly CsvFileProcessor _processor = new(NullLogger<CsvFileProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_ValidCsv_ReturnsCorrectAverage()
    {
        var file = TestHelpers.CreateFile("test.csv", "Name,Amount\nAlice,100\nBob,200\nCarol,300");

        var result = await _processor.ProcessAsync(file, "Amount", CancellationToken.None);

        Assert.Equal(3, result.RecordCount);
        var summary = result.Result;
        Assert.Equal(200d, (double)summary.GetType().GetProperty("Average")!.GetValue(summary)!);
    }

    [Fact]
    public async Task ProcessAsync_MissingColumn_Throws()
    {
        var file = TestHelpers.CreateFile("test.csv", "Name,Amount\nAlice,100");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(file, "Price", CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_NonNumericRows_AreSkipped()
    {
        var file = TestHelpers.CreateFile("test.csv", "Name,Amount\nAlice,100\nBob,not-a-number\nCarol,300");

        var result = await _processor.ProcessAsync(file, "Amount", CancellationToken.None);

        Assert.Equal(3, result.RecordCount);
        var summary = result.Result;
        Assert.Equal(200d, (double)summary.GetType().GetProperty("Average")!.GetValue(summary)!);
    }

    [Fact]
    public async Task ProcessAsync_NoNumericValues_Throws()
    {
        var file = TestHelpers.CreateFile("test.csv", "Name,Amount\nAlice,abc");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(file, "Amount", CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_EmptyParameter_DefaultsToAmount()
    {
        var file = TestHelpers.CreateFile("test.csv", "Name,Amount\nAlice,50");

        var result = await _processor.ProcessAsync(file, "", CancellationToken.None);

        Assert.Equal(1, result.RecordCount);
    }
}