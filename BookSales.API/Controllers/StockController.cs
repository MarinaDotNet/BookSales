using Asp.Versioning;
using BookSales.API.Models;
using BookSales.API.Services;
using BooksStock.API.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.Data;


namespace BookSales.API.Controllers;
/// <summary>
/// StockController provides access to book data stored in MongoDB through three API versions.
/// </summary>
/// <remarks>
/// This controller offers different API versions for various user roles:
/// 1. **API Version 1**: For administrators, includes full CRUD operations (retrieve, add, update, and delete books).
/// 2. **API Version 2**: For signed-in users, includes selected GET methods from Version 1, with a default filter for books where availability status is set to true.
/// 3. **API Version 3**: The default version for unsigned users, provides two methods: one to get the most expensive book (with availability set to true), and another to retrieve the total count of available books.
/// </remarks>

/// <summary>
/// **API Version 1**: For administrators, includes full CRUD operations (retrieve, add, update, and delete books).
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
/// Initializes a new instance of the <see cref="StockV1Controller"/> class.
/// </summary>
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
[ApiController]
[ApiVersion("1")]
[Produces("application/json")]
[Consumes("application/json")]
[EnableCors(PolicyName="MyAdministrationPolicy")]
public class StockV1Controller(MongoDBServices service, ILogger<StockV1Controller> logger) : ControllerBase
{
    private readonly MongoDBServices _service = service ?? 
        throw new ArgumentNullException(nameof(service), "MongoDB service cannot be null or empty.");
    private readonly ILogger<StockV1Controller> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null or empty.");

    #region Http Get Methods

