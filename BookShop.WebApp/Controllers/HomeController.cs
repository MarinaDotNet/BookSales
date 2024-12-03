using ApiUtilities.Constants;
using ApiUtilities.Models;
using BookShop.WebApp.Models;
using BookShop.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using static BookShop.WebApp.Services.ApiEndpoints;

namespace BookShop.WebApp.Controllers;

/// <summary>
/// Controller for handling home-related actions such as displaying the index page and privacy information.
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging information and errors.</param>
    /// <param name="configuration">The configuration settings for the application.</param>
    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
        _configuration = configuration;
        _logger = logger;

        //Sets the authorization headers.
        SetAuthorizationHeader();
    }

    /// <summary>
    /// Sets the authorization headers.
    /// Checks if the user token exists and is not expired, then sets the authorization headers for calling 2nd version of API;
    /// otherwise sets the headers for call 3rd version of API.
    /// </summary>
    private void SetAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add(SecurityConstants.AuthApiKey, _configuration[SecurityConstants.AuthApiKey + "Book"]);
        if (_memoryCache.TryGetValue("userToken", out (string token, string expiry, string userName, string email) userCache) &&
            !string.IsNullOrWhiteSpace(userCache.token) &&
            DateTime.TryParse(userCache.expiry, out DateTime validTill) &&
            DateTime.Now <= validTill)
        {
            _httpClient.DefaultRequestHeaders.Add("Api-Version", "2");
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + userCache.token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Add("Api-Version", "3");
        }
    }

    /// <summary>
    /// Resets the authorization headers for index view if needed.
    /// </summary>
    private void ResetAuthorizationHeadersIndex()
    {
        if(_httpClient.DefaultRequestHeaders.Count() > 3)
        {
            _httpClient.DefaultRequestHeaders.Remove("Api-Version");
            _httpClient.DefaultRequestHeaders.Add("Api-Version", "3");
        }
        
    }

    /// <summary>
    /// Sets up or updates the pagination for the <see cref="ViewModel"/> <paramref name="view"/> based on the provided parameters: 
    /// <paramref name="itemsPerPage"/> and <paramref name="page"/>
    /// </summary>
    /// <param name="view">The view model <see cref="ViewModel"/> that contains pagination data.</param>
    /// <param name="itemsPerPage">The number of items should be displayed per page. Defaults to 6 if null.</param>
    /// <param name="page">Current page number. Defaults to 1 if null.</param>
    /// <remarks>
    /// If the number of items per page changes of the <see cref="PaginationModel"/>, the cache with the key "dataToDisplay" is cleared.
    /// This ensures that outdated data is not displayed. If only the page number changes, 
    /// the existing pagination object is updated without recreating it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Throw when <paramref name="view"/> is null</exception>
    private void SetupPagination(ViewModel view, int? itemsPerPage, int? page)
    {
        ArgumentNullException.ThrowIfNull(view, "View model cannot be null.");

        if(view.Pagination is null || view.Pagination.ItemsPerPage != itemsPerPage)
        {
            //Clear cache if itemsPerPage has changed to ensure data is updated on the user's request
            if(view.Pagination!.ItemsPerPage != itemsPerPage)
            {
                _memoryCache.Remove("dataToDisplay");
            }

            //Initialize or reset the pagination with requested or with default values
            view.Pagination = new(itemsPerPage ?? 6, page ?? 1);
        }
        else 
        {
            //Only update the current page if the itemsPerPage was not requested for a change
            view.Pagination.SetCurrentPage(page ?? 1);
        }
    }

    /// <summary>
    /// Populates the specified <see cref="ViewModel"/> with genres, pagination data, and a collection of books
    /// retrieved from the provided API endpoints.
    /// </summary>
    /// <param name="view">The <see cref="ViewModel"/> to populate with data.</param>
    /// <param name="booksEndpoint">The API endpoint to fetch the book data.</param>
    /// <param name="countEndPoint">The API endpoint to fetch the total count of books (optional).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no books are available for the current page.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="view"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="booksEndpoint"/> is <c>null</c>, empty, or consists only of whitespace characters.
    /// </exception>
    private async Task PopulateViewModelAsync(ViewModel view, string booksEndpoint, string? countEndPoint)
    {
        ArgumentNullException.ThrowIfNull(view, "View model cannot be null.");
        ArgumentException.ThrowIfNullOrWhiteSpace(booksEndpoint, "Books endpoint cannot be null or empty.");

        //Fetch genres if not yet loaded
        if (view.Genres is null || view.Genres.Length <= 1)
        {
            view.Genres = (await _httpClient.GetFromJsonAsync<IEnumerable<string>>(ApiEndpoints.Books.GetAllGenres))!.ToArray();
        }

        if(view.Pagination is not null)
        {
            // Retrieve total item count from the appropriate endpoint.
            var totalItems = await _httpClient.GetFromJsonAsync<int>(countEndPoint ?? ApiEndpoints.Books.GetCountAll);
            view.Pagination.CalculateTotalPages(totalItems);

            // Fetch and cache the books data.;
            var booksToCache = await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(booksEndpoint);
            ManageDataToDisplay(booksToCache!, view);

            // Retrieve books from cache based on the current page.
            var books = GetBooksFromCache(view.Pagination.CurrentPage);

            if (books is null || books.Count == 0)
            {
                _logger.LogWarning("No books found for the current page.");
                throw new InvalidOperationException("No books available.");
            }

            // Populate the view model's product data.
            view.Product = new() { Books = books };
        }

        // Cache the updated view model.
        _memoryCache.Set("view", view);
    }

    /// <summary>
    /// Caches paginated book data retrieved from the provided collection.
    /// </summary>
    /// <param name="books">The collection of books to paginate and cache.</param>
    ///<param name="view">The <see cref="ViewModel"/> to populate with data.</param>
    /// <remarks>
    /// If the cache is valid (not expired and containing the correct number of pages), no action is taken. 
    /// Otherwise, the books are split into pages and stored in the cache for 2 hours.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no books are provided for caching.</exception>
    private void ManageDataToDisplay(IEnumerable<Book> books, ViewModel view)
    {
        // Ensure at least one page.
        int pages = view.Pagination.TotalPages < 1 ? 1 : view.Pagination.TotalPages;
        // Default to a minimum of 6 items per page.
        int itemsPerPage = view.Pagination.ItemsPerPage < 6 ? 6 : view.Pagination.ItemsPerPage;

        if (_memoryCache.TryGetValue("dataToDisplay", out (Dictionary<int, IEnumerable<Book>> dataAtPage, DateTime expiryTime) cache))
        {
            if(cache.expiryTime >= DateTime.Now && cache.dataAtPage.Count == pages)
            {
                //Cache is valid and do not need to be updated
                return;
            }

            // Remove stale cache.
            _memoryCache.Remove("dataToDisplay"); 
        }

        if(books is null || !books.Any())
        {
            _logger.LogWarning("No books found in the provided collection.");
            throw new InvalidOperationException("No books found in the provided collection.");
        }

        Dictionary<int, IEnumerable<Book>> paginatedBooks = [];

        //Order list by Price
        books = view.Filter.IsAscendingOrder ? 
            books.OrderBy(book => book.Price) : 
            books.OrderByDescending(book => book.Price);

        for (int i = 1; i <= pages; i++)
        {
            int toSkip = (i - 1) * itemsPerPage;
            paginatedBooks[i] = books.Skip(toSkip).Take(itemsPerPage);
        }

        _memoryCache.Set("dataToDisplay", (paginatedBooks, DateTime.Now.AddHours(2)));
    }

    /// <summary>
    /// Retrieves the list of books for the specified page from the cache.
    /// </summary>
    /// <param name="page">The current page number to retrieve books for. Defaults to 1 if not specified.</param>
    /// <returns>A list of <see cref="Book"/> objects for the specified page. Returns an empty list if no books are found.</returns>
    /// <remarks>
    /// This method attempts to retrieve cached paginated book data. If the cache is expired or invalid, 
    /// it returns an empty list.
    /// </remarks>
    private List<Book> GetBooksFromCache(int page = 1)
    {
        // Ensure page number is at least 1.
        page = page < 1 ? 1 : page;

        if (_memoryCache.TryGetValue("dataToDisplay", out (Dictionary<int, IEnumerable<Book>> dataAtPage, DateTime expiryTime) cache))
        {
            if (cache.expiryTime >= DateTime.Now &&
                cache.dataAtPage.TryGetValue(page, out IEnumerable<Book>? books))
            {
                return books!.ToList();
            }
        }

        _logger.LogWarning($"No data found in cache for page {page}.");
        return [];
    }

    /// <summary>
    /// Displays the index page with a list of books.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> representing the result of the action.</returns>
    public async Task<IActionResult> Index()
    {
        try
        {
            //To ensure that the 3rd version of the API will be called.
            ResetAuthorizationHeadersIndex();

            var data = await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(ApiEndpoints.Books.GetAll);

            if (data is null || !data.Any())
            {
                _logger.LogWarning("No books are found or data is null.");
                return Error();
            }

            ViewModel viewModel = new() { Product = new(){ Books = data } };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return Error();
        }
        
    }

    /// <summary>
    /// Displays the privacy policy page.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> representing the result of the action.</returns>
    [TokenAuthorizationAttribute("userToken")]
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Displays the Shop page.
    /// </summary>
    /// <param name="itemsPerPage">The items per page, default sets to six items per page.</param>
    /// <param name="page">The number of the page requested, default sets to the first page.</param>
    /// <returns>An <see cref="IActionResult"/> representing the result of the action.</returns>
    [TokenAuthorizationAttribute("userToken")]
    public async Task<IActionResult> ShopAsync(int? itemsPerPage, int? page)
    {
        try
        {
            //Geting cached value
            if(!_memoryCache.TryGetValue("view", out ViewModel? view))
            {
                view = new ViewModel();
            }

            //Set pagination
            SetupPagination(view!, itemsPerPage, page);

            //Fetch data and update the ViewModel
            await PopulateViewModelAsync(view!, ApiEndpoints.Books.GetAll, null);

            return View(view);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return Error();
        }
        
    }

    /// <summary>
    /// Searches for books based on a given search term, sort order, and pagination settings.
    /// Updates the view model with the search results.
    /// </summary>
    /// <param name="searchTerm">The term used to search for books.</param>
    /// <param name="isAscendingOrder">Indicates whether the books should be sorted in ascending order by price.</param>
    /// <param name="itemsPerPage">Optional number of items to display per page.</param>
    /// <param name="page">Optional current page number for pagination.</param>
    /// <returns>An IActionResult that displays the search results or an error view if an exception occurs.</returns>
    [TokenAuthorizationAttribute("userToken")]
    public async Task<IActionResult> SearchBooksAsync(string searchTerm, bool isAscendingOrder, int? itemsPerPage, int? page)
    {
        try
        {
            // Retrieve cached view model or initialize a new one.
            if (!_memoryCache.TryGetValue("view", out ViewModel? view))
            {
                view = new ViewModel();
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = view!.Filter.SearchTearm ??
                    throw new ArgumentNullException(searchTerm, "The search term cannot be null or empty.");
            }
            else
            {
                view!.Filter.SearchTearm = searchTerm;
            }

            // Update sort order and handle genre selection.
            view.Filter.IsAscendingOrder = isAscendingOrder;
            view.Filter.SelectedGenre = page is null ? "undefined" : view.Filter.SelectedGenre;

            //Set pagination
            SetupPagination(view!, itemsPerPage, page);

            // TODO: Restrict genres to those matching the search term.
            await PopulateViewModelAsync(view, 
                ApiEndpoints.Books.Search(searchTerm, isAscendingOrder), 
                ApiEndpoints.Books.GetCountForSearch(searchTerm));

            return View(view);
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, $"An unexpected error occurred during the search with term '{searchTerm}'.");
            return Error();
        }
    }

    /// <summary>
    /// Filters books based on the genre, sort order, and pagination settings.
    /// Updates the view model with the filtered results.
    /// </summary>
    /// <param name="genre">The genre to filter books by.</param>
    /// <param name="isAscendingOrder">Determines if the books should be ordered by ascending price.</param>
    /// <param name="itemsPerPage">Optional number of items per page.</param>
    /// <param name="page">Optional current page number for pagination.</param>
    /// <returns>A view with the filtered book data or an error view in case of an exception.</returns>
    [TokenAuthorizationAttribute("userToken")]
    public async Task<IActionResult> FilterBooksInSearchAsync(string genre, bool isAscendingOrder, int? itemsPerPage, int? page)
    {
        try
        {
            // Retrieve cached view model or initialize a new one if not found.
            if (!_memoryCache.TryGetValue("view", out ViewModel? view))
            {
                view = new ViewModel();
            }

            // Check if genre or order has changed.
            bool isGenreChanged = !string.IsNullOrWhiteSpace(genre) &&
                !string.Equals(view!.Filter.SelectedGenre, genre, StringComparison.OrdinalIgnoreCase);
            bool isOrderChanged = !view!.Filter.IsAscendingOrder.Equals(isAscendingOrder);

            view!.Filter.IsAscendingOrder = isAscendingOrder;

            if (page is null || isGenreChanged)
            {
                // Retrieve filtered books.
                List<Book> filteredBooks = await FilterBooksAsync(view, genre!, isAscendingOrder);

                if(filteredBooks is null || filteredBooks.Count == 0)
                {
                    _logger.LogWarning($"No books found for genre '{genre}' and page {page ?? 1}.");
                    throw new InvalidOperationException("No books available.");
                }

                if (isGenreChanged)
                {
                    view.Filter.SelectedGenre = genre;
                }

                if(isGenreChanged || isOrderChanged)
                {
                    _memoryCache.Remove("dataToDisplay");
                }

                //Set pagination
                SetupPagination(view, itemsPerPage ?? view.Pagination.ItemsPerPage, page);
                view.Pagination.CalculateTotalPages(filteredBooks.Count);

                ManageDataToDisplay(filteredBooks, view);
            }
            else
            {
                //Set pagination
                SetupPagination(view!, itemsPerPage ?? view.Pagination.ItemsPerPage, page);
            }

            // Retrieve books from cache for the current page.
            var booksToDisplay = GetBooksFromCache(view.Pagination.CurrentPage);
            if (booksToDisplay is null || booksToDisplay.Count == 0)
            {
                _logger.LogWarning($"No books found for current page {view.Pagination.CurrentPage}.");
                throw new InvalidOperationException("No books available.");
            }

            // Populate the view model's product data.
            view.Product = new() { Books = booksToDisplay };
            _memoryCache.Set("view", view);

            return View("SearchBooks", view);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during filtering.");
            return Error();
        }
    }

    /// <summary>
    /// Filters books based on the provided genre, order, and pagination settings.
    /// </summary>
    /// <param name="view">The view model containing filter and pagination settings.</param>
    /// <param name="genre">The genre to filter by, or null to apply no genre filter.</param>
    /// <param name="isAscendingOrder">Determines whether the books should be ordered by ascending price.</param>
    /// <param name="itemsPerPage">The number of items per page, used for pagination.</param>
    /// <param name="page">The current page number for pagination.</param>
    /// <returns>A list of books filtered and sorted based on the specified criteria.</returns>
    private async Task<List<Book>> FilterBooksAsync(ViewModel view, string? genre, bool isAscendingOrder)
    {
        List<Book> filteredBooks = [];

        // Check if genre filter matches the current filter.
        if (!string.IsNullOrWhiteSpace(genre) && 
            string.Equals(view.Filter.SelectedGenre, genre, StringComparison.OrdinalIgnoreCase))
        {
            //If genre is 'undefined', then it is not filtered by genre.
            var books = genre.Equals("undefined", StringComparison.OrdinalIgnoreCase) ?
                Enumerable.Range(1, view.Pagination.TotalPages)
                .SelectMany(i => GetBooksFromCache(i))
                .ToList() :
                Enumerable.Range(1, view.Pagination.TotalPages)
                .SelectMany(i => GetBooksFromCache(i))
                .Where(book => book.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
                .ToList();

            filteredBooks.AddRange(books);
        }
        else
        {
            // Set the selected genre or default it to "undefined".
            view.Filter.SelectedGenre = genre ?? "undefined";

            // Fetch books from the API.
            filteredBooks = (await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(
                ApiEndpoints.Books.Search(view.Filter.SearchTearm, isAscendingOrder)))?.ToList() ?? [];

            if (!string.IsNullOrWhiteSpace(view.Filter.SelectedGenre) && 
                !string.Equals(view.Filter.SelectedGenre, "undefined", StringComparison.OrdinalIgnoreCase))
            {
                filteredBooks = filteredBooks
                    .Where(book => book.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

        }

        // Sort the books based on price.
        return [.. isAscendingOrder ? 
            filteredBooks.OrderBy(book => book.Price) : 
            filteredBooks.OrderByDescending(book => book.Price)];
    }

    /// <summary>
    /// Displays the error page when an error occurs.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> representing the error view.</returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}