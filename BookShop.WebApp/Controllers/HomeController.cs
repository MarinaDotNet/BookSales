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
    /// Populates the <see cref="ViewModel"/> <paramref name="view"/> with genres, pagination data, and books collection based on the 
    /// specified request endpoints <paramref name="booksEndpoint"/>
    /// </summary>
    /// <param name="view">The view model <see cref="ViewModel"/> to populate with data.</param>
    /// <param name="booksEndpoint">The API endpoint to fetch books data from.</param>
    /// <returns>A <see cref="Task"/> representing an asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"> thrown when no books are available.</exception>
    /// <exception cref="ArgumentNullException"> throw when <paramref name="view"/> is null.</exception>
    /// <exception cref="ArgumentException"> throw when <paramref name="booksEndpoint"/> is null, empty or consists of white space chars.</exception>
    private async Task PopulateViewModelAsync(ViewModel view, string booksEndpoint)
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
            var totalItems = await _httpClient.GetFromJsonAsync<int>(ApiEndpoints.Books.GetCountAll);

            view.Pagination.CalculateTotalPages(totalItems);
            
            //Populate cache with relevant data for display
            await ManageDataToDisplay(booksEndpoint, view.Pagination.TotalPages, view.Pagination.ItemsPerPage);

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
    }

    /// <summary>
    /// Manages the retrieval and caching of paginated book data from the specified API endpoint.
    /// </summary>
    /// <param name="endpoint">The API endpoint to fetch the book data from.</param>
    /// <param name="pages">The total number of pages for pagination.</param>
    /// <param name="quantityPerPage">The number of books to display per page.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method checks the cache for existing paginated data. If the cache is expired or missing data, 
    /// it fetches all books from the API, splits them into pages, and stores them in the cache.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no books are available from the API.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="endpoint"/> is null or empty.</exception>
    private async Task ManageDataToDisplay(string endpoint, int pages, int quantityPerPage)
    {
        if(_memoryCache.TryGetValue("dataToDisplay", out (Dictionary<int, IEnumerable<Book>> dataAtPage, DateTime expiryTime) cache))
        {
            if(cache.expiryTime >= DateTime.Now && cache.dataAtPage.Count == pages)
            {
                //Cache is valid and do not need to be updated
                return;
            }

            // Cache invalid, remove it.
            _memoryCache.Remove("dataToDisplay"); 
        }

        ArgumentNullException.ThrowIfNullOrWhiteSpace(endpoint, "The endpoint parameter cannot be null or empty.");

        var books = await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(endpoint);

        if(books is null || !books.Any())
        {
            _logger.LogWarning("No books found in database.");
            throw new InvalidOperationException("No books available.");
        }

        // Ensure at least one page.
        pages = pages < 1 ? 1 : pages;
        // Default to a minimum of 6 items per page.
        quantityPerPage = quantityPerPage < 6 ? 6 : quantityPerPage;

        Dictionary<int, IEnumerable<Book>> keyValuePairs = [];

        for (int i = 1; i <= pages; i++)
        {
            int toSkip = (i - 1) * quantityPerPage;

            keyValuePairs[i] = books.Skip(toSkip).Take(quantityPerPage);
        }

        _memoryCache.Set("dataToDisplay", (keyValuePairs, DateTime.Now.AddHours(2)));
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
            await PopulateViewModelAsync(view!, ApiEndpoints.Books.GetAll);

            _memoryCache.Set("view", view);

            return View(view);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return Error();
        }
        
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
