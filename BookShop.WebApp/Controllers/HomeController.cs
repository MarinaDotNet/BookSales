using ApiUtilities.Constants;
using ApiUtilities.Models;
using BookShop.WebApp.Models;
using BookShop.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BookShop.WebApp.Controllers;

/// <summary>
/// Controller for handling home-related actions such as displaying the index page and privacy information.
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging information and errors.</param>
    /// <param name="configuration">The configuration settings for the application.</param>
    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Api-Version", "3");
        _httpClient.DefaultRequestHeaders.Add(SecurityConstants.AuthApiKey, _configuration[SecurityConstants.AuthApiKey + "Book"]);
        _logger = logger;
    }

    /// <summary>
    /// Displays the index page with a list of books.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> representing the result of the action.</returns>
    public async Task<IActionResult> Index()
    {
        try
        {
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
    /// Displays the error page when an error occurs.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> representing the error view.</returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
