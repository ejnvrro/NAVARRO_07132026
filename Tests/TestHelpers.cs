using Microsoft.AspNetCore.Http;
using System.Text;

namespace FileProcessorApi.Tests;

public static class TestHelpers
{
    public static IFormFile CreateFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }
}