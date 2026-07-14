using FileProcessorApi.Services;
using FileProcessorApi.Services.FileProcessor;

namespace FileProcessorApi.Tests;

public class JsonFileProcessorTests
{
    private readonly JsonFileProcessor _processor = new();

    private const string SampleJson = """
        [
          { "Name": "Alice", "Amount": 100 },
          { "Name": "Bob", "Amount": 250.5 },
          { "Name": "Carol", "Amount": 49.5 }
        ]
        """;

    [Theory]
    [InlineData("Amount>100", 1)]
    [InlineData("Amount<200", 2)]
    [InlineData("Amount>0", 3)]
    [InlineData("Amount>999", 0)]
    public async Task ProcessAsync_NumericFilters_ReturnExpectedCounts(string filter, int expected)
    {
        var file = TestHelpers.CreateFile("test.json", SampleJson);

        var result = await _processor.ProcessAsync(file, filter, CancellationToken.None);

        var matchCount = (int)result.Result.GetType().GetProperty("MatchCount")!.GetValue(result.Result)!;
        Assert.Equal(expected, matchCount);
    }

    [Fact]
    public async Task ProcessAsync_StringEquality_Matches()
    {
        var file = TestHelpers.CreateFile("test.json", SampleJson);

        var result = await _processor.ProcessAsync(file, "Name=Alice", CancellationToken.None);

        var matchCount = (int)result.Result.GetType().GetProperty("MatchCount")!.GetValue(result.Result)!;
        Assert.Equal(1, matchCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Amount")]
    [InlineData(">100")]
    [InlineData("Amount>")]
    public async Task ProcessAsync_InvalidFilter_Throws(string filter)
    {
        var file = TestHelpers.CreateFile("test.json", SampleJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(file, filter, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_InvalidJson_Throws()
    {
        var file = TestHelpers.CreateFile("test.json", "{ not valid json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(file, "Amount>0", CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_RootNotArray_Throws()
    {
        var file = TestHelpers.CreateFile("test.json", """{ "Name": "Alice" }""");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(file, "Amount>0", CancellationToken.None));
    }
}