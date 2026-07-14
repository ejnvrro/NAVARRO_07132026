using System.Security.Cryptography;
using System.Text;
using FileProcessorApi.Services;
using Microsoft.Extensions.Configuration;

namespace FileProcessorApi.Tests;

public class ApiKeyValidatorTests
{
    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static ApiKeyValidator CreateValidator(params (string Name, string Key)[] clients)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < clients.Length; i++)
        {
            settings[$"ApiClients:{i}:Name"] = clients[i].Name;
            settings[$"ApiClients:{i}:KeyHash"] = Hash(clients[i].Key);
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ApiKeyValidator(config);
    }

    [Fact]
    public void Validate_CorrectKey_ReturnsClientName()
    {
        var validator = CreateValidator(("demo-client", "correct-key"));

        var result = validator.Validate("correct-key");

        Assert.Equal("demo-client", result);
    }

    [Fact]
    public void Validate_WrongKey_ReturnsNull()
    {
        var validator = CreateValidator(("demo-client", "correct-key"));

        Assert.Null(validator.Validate("wrong-key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingKey_ReturnsNull(string? key)
    {
        var validator = CreateValidator(("demo-client", "correct-key"));

        Assert.Null(validator.Validate(key!));
    }

    [Fact]
    public void Validate_MultipleClients_IdentifiesCorrectOne()
    {
        var validator = CreateValidator(
            ("client-a", "key-a"),
            ("client-b", "key-b"));

        Assert.Equal("client-b", validator.Validate("key-b"));
    }

    [Fact]
    public void Validate_NoClientsConfigured_ReturnsNull()
    {
        var validator = CreateValidator();

        Assert.Null(validator.Validate("any-key"));
    }
}