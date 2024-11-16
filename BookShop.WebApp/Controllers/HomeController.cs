using ApiUtilities.Constants;
using ApiUtilities.Models;
using BookShop.WebApp.Models;
using BookShop.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Linq;

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
    public async Task<IActionResult> ShopAsync(int itemsPerPage = 6, int page = 1)
    {
        try
        {
            //Creates and initializes the ViewModel for the page and sets the pagination for it.
            ViewModel view = new() { Pagination = new(itemsPerPage, page) };
            
            //Gets the total quantity of the Books in the database  to count how many pages need to be displayed.
            var quantity = await _httpClient.GetFromJsonAsync<int>(ApiEndpoints.Books.GetCountAll);

            if (quantity < 1)
            {
                _logger.LogWarning("No books are found or data is null.");
                return Error();
            }

            //Calculating and setting the total amount of pages.
            view.Pagination.CalculateTotalPages(quantity);

            //Find out how many items to skip, to display appropriate items at the requested page.
            int toSkip = view.Pagination.ToSkipItems;

            var data = (await _httpClient.GetFromJsonAsync<IEnumerable<Book>>(ApiEndpoints.Books.GetAll))!.Skip(toSkip).Take(view.Pagination.ItemsPerPage);

            if (data is null || !data.Any())
            {
                _logger.LogWarning("No books are found or data is null.");
                return Error();
            }

            view.Product = new() { Books = data };

            //Gets and sets the list of genres for future filtering if needed.
            view.Genres = (await _httpClient.GetFromJsonAsync<IEnumerable<string>>(ApiEndpoints.Books.GetAllGenres))!.ToArray();

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
