using BooksStock.API.Services.ApiKey;
using System.Net;

namespace BooksStock.API.Services;

/// <summary>
/// Middleware for validating the API key and API version from the incoming request.
/// Ensures that the request contains a valid API key and version before proceeding to the next middleware.
/// </summary>
/// <param name="next">The next middleware in the HTTP request pipeline.</param>
/// <param name="apiKeyValidator">The service for validating the API key.</param>
/// <param name="logger">The logger used to log error messages and information.</param>
/// <exception cref="ArgumentNullException">
/// Thrown when the <paramref name="next"/>, <paramref name="apiKeyValidator"/>, or <paramref name="logger"/> is null.
/// </exception>
public class ApiMiddleware(RequestDelegate next, IApiKeyValidator apiKeyValidator, ILogger<ApiMiddleware> logger)
{
    private readonly RequestDelegate _next = next ??
        throw new ArgumentNullException(nameof(next), "Request cannot be null.");  
    private readonly IApiKeyValidator _apiKeyValidator = apiKeyValidator ??
        throw new ArgumentNullException(nameof(apiKeyValidator), "IApiKeyValidator cannot be null.");
    private readonly ILogger<ApiMiddleware> _logger = logger ?? 
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");

    /// <summary>
    /// Processes the incoming request by checking the API key and version.
    /// If the API key or version is invalid, an error response is returned.
    /// Otherwise, the request is passed to the next middleware.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> representing the current HTTP request.</param>
    /// <returns>A task that represents the asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Check if the StockApiKey header is present
            if (!context.Request.Headers.TryGetValue("StockApiKey", out var userApiKey) ||
                string.IsNullOrWhiteSpace(userApiKey))
            {
               await LogError((int)HttpStatusCode.BadRequest, "Missing Stock API key.", context);
                return;
            }

            // Validate the API key format (ensure it's a valid version: 1, 2, or 3)
            if (!context.Request.Headers.TryGetValue("Api-Version", out var apiVersionHeader) &&
                !decimal.TryParse(apiVersionHeader, out decimal version) &&
                (version != 1 && version != 2 && version != 3))
            {
               await LogError((int)HttpStatusCode.ExpectationFailed, "Invalid or missing API version.", context);
                return;
            }

            // Validate the provided API key
            if (!_apiKeyValidator.IsValid(userApiKey!))
            {
               await LogError((int)HttpStatusCode.Unauthorized, "Unauthorized: Invalid API key.", context);
                return;
            }

            // Proceed with the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the exception and return a 500 Internal Server Error response
          await  LogError((int)HttpStatusCode.InternalServerError, $"Server error: {ex.Message}", context);
            return;
        }
    }

    /// <summary>
    /// Logs an error and writes a structured error response to the HTTP context.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the error.</param>
    /// <param name="message">The error message to include in the response.</param>
    /// <param name="context">The <see cref="HttpContext"/> to which the error response is written.</param>
    /// <returns>A task that represents the asynchronous logging and response writing operation.</returns>
    private async Task LogError(int statusCode, string message, HttpContext context)
    {
        context.Response.Clear();

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new {StatusCode = statusCode, Message = message};
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));

        _logger.LogError("Error {StatusCode}: {Message} at {Path}", statusCode, message, context.Request.Path);
    }
}
