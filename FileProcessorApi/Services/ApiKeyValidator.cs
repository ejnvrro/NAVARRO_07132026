using System.Security.Cryptography;
using System.Text;
using FileProcessorApi.Models;

namespace FileProcessorApi.Services;

public interface IApiKeyValidator
{
    /// <summary>Returns the client name if the key is valid, otherwise null.</summary>
    string? Validate(string providedKey);
}

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly List<ApiClient> _clients;

    public ApiKeyValidator(IConfiguration configuration)
    {
        _clients = configuration.GetSection("ApiClients").Get<List<ApiClient>>() ?? [];
    }

    public string? Validate(string providedKey)
    {
        if (string.IsNullOrEmpty(providedKey))
            return null;

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));

        foreach (var client in _clients)
        {
            byte[] storedHash;
            try
            {
                storedHash = Convert.FromHexString(client.KeyHash);
            }
            catch (FormatException)
            {
                continue; // malformed hash in config, skip
            }

            if (CryptographicOperations.FixedTimeEquals(providedHash, storedHash))
            {
                return client.Name;
            }
        }

        return null;
    }
}