    /// <summary>
    /// Retrieves all books from the database, with an optional filter for availability status.
    /// </summary>
    /// <remarks>
    /// This method fetches all books from the database. If an availability status is provided, it filters the results to only 
    /// include books that match the specified availability status. If no availability status is provided, all books are retrieved.
    /// </remarks>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, the method only retrieves books that 
    /// match the availability status. If null, all books are returned without any availability filtering.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an 
    /// <see cref="ActionResult{IEnumerable{Book}}"/> representing the 
    /// list of books retrieved from the database, filtered by availability if applicable.
    /// </returns>
    /// <response code="200">Returns the list of books if any are found in the database.</response>
    /// <response code="404">Returns if no books are found in the database.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooksAsync(bool? isAvailable = null)
    {
        try
        {
            var books = await _service.GetAllDataAsync(isAvailable);

            return books.Count != 0 ? 
                Ok(books) : 
                NotFound("No books found in the database.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves a book from the MongoDB collection by its unique identifier.
    /// </summary>
    /// <remarks>
    /// This method takes a 24-character MongoDB ObjectId as input and returns the corresponding book.
    /// If the book is not found, a 404 Not Found response is returned. Errors during processing or validation
    /// are handled with appropriate feedback messages to the user.
    /// </remarks>
    /// <param name="id">The 24-character unique identifier of the book to retrieve.</param>
    /// <returns>
    /// Returns a 200 OK response with the book if found, a 404 Not Found response if the book does not exist,
    /// and a 400 Bad Request response if the ID is invalid.
    /// </returns>
    /// <response code="200">Returns the book with the specified ID.</response>
    /// <response code="400">Returns if the provided ID is invalid or improperly formatted.</response>
    /// <response code="404">Returns if the book is not found in the database.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("book/{id}")]
    public async Task<ActionResult<Book>> GetBookByIdAsync([RegularExpression("^[a-fA-F0-9]{24}$", ErrorMessage = "The ID should be a valid hexadecimal string.")] string id)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("The ID cannot be null or empty.");
            }
            var book = await _service.GetBookByIdAsync(id);

            return book is null ? 
                NotFound($"The book with ID '{id}' was not found in database.") : 
                Ok(book);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves books that exactly match the specified condition for one or more properties 
    /// (title, one author from the authors array, language, one genre from the genres array, or publisher),
    /// and optionally filters by availability.
    /// </summary>
    /// <remarks>
    /// This method searches for books in the MongoDB collection that match the given condition exactly based on one or more of 
    /// the following properties:
    /// "title", "authors" (one author from the array), "language", "genres" (one genre from the array), or "publisher".
    /// Additionally, it can optionally filter the books based on their availability status.
    /// If no books match the specified condition or availability, the method returns a 404 Not Found response.
    /// </remarks>
    /// <param name="condition">
    /// The string to match exactly against one or more of the following properties: title, one author from the authors array, 
    /// language, one genre from the genres array, or publisher.
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="ascendingOrder">
    /// A boolean value indicating whether the results should be sorted in ascending order (true) or descending order (false).
    /// Defaults to descending order.
    /// </param>
    /// <param name="orderParameter">
    /// The property by which to order the results. 
    /// Must be one of the following: "title", "pages", "publisher", or "price". 
    /// Defaults to "price". This parameter is case-insensitive.
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value indicating whether to filter by available (true) or unavailable (false) books.
    /// If null, the availability filter is not applied.
    /// </param>
    /// <returns>
    /// Returns a 200 OK response with a list of books if matches are found.
    /// Returns a 404 Not Found response if no books match the given condition or availability.
    /// Returns a 500 Internal Server Error response if an error occurs during processing.
    /// </returns>
    /// <response code="200">Returns the list of books that exactly match the specified condition and optionally, availability.</response>
    /// <response code="404">Returns if no books are found matching the given condition or availability status.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/match/{condition}")]
    public async Task<ActionResult<IEnumerable<Book>>> GetBooksByExactMatchAsync(
        [Required]string condition, bool ascendingOrder = false, 
        [RegularExpression("^(?i)(title|pages|publisher|price)$", 
        ErrorMessage = "The order parameter must be one of the following: 'title', 'pages', 'publisher', 'price'.")] string orderParameter = "price", bool? isAvailbale = null)
    {
        try
        {
            var books = await _service.GetBooksByExactMatchAsync(condition, ascendingOrder, orderParameter);

            if(isAvailbale.HasValue)
            {
                books = books.Where(book => book.IsAvailable == isAvailbale).ToList();
            }

            return books.Count > 0 ?
            Ok(books) : 
                NotFound("No books found matching the given condition.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves a list of books that partially match the specified condition across multiple book attributes 
    /// (title, one author from the authors array, annotation, language, one genre from the genres array, or publisher),
    /// and optionally filters by availability.
    /// </summary>
    /// <remarks>
    /// This method allows users to search for books based on a partial match to one or more book attributes based on one or more of 
    /// the following properties:
    /// "title", "authors" (one author from the array), "language", "genres" (one genre from the array), annotation, or "publisher".
    /// It also allows sorting by a specified attribute and filtering based on availability. 
    /// Case-insensitive comparison is handled using MongoDB's collation feature.
    /// If no books match the specified condition or availability, the method returns a 404 Not Found response. 
    /// </remarks>
    /// <param name="condition">
    /// The search term to match partially against book attributes such as "Title", "Language", "Publisher", "Annotation",
    /// "Authors", or "Genres". This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="ascendingOrder">
    /// A boolean value indicating whether the results should be sorted in ascending order (true) or descending 
    /// order (false). Defaults to false (descending order).
    /// </param>
    /// <param name="orderParameter">
    /// The attribute by which to sort the results. This must be one of the following: 'title', 'pages', 'publisher', or 'price'. 
    /// The parameter is case-insensitive and defaults to "price".
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, only books that 
    /// match the specified availability status are returned. If null, no availability filtering is applied.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an <see cref="IEnumerable{Book}"/> representing 
    /// the list of books that match the search criteria.
    /// </returns>
    /// <response code="200">Returns the list of books that partially match the search term.</response>
    /// <response code="404">Returns if no books are found matching the given condition.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/partialmatch/{condition}")]
    public async Task<ActionResult<IEnumerable<Book>>> GetBooksByPartialMatch(
        [Required] string condition, bool ascendingOrder = false,
        [RegularExpression("^(?i)(title|pages|publisher|price)$", 
        ErrorMessage = "The order parameter must be one of the following: 'title', 'pages', 'publisher', 'price'.")] string orderParameter = "price", bool? isAvailable = null)
    {
        try
        {
            var books = await _service.GetBooksByPartialMatchAsync(condition, ascendingOrder, orderParameter);

            if(isAvailable.HasValue)
            {
                books = books.Where(book => book.IsAvailable == isAvailable).ToList();
            }

            return books.Count > 0 ?
                Ok(books) : 
                NotFound("No books found matching the given condition.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves all books from the database and sorts them based on the specified order parameter and direction, 
    /// with an optional filter for availability status.
    /// </summary>
    /// <remarks>
    /// This method fetches all books and sorts them based on the given attribute, such as "title", "pages", "publisher", or "price".
    /// The sorting direction (ascending or descending) is determined by the `isAscendingOrder` parameter.
    /// Additionally, an optional filter can be applied to only retrieve books that match the given availability status.
    /// </remarks>
    /// <param name="isAscendingOrder">
    /// A boolean value indicating whether the books should be sorted in ascending order (true) or descending order (false).
    /// </param>
    /// <param name="orderParameter">
    /// The attribute by which to sort the results. This must be one of the following: 'title', 'pages', 'publisher', or 'price'. 
    /// The parameter is case-insensitive and defaults to "price".
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, only books that 
    /// match the specified availability status are returned. If null, no availability filtering is applied.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an <see cref="IEnumerable{Book}"/> representing the sorted list of books.
    /// </returns>
    /// <response code="200">Returns the sorted list of books based on the specified criteria.</response>
    /// <response code="404">Returns if no books are found with the given sorting or filtering criteria.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/sort")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooksSortedAsync(bool isAscendingOrder, 
        [RegularExpression("^(?i)(title|pages|publisher|price)$",
        ErrorMessage = "The order parameter must be one of the following: 'title' 'pages', 'publisher, 'price'.")] string orderParameter = "price",
        bool? isAvailable = null)
    {
        try
        {
            var books = await _service.SortAllBooksAsync(isAscendingOrder, orderParameter, isAvailable);

            return books.Count > 0 ?
                Ok(books) : 
                NotFound("No books found with the specified sorting criteria.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    #region Http Get Count Methods

    /// <summary>
    /// Retrieves the total count of books in the MongoDB collection.
    /// </summary>
    /// <remarks>
    /// This method retrieves the total number of books stored in the MongoDB collection by calling the
    /// service layer. It handles both known application errors (like database failures) and unexpected
    /// exceptions, ensuring the API remains responsive even in the event of failure. 
    /// </remarks>
    /// <returns>
    /// Returns a 200 OK response with the total count of books if the operation is successful.
    /// Returns a 500 Internal Server Error if a known error (such as a database error) or 
    /// an unexpected error occurs during the operation.
    /// </returns>
    /// <response code="200">Returns the total number of books in the collection.</response>
    /// <response code="500">Returns a server error if an application or unexpected error occurs.</response>
    [HttpGet("all/count")]
    public async Task<ActionResult<long>> GetTotalBooksCountAsync()
    {
        try
        {
            var totalBooksCount = await _service.GetTotalBooksCountAsync();

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books based on their availability status.
    /// </summary>
    /// <remarks>
    /// This method returns the total number of books in the MongoDB collection filtered by their availability status. 
    /// The availability status is a boolean value where `true` indicates available books and `false` indicates unavailable books.
    /// If an error occurs during the process, the method handles known and unknown exceptions, returning appropriate feedback to the user.
    /// </remarks>
    /// <param name="isAvailable">
    /// A boolean value indicating the availability status to filter the books by. 
    /// True to count available books, false to count unavailable books.
    /// </param>
    /// <returns>
    /// Returns a 200 OK response with the total count of books matching the availability status.
    /// Returns a 500 Internal Server Error if an application-specific or unexpected error occurs.
    /// </returns>
    /// <response code="200">Returns the total count of books with the specified availability status.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("books/count/availability/{isAvailable}")]
    public async Task<ActionResult<long>> CountBooksByAvailabilityAsync(bool isAvailable)
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByAvailabilityAsync(isAvailable);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books that exactly match the specified condition across multiple book attributes 
    /// (such as title, language, genre, publisher, or authors) and optionally filters by availability status.
    /// </summary>
    /// <remarks>
    /// This method counts books that match the given condition exactly across various book attributes. It also allows for 
    /// an optional filter based on availability status. If no availability status is provided, the method counts books regardless 
    /// of their availability.
    /// </remarks>
    /// <param name="condition">
    /// The search term to match exactly against book attributes such as "Title", "Language", "Publisher", "Authors", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, only books that 
    /// match the specified availability status are counted. If null, no availability filtering is applied.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total count of books that match the given condition.
    /// </returns>
    /// <response code="200">Returns the total count of books that match the search term.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("books/count/match/{condition}")]
    public async Task<ActionResult<long>> CountBooksByMatchAsync([Required]string condition, bool? isAvailable = null)
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByExactMatchAsync(condition, isAvailable);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books that partially match the specified condition across multiple book attributes 
    /// (such as title, language, genre, publisher, annotation, or authors) and optionally filters by availability status.
    /// </summary>
    /// <remarks>
    /// This method counts books that partially match the given condition across various book attributes. It also allows for 
    /// an optional filter based on availability status. If no availability status is provided, the method counts books
    /// regardless of their availability.
    /// </remarks>
    /// <param name="condition">
    /// The search term to match partially against book attributes such as "Title", "Language", "Publisher", "Authors", 
    /// "Annotation", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, only books that 
    /// match the specified availability status are counted. If null, no availability filtering is applied.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total count of books that partially 
    /// match the given condition.
    /// </returns>
    /// <response code="200">Returns the total count of books that match the search term.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("books/count/partialmatch/{condition}")]
    public async Task<ActionResult<long>> CountBooksByPartialMatchAsync([Required] string condition, bool? isAvailable = null)
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByPartialMatchAsync(condition, isAvailable);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }
    #endregion Http Get Count Methods

    #endregion Http Get Methods

    #region Manipulations with Data

    /// <summary>
    /// Adds a new book to the database.
    /// </summary>
    /// <param name="bookDto">The data transfer object containing book details.</param>
    /// <returns>
    /// An <see cref="ActionResult{Book}"/> containing the newly created book if successful,
    /// or a <see cref="BadRequest"/> if the creation fails.
    /// </returns>
    /// <response code="200">Book added successfully.</response>
    /// <response code="400">Invalid data provided for creating the book.</response>
    /// <response code="500">An internal server error occurred.</response>
    /// <exception cref="ApplicationException">Thrown when a known application error occurs.</exception>
    /// <exception cref="Exception">Thrown when an unexpected error occurs.</exception>
    [HttpPost("/add")]
    public async Task<ActionResult<Book>> PostNewBookAsync([FromBody] BookDTO bookDto)
    {
        try
        {
           var result = await _service.AddBookAsync(bookDto);
           return result != null ? Ok(result) : BadRequest(result);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Updates an existing book in the database and returns both the previous and updated versions.
    /// </summary>
    /// <param name="bookDto">The data transfer object containing the updated book information.</param>
    /// <param name="id">The 24-character ID of the book to update.</param>
    /// <returns>
    /// An <see cref="ActionResult{Book}"/> with a message and the previous and updated versions of the book if successful,
    /// or a <see cref="BadRequest"/> if the update fails.
    /// </returns>
    /// <response code="200">Book updated successfully, returning both the previous and updated versions.</response>
    /// <response code="400">Invalid data provided or book not found for update.</response>
    /// <response code="500">An internal server error occurred during processing.</response>
    /// <exception cref="ApplicationException">Thrown when a known application error occurs.</exception>
    /// <exception cref="Exception">Thrown when an unexpected error occurs.</exception>
    [HttpPut("/update")]
    public async Task<ActionResult<Book>> PutBookAsync([FromBody] BookDTO bookDto, [Required][StringLength(24)] string id)
    {
        try
        {
            var result = await _service.UpdateBookAsync(bookDto, id);
            return result.previousBook == null || result.updatedBook == null ? 
                BadRequest(result) : 
                Ok(new
                {
                    Message = "Updated successfully",
                    before = result.previousBook,
                    after = result.updatedBook
                });
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Deletes a book from the database based on the provided book ID.
    /// </summary>
    /// <param name="id">The unique 24-character identifier of the book to delete.</param>
    /// <returns>
    /// An <see cref="ActionResult{Book}"/> containing the deleted book if successful, 
    /// or a <see cref="BadRequest"/> if the deletion fails.
    /// </returns>
    /// <response code="200">Book deleted successfully.</response>
    /// <response code="400">Invalid ID provided or deletion failed.</response>
    /// <response code="500">An internal server error occurred during processing.</response>
    /// <exception cref="ApplicationException">Thrown when a known application error occurs.</exception>
    /// <exception cref="Exception">Thrown when an unexpected error occurs.</exception>
    [HttpDelete("/delete")]
    public async Task<ActionResult<Book>> DeleteBookAsync([Required][StringLength(24)] string id)
    {
        try
        {
            var result = await _service.DeleteBookByIdAsync(id);
            return result == null ? BadRequest(result) : Ok(result);
        }
        catch(ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }
    #endregion Manipulations with Data
}

/// <summary>
/// **API Version 2**: For signed-in users, includes selected GET methods from Version 1, with a default 
/// filter for books where availability status is set to true.
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
/// Initializes a new instance of the <see cref="StockV2Controller"/> class.
/// </summary>
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
[ApiController]
[ApiVersion("2")]
[Produces("application/json")]
[Consumes("application/json")]
[EnableCors(PolicyName = "MyUserPolicy")]
public class StockV2Controller(MongoDBServices service, ILogger<StockV2Controller> logger) : ControllerBase
{
    private readonly MongoDBServices _service = service ?? 
        throw new ArgumentNullException(nameof(service), "MongoDB service cannot be null or empty.");
    private readonly ILogger<StockV2Controller> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null or empty.");

    #region Http Get Methods

    /// <summary>
    /// Retrieves all books from the database where the availability status is true.
    /// </summary>
    /// <remarks>
    /// This method fetches all books from the database with an availability status of true 
    /// <see cref="Book.IsAvailable"/>.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The task result contains an <see cref="ActionResult{IEnumerable{Book}}"/> representing the 
    /// list of books retrieved from the database with availability status set to true 
    /// <see cref="Book.IsAvailable"/>.
    /// </returns>
    /// <response code="200">Returns the list of books if any are found in the database.</response>
    /// <response code="404">Returns if no books are found in the database.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooksAsync()
    {
        try
        {
            var books = await _service.GetAllDataAsync(true);

            return books.Count != 0 ?
                Ok(books) :
                NotFound("No books found in the database.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves a book by its 24-character hexadecimal ID.
    /// </summary>
    /// <param name="id">The book's unique ID, validated as a 24-character hexadecimal string.</param>
    /// <returns>
    /// An <see cref="ActionResult{Book}"/>. 
    /// Returns 200 OK if the book is found and available <see cref="Book.IsAvailable"/>.
    /// 400 Bad Request if the ID is invalid or 
    /// if the book is found but it is unavailable <see cref="Book.IsAvailable"/>.
    /// 404 Not Found if the book does not exis.
    /// And 500 Internal Server Error for unexpected errors.
    /// </returns>
    /// <response code="200">The book was found and is available <see cref="Book.IsAvailable"/>.</response>
    /// <response code="400">The ID is invalid or the book is unavailable <see cref="Book.IsAvailable"/>.</response>
    /// <response code="404">The book with the specified ID was not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("book/{id}")]
    public async Task<ActionResult<Book>> GetBookByIdAsync([RegularExpression("^[a-fA-F0-9]{24}$", ErrorMessage = "The ID should be a valid hexadecimal string.")] string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("The ID cannot be null or empty.");
            }
            var book = await _service.GetBookByIdAsync(id);

            return book is null ?
                NotFound($"The book with ID '{id}' was not found in database.") :
                book.IsAvailable == false ? 
                BadRequest($"The book with ID '{id}' is not available in the store at this time.") :
                Ok(book);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves books that exactly match the specified condition for one or more properties 
    /// (title, one author from the authors array, language, one genre from the genres array, or publisher),
    /// And fitler books where availability status is true <see cref="Book.IsAvailable"/>.
    /// </summary>
    /// <remarks>
    /// This method searches for books in the MongoDB collection that match the given condition exactly 
    /// based on one or more of the following properties:
    /// "title", "authors" (one author from the array), "language", "genres" (one genre from the array), 
    /// or "publisher".
    /// And filter the books where their availability status is true <see cref="Book.IsAvailable"/>.
    /// If no books match the specified condition, the method returns a 404 Not Found response.
    /// </remarks>
    /// <param name="condition">
    /// The string to match exactly against one or more of the following properties: title, one author from the authors array, 
    /// language, one genre from the genres array, or publisher.
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="ascendingOrder">
    /// A boolean value indicating whether the results should be sorted in ascending order (true) or 
    /// descending order (false).
    /// Defaults to descending order.
    /// </param>
    /// <param name="orderParameter">
    /// The property by which to order the results. 
    /// Must be one of the following: "title", "pages", "publisher", or "price". 
    /// Defaults to "price". This parameter is case-insensitive.
    /// </param>
    /// <returns>
    /// Returns a 200 OK response with a list of books if matches are found with availability status true 
    /// <see cref="Book.IsAvailable"/>.
    /// Returns a 404 Not Found response if no books match the given condition with availability status true 
    /// <see cref="Book.IsAvailable"/>.
    /// Returns a 500 Internal Server Error response if an error occurs during processing.
    /// </returns>
    /// <response code="200">Returns the list of books that exactly match the specified condition 
    /// with availability status true <see cref="Book.IsAvailable"/></response>
    /// <response code="404">Returns if no books are found matching the given condition with 
    /// availability status true <see cref="Book.IsAvailable"/></response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/match/{condition}")]
    public async Task<ActionResult<IEnumerable<Book>>> GetBooksByExactMatchAsync(
        [Required] string condition, bool ascendingOrder = false,
        [RegularExpression("^(?i)(title|pages|publisher|price)$",
        ErrorMessage = "The order parameter must be one of the following: 'title', 'pages', 'publisher', 'price'.")] string orderParameter = "price")
    {
        try
        {
            var books = await _service.GetBooksByExactMatchAsync(condition, ascendingOrder, orderParameter);

            books = books.Where(book => book.IsAvailable == true).ToList();

            return books.Count > 0 ?
                Ok(books) :
                NotFound("No books found matching the given condition.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves a list of books that partially match the specified condition across multiple attributes
    /// (title, one author from the authors array, annotation, language, one genre from the genres array, 
    /// or publisher) and filters them by availability status <see cref="Book.IsAvailable"/> set to true.
    /// </summary>
    /// <remarks>
    /// This method allows users to search for books based on a partial match to one or more book attributes 
    /// based on one or more of the following properties:
    /// "title", "authors" (one author from the array), "language", "genres" (one genre from the array),
    /// annotation, or "publisher".
    /// And filters them by availability status <see cref="Book.IsAvailable"/> set to true.
    /// Case-insensitive comparison is handled using MongoDB's collation feature.
    /// If no books match the specified condition with availability status filter, the method returns a 
    /// 404 Not Found response. 
    /// </remarks>
    /// <param name="condition">
    /// The search term to match partially against book attributes such as "Title", "Language", "Publisher", 
    /// "Annotation", "Authors", or "Genres". This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="ascendingOrder">
    /// A boolean value indicating whether the results should be sorted in ascending order (true) or descending 
    /// order (false). Defaults to false (descending order).
    /// </param>
    /// <param name="orderParameter">
    /// The attribute by which to sort the results. This must be one of the following: 'title', 'pages',
    /// 'publisher', or 'price'. 
    /// The parameter is case-insensitive and defaults to "price".
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains 
    /// an <see cref="IEnumerable{Book}"/> representing the list of books that match the search criteria and
    /// availability filter <see cref="Book.IsAvailable"/>.
    /// </returns>
    /// <response code="200">Returns the list of books that partially match the search term.</response>
    /// <response code="404">Returns if no books are found matching the given condition.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/partialmatch/{condition}")]
    public async Task<ActionResult<IEnumerable<Book>>> GetBooksByPartialMatch(
        [Required] string condition, bool ascendingOrder = false,
        [RegularExpression("^(?i)(title|pages|publisher|price)$",
        ErrorMessage = "The order parameter must be one of the following: 'title', 'pages', 'publisher', 'price'.")] string orderParameter = "price")
    {
        try
        {
            var books = await _service.GetBooksByPartialMatchAsync(condition, ascendingOrder, orderParameter);

            books = books.Where(book => book.IsAvailable == true).ToList();

            return books.Count > 0 ?
                Ok(books) :
                NotFound("No books found matching the given condition.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves all books from the database and sorts them based on the specified order parameter and direction, 
    /// with default filter for availability status true <see cref="Book.IsAvailable"/>.
    /// </summary>
    /// <remarks>
    /// This method retrieves all books and sorts them based on the specified attribute, such as "title", "pages", 
    /// "publisher", or "price". The sorting direction (ascending or descending) is determined by the 
    /// `isAscendingOrder` parameter.
    /// The default filter applies to books with the availability status set to true 
    /// <see cref="Book.IsAvailable"/>.
    /// </remarks>
    /// <param name="isAscendingOrder">
    /// A boolean value indicating whether the books should be sorted in ascending order (true) or 
    /// descending order (false).
    /// </param>
    /// <param name="orderParameter">
    /// The attribute by which to sort the results. This must be one of the following: 'title', 'pages',
    /// 'publisher', or 'price'. 
    /// The parameter is case-insensitive and defaults to "price".
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an 
    /// <see cref="IEnumerable{Book}"/> representing the sorted list of books.
    /// </returns>
    /// <response code="200">Returns the sorted list of books based on the specified criteria where the
    /// availability statys is true.</response>
    /// <response code="404">Returns if no books are found with the given sorting or filtering criteria.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all/sort")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooksSortedAsync(bool isAscendingOrder,
        [RegularExpression("^(?i)(title|pages|publisher|price)$",
        ErrorMessage = "The order parameter must be one of the following: 'title' 'pages', 'publisher, 'price'.")] string orderParameter = "price")
    {
        try
        {
            var books = await _service.SortAllBooksAsync(isAscendingOrder, orderParameter, true);

            return books.Count > 0 ?
                Ok(books) :
                NotFound("No books found with the specified sorting criteria.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    #region Http Get Count Methods

    /// <summary>
    /// Retrieves the total count of books in the MongoDB collection, filtered by availability status set to true <see cref="Book.IsAvailable"/>.
    /// </summary>
    /// <remarks>
    /// This method returns the total number of books, filtered by availability status set to true, from the MongoDB collection.
    /// It handles both application errors (e.g., database failures) and unexpected exceptions to ensure API responsiveness.
    /// </remarks>
    /// <returns>
    /// A 200 OK response with the total count of books, filtered by availability status set to true, if the operation succeeds.
    /// A 500 Internal Server Error if an application or unexpected error occurs.
    /// </returns>
    /// <response code="200">Returns the total number of books in the collection.</response>
    /// <response code="500">Returns a server error if an application or unexpected error occurs.</response>
    [HttpGet("all/count")]
    public async Task<ActionResult<long>> GetTotalBooksCountAsync()
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByAvailabilityAsync(true);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books that exactly match the specified condition across multiple 
    /// attributes (e.g., title, language, genre, publisher, or authors), with a default filter for availability status set to true.
    /// </summary>
    /// <remarks>
    /// This method counts books that exactly match the given condition across various attributes 
    /// while applying a default filter for books with availability status set to true.
    /// </remarks>
    /// <param name="condition">
    /// The search term to match exactly against book attributes such as "Title", "Language", "Publisher", "Authors", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total count of books that match the specified condition.
    /// </returns>
    /// <response code="200">Returns the total count of books that match the search term.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("books/count/match/{condition}")]
    public async Task<ActionResult<long>> CountBooksByMatchAsync([Required] string condition)
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByExactMatchAsync(condition, true);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books that partially match the given condition across multiple 
    /// attributes (e.g., title, language, genre, publisher, or authors), with a default filter for availability status set to true.
    /// </summary>
    /// <remarks>
    /// This method counts books that partially match the given condition across various attributes 
    /// while applying a default filter for books with availability status set to true.
    /// </remarks>
    /// <param name="condition">
    /// The search term to match partially against book attributes such as "Title", "Language", "Publisher",
    /// "Authors", "Annotation", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total count of books 
    /// that partially 
    /// match the given condition.
    /// </returns>
    /// <response code="200">Returns the total count of books that match the search term.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("books/count/partialmatch/{condition}")]
    public async Task<ActionResult<long>> CountBooksByPartialMatchAsync([Required] string condition)
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByPartialMatchAsync(condition, true);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }
    #endregion Http Get Count Methods

    #endregion Http Get Methods
}

/// <summary>
/// 3. **API Version 3**: The default version for unsigned users, provides two methods: one to get the 5 most
/// expensive book (with availability set to true), and another to retrieve the total count of available books.
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
/// Initializes a new instance of the <see cref="StockV3Controller"/> class.
/// </summary>
/// <param name="service">The service responsible for interacting with MongoDB collections.</param>
/// <param name="logger">The logger instance used for logging errors and information.</param>
[ApiController]
[ApiVersion("3")]
[Produces("application/json")]
[Consumes("application/json")]
[EnableCors(PolicyName = "MyUserPolicy")]
public class StockV3Controller(MongoDBServices service, ILogger<StockV3Controller> logger) : ControllerBase
{
    private readonly MongoDBServices _service = service ??
        throw new ArgumentNullException(nameof(service), "MongoDB service cannot be null or empty.");
    private readonly ILogger<StockV3Controller> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null or empty.");

    /// <summary>
    /// Retrieves the top 5 most expensive books from the database, filtered by availability status set to true.
    /// </summary>
    /// <remarks>
    /// This method fetches up to 5 books from the database, sorted in descending order by price, 
    /// and filtered to include only books with availability status set to true.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a list of the top 5 most expensive books.
    /// Returns a 200 OK response with the list of books if found.
    /// Returns a 404 Not Found response if no books are found in the database.
    /// Returns a 500 Internal Server Error if an application or unexpected error occurs during the operation.
    /// </returns>
    /// <response code="200">Returns the list of books if found.</response>
    /// <response code="404">Returns if no books are found in the database.</response>
    /// <response code="500">Returns if an application or unexpected error occurs during the operation.</response>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooksAsync()
    {
        try
        {
            var books = (await _service.SortAllBooksAsync(false, "price", true)).Take(5).ToList();
            
            return books.Count != 0 ?
                Ok(books) :
                NotFound("No books found in the database.");
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves the total count of books in the MongoDB collection, filtered by availability status set to true <see cref="Book.IsAvailable"/>.
    /// </summary>
    /// <remarks>
    /// This method returns the total number of books, filtered by availability status set to true, from the MongoDB collection.
    /// It handles both application errors (e.g., database failures) and unexpected exceptions to ensure API responsiveness.
    /// </remarks>
    /// <returns>
    /// A 200 OK response with the total count of books, filtered by availability status set to true, if the operation succeeds.
    /// A 500 Internal Server Error if an application or unexpected error occurs.
    /// </returns>
    /// <response code="200">Returns the total number of books in the collection.</response>
    /// <response code="500">Returns a server error if an application or unexpected error occurs.</response>
    [HttpGet("all/count")]
    public async Task<ActionResult<long>> GetTotalBooksCountAsync()
    {
        try
        {
            var totalBooksCount = await _service.CountBooksByAvailabilityAsync(true);

            return Ok(totalBooksCount);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }
}