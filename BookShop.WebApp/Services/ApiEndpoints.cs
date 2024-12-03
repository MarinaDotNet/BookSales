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
    /// Base URL for the AuthAccount API. This should be set to the root URL of the AuthAccount API service.
    /// </summary>
    public static string BaseAccountApiUrl { get; set; } = string.Empty!;

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

        /// <summary>
        /// Gets the total amount of all books.
        /// </summary>
        /// <value>
        /// The get count all books.
        /// </value>
        public static string GetCountAll => $"{BaseBookApiUrl}/all/count";

        /// <summary>
        /// Gets the get all genres.
        /// </summary>
        /// <value>
        /// The get all genres.
        /// </value>
        public static string GetAllGenres => $"{BaseBookApiUrl}/all/genres";

        /// <summary>
        /// Searches the specified expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="isAscending">if set to <c>true</c> [is ascending].</param>
        /// <returns></returns>
        public static string Search(string expression, bool isAscending) =>
            $"{BaseBookApiUrl}/all/partialmatch/{Uri.EscapeDataString(expression)}?ascendingOrder={isAscending.ToString().ToLower()}";

        /// <summary>
        /// Gets the count for search.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static string GetCountForSearch(string expression) =>
            $"{BaseBookApiUrl}/books/count/partialmatch/{Uri.EscapeDataString(expression)}";
    }

    /// <summary>
    /// Static class that contains endpoints-related authorization in user account.
    /// </summary>
    public static class Account
    {
        /// <summary>
        /// Gets the  endpoint for retieving JWT token data.
        /// </summary>
        /// <value>A string representing the URL to fetch JWT token data.</value>
        public static string GetLoginToken => $"{BaseAccountApiUrl}/account/login";

        /// <summary>Gets the send register request.</summary>
        /// <value>The send register request.</value>
        public static string SendRegisterRequest => $"{BaseAccountApiUrl}/new";

        /// <summary>Gets the re send confirmation request.</summary>
        /// <value>The re send confirmation request.</value>
        public static string ReSendConfirmationRequest => $"{BaseAccountApiUrl}/account/confirmemail/resend";

        /// <summary>Gets the send password change request.</summary>
        /// <value>The send password change request.</value>
        public static string SendPasswordChangeRequest => $"{BaseAccountApiUrl}/account/password/reset";


        /// <summary>Gets the send update account request.</summary>
        /// <value>The send update account request.</value>
        public static string SendUpdateAccountRequest => $"{BaseAccountApiUrl}/account/update";

        /// <summary>Gets the send delete account request.</summary>
        /// <value>The send delete account request.</value>
        public static string SendDeleteAccountRequest => $"{BaseAccountApiUrl}/account/delete";
    }
}
