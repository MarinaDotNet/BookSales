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
    private static void SetupPagination(ViewModel view, int? itemsPerPage, int? page)
    {
        if(view.Pagination is null || view.Pagination.ItemsPerPage != itemsPerPage)
        {
            view.Pagination = new PaginationModel(itemsPerPage ?? 6, page ?? 1);
        }
        else 
        {
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
    private async Task PopulateViewModelAsync(ViewModel view, string booksEndpoint)
    {
        if(view.Genres is null || view.Genres.Length == 0)
        {
            view.Genres = (await _httpClient.GetFromJsonAsync<IEnumerable<string>>(ApiEndpoints.Books.GetAllGenres))!.ToArray();
        }

        if(view.Pagination is not null)
        {
            var totalItems = await _httpClient.GetFromJsonAsync<int>(ApiEndpoints.Books.GetCountAll);
            view.Pagination.CalculateTotalPages(totalItems);

            var books = (await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(booksEndpoint))!
                .Skip(view.Pagination.ToSkipItems)
                .Take(view.Pagination.ItemsPerPage);

            if (books is null || !books.Any())
            {
                _logger.LogWarning("No books found for the current page.");
                throw new InvalidOperationException("No books available.");
            }

            view.Product = new() { Books = books };
        }
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
