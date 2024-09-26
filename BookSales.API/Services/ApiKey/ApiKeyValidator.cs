namespace BooksStock.API.Services.ApiKey;

/// <summary>
/// Provides validation logic for API keys used to secure API endpoints.
/// </summary>
/// <param name="configuration">An instance of <see cref="IConfiguration"/> used to retrieve the API key 
/// from the configuration settings.</param>
/// <exception cref="ArgumentNullException">Thrown when the configuration is null.</exception>
public class ApiKeyValidator(IConfiguration configuration, ILogger<ApiKeyValidator> logger) : IApiKeyValidator
{
    private readonly IConfiguration _configuration = configuration ??
        throw new ArgumentNullException(nameof(configuration), "IConfiguration should not be null or empty.");
    private readonly ILogger<ApiKeyValidator> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "Logger should not be null or empty.");

    public bool IsValid(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        try
        {
            string keyConfig = _configuration.GetValue<string>("StockApiKey")!;

            // Ensure the config key is valid and return the comparison result
            return !string.IsNullOrWhiteSpace(keyConfig) && apiKey.Equals(keyConfig);
        }
        catch (Exception ex)
        {
            {
                _logger.LogError(ex, "Error validating API key.");
                return false;
            }
        }
    }
}
