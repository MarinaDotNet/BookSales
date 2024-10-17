namespace ApiUtilities.Services.ApiKey;

/// <summary>
/// Defines a contract for validating API keys used for accessing secured API endpoints.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates the provided API key.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>
    /// A boolean value indicating whether the API key is valid.
    /// </returns>
    bool IsValid(string apiKey);
}
