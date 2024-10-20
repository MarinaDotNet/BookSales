namespace BookShop.WebApp.Services;

/// <summary>
/// Static class that holds API endpoints for the application.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Base URL for the Book API. This should be set to the root URL of the Book API service.
    /// </summary>
    public static string BaseBookApiUrl {  get; set; } = string.Empty!;

    /// <summary>
    /// Static class that contains endpoints related to books.
    /// </summary>
    public static class Books
    {
        /// <summary>
        /// Gets the endpoint for retrieving all books.
        /// </summary>
        /// <value>A string representing the URL to fetch all books.</value>
        public static string GetAll => $"{BaseBookApiUrl}/all";
    }
}
