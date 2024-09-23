using BookSales.API.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace BookSales.API.Services;

/// <summary>
/// Provides services for interacting with MongoDB collections, specifically for managing class <see cref="Book"/> entities.
/// </summary>
/// <remarks>
/// This class handles MongoDB operations such as adding, updating, deleting, and querying book data.
/// It utilizes a MongoDB collection to store and manage book information.
/// </remarks>
public class MongoDBServices
{
    private readonly IMongoCollection<Book> _collection;
    private readonly ILogger<MongoDBServices> _logger;

    /// <summary>
    /// Initializes new instance of the <see cref="MongoDBServices"/> class.
    /// </summary>
    /// <param name="settings">The configuration settings for MongoDB, injected via <see cref="IOptions{T}"/>.</param>
    /// <param name="logger">The logger instance for logging operations and errors.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="settings"/> is null.
    /// </exception>
    /// <exception cref="MongoConfigurationException">
    /// Thrown when the MongoDB client cannot be configured due to invalid settings.
    /// </exception>
    public MongoDBServices(IOptions<MongoDBSettings> settings, ILogger<MongoDBServices> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");

        //Validate MongoDB settings
        var mongoDBSettings = settings.Value ?? 
            throw new ArgumentNullException(nameof(settings), "MongoDB settings cannot be null.");

        ValidateMongoDBSettings(mongoDBSettings);

        try
        {
            var mongoClient = new MongoClient(mongoDBSettings.ConnectionString);
            var mongoDB = mongoClient.GetDatabase(mongoDBSettings.DatabaseName);
            _collection = mongoDB.GetCollection<Book>(mongoDBSettings.CollectionName);

            _logger.LogInformation("MongoDB connection established successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure the MongoDB client.");
            throw new MongoConfigurationException("Failed to configure the MongoDB client.");
        }
    }

    ///<summary>Validates the MongoDB settings.</summary>
    ///<param name="mongoDBSettings">The MongoDB settings to validate.</param>
    ///<exception cref="ArgumentException">Thrown when any required setting is null or empty.</exception>
    private static void ValidateMongoDBSettings(MongoDBSettings mongoDBSettings)
    {
        if (string.IsNullOrWhiteSpace(mongoDBSettings.ConnectionString))
        {
            throw new ArgumentException("The connection string cannot be null or empty.", nameof(mongoDBSettings));
        }

        if (string.IsNullOrWhiteSpace(mongoDBSettings.DatabaseName))
        {
            throw new ArgumentException("The database name cannot be null or empty.", nameof(mongoDBSettings));
        }

        if (string.IsNullOrWhiteSpace(mongoDBSettings.CollectionName))
        {
            throw new ArgumentException("The collection name cannot be null or empty.", nameof(mongoDBSettings));
        }
    }

    #region Retrieving Data from MongoDB

    /// <summary>
    /// Retrieves all books from the MongoDB collection, with an optional filter for availability status.
    /// </summary>
    /// <remarks>
    /// This method fetches all books from the MongoDB collection. If an availability status is provided, the method
    /// only retrieves books that match the specified availability status. If the availability filter is not provided (null),
    /// all books in the collection are retrieved without filtering by availability.
    /// </remarks>
    /// <param name="isAvailable">
    /// An optional boolean value that filters books based on their availability status. If provided, only books that 
    /// match the specified availability status are returned. If null, no availability filtering is applied.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a <see cref="List{Book}"/> representing
    /// the list of books that match the availability filter, or all books if no filter is applied.
    /// </returns>
    /// <exception cref="MongoException">
    /// Thrown when a MongoDB-related error occurs during the data retrieval process.
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Thrown when an unexpected error occurs during the execution of the method.
    /// </exception>
    public async Task<List<Book>> GetAllDataAsync(bool? isAvailable)
    {
        try
        {
            return isAvailable.HasValue ?
                await _collection.Find(book => book.IsAvailable == isAvailable.Value).ToListAsync() :
                await _collection.Find(Builders<Book>.Filter.Empty).ToListAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "An error occured while retrieving data from the database collection.");
            throw new MongoException("A Mongo error occured while retrieving all data. Please try again latter.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while retrieving data from the database collection");
            throw new ApplicationException("An error occured while retieving data from the database collection", ex);
        }
    }

    ///<summary>Retrieves the book by its unique identifier from the MongoDB collection asynchronously.</summary>
    ///<param name="id">The unique identifier of the book to retrieve.</param>
    ///<return>The <see cref="BookDto"/> entity if found; otherwise, <c>null</c>.</return>
    ///<exception cref="ArgumentNullException">Thrown if the <paramref name="id" /> is null or empty.</exception>
    ///<exception cref="MongoException">Thrown when MongoDB-related error occurs.</exception>
    ///<exception cref="ApplicationException">Thrown when unexpected error occurs during the operation</exception>
    ///<remarks>
    ///This method uses the provided book ID to query MongoDB collection and retrieve the corresponding book.
    ///If no book matches the ID, the method return null.
    ///</remarks>
    public async Task<Book> GetBookByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id), "Book ID cannot be null or empty.");
            }

            return await _collection.Find(book => book.Id!.Equals(id)).FirstOrDefaultAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection.");
            throw new MongoException("A Mongo Error occured while retreiving the data from database collection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection.");
            throw new ApplicationException("An error occured while retreiving the data from database collection. Please try again latter.", ex);
        }
    }

    ///<summary>
    ///Asynchronously retrieves a list of books where the specified condition matches exactly with the book's 
    ///title, author, language, genre, or publisher. 
    /// </summary>
    /// <param name="condition">The string to match exactly agains book property.</param>
    /// <param name="ascendingOrder">Determines the sorting order; true for ascending, false for descending.</param>
    /// <param name="orderParameter">
    /// The property by which to order the result. 
    /// Supported values: "title", "pages", "publisher", "price"
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the sorted list of books 
    /// matching the condition exactly.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="condition"/> is null or empty.</exception>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs.</exception>
    /// <exception cref="ApplicationException">Thrown when an unexpected error occurs during the operation.</exception>
    /// 
    public async Task<List<Book>> GetBooksByExactMatchAsync(string condition, bool ascendingOrder, string orderParameter)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            _logger.LogWarning("The condition is null or empty.");
            throw new ArgumentException("The condition cannot be null or empty.", nameof(condition));
        }

        try
        {
            var trimedCondition = condition.Trim();

            var books = await _collection.Find(book =>
            book.Title!.Trim().Equals(trimedCondition, StringComparison.OrdinalIgnoreCase) ||
            book.Language!.Trim().Equals(trimedCondition, StringComparison.OrdinalIgnoreCase) ||
            book.Publisher!.Trim().Equals(trimedCondition, StringComparison.OrdinalIgnoreCase) ||
            book.Authors!.Any(author => author.Trim().Equals(trimedCondition, StringComparison.OrdinalIgnoreCase)) ||
            book.Genres!.Any(genre => genre.Trim().Equals(trimedCondition, StringComparison.OrdinalIgnoreCase))
            ).ToListAsync();

            return SortBooks(books, ascendingOrder, orderParameter);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection. '{Condition}'.", condition);
            throw new MongoException("A MongoDB error occured while retreiving the data from databse collection. Please try again latter.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection. '{Condition}'.", condition);
            throw new ApplicationException("An error occured while retreiving the data from database collection. Please try again latter.", ex);
        }
    }

    /// <summary>
    /// Asynchronously retrieves a list of books where the specified condition is contained within 
    /// the book's title, author, language, genre, publisher, or annotation.
    /// </summary>
    /// <param name="condition">The string to search within book properties.</param>
    /// <param name="ascendingOrder">Determines the sorting order; true for ascending, false for descending.</param>
    /// <param name="orderParameter">The property by which to order the results. Supported values: "title", "pages", "publisher", "price".</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a sorted list of books that contain the condition.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="condition"/> is null or empty.</exception>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs.</exception>
    /// <exception cref="ApplicationException">Thrown when an unexpected error occurs during the operation.</exception>
    public async Task<List<Book>> GetBooksByPartialMatchAsync(string condition, bool ascendingOrder, string orderParameter)
    {
        if (string.IsNullOrEmpty(condition))
        {
            _logger.LogWarning("The condition is null or empty.");
            throw new ArgumentNullException(nameof(condition), "The condtion cannot be null or empty.");
        }
        try
        {
            var trimedCondition = condition.Trim().ToUpperInvariant();

            //Defines the filter using Builders for better readability and performance
            var filter = Builders<Book>.Filter.Or(
                Builders<Book>.Filter.Where(_ => _.Title != null && _.Title.Trim().ToUpperInvariant().Contains(trimedCondition)),
                Builders<Book>.Filter.Where(_ => _.Language != null && _.Language.Trim().ToUpperInvariant().Contains(trimedCondition)),
                Builders<Book>.Filter.Where(_ => _.Publisher != null && _.Publisher.Trim().ToUpperInvariant().Contains(trimedCondition)),
                Builders<Book>.Filter.Where(_ => _.Authors!.Any(author => author.Trim().ToUpperInvariant().Contains(trimedCondition))),
                Builders<Book>.Filter.Where(_ => _.Genres!.Any(genre => genre.Trim().ToUpperInvariant().Contains(trimedCondition))),
                Builders<Book>.Filter.Where(_ => _.Annotation != null && _.Annotation.Trim().ToUpperInvariant().Contains(trimedCondition))
                );

            // Asynchronously retrieve matching books
            var books = await _collection.Find(filter).ToListAsync();

            // Sort the results based on the specified order parameter
            return SortBooks(books, ascendingOrder, orderParameter);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection. '{Condition}'.", condition);
            throw new MongoException("A MongoDB error occured while retreiving the data from databse collection. Please try again latter.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while retreiving the data from database collection. '{Condition}'.", condition);
            throw new ApplicationException("An error occured while retreiving the data from database collection. Please try again latter.", ex);
        }
    }

    /// <summary>
    /// Asynchronously retrieves the total count of all books in the MongoDB collection.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total number of books in the collection.
    /// </returns>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs.</exception>
    /// <exception cref="ApplicationException">Thrown when an unexpected error occurs during the operation.</exception>
    public async Task<long> GetTotalBooksCountAsync()
    {
        try
        {
            return await _collection.CountDocumentsAsync(FilterDefinition<Book>.Empty);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while counting documents in the book collection.");
            throw new MongoException("A MongoDB error occurred while counting documents in the book collection. Please try again later.", ex); ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while counting documents in the book collection.");
            throw new ApplicationException("An unexpected error occurred while counting documents in the book collection. " +
                "Please try again later.", ex);
        }
    }

    /// <summary>
    /// Asynchronously retrieves the total count of books in the MongoDB collection based on their availability status.
    /// </summary>
    /// <param name="isAvailable">A boolean indicating whether to count available books (true) or unavailable books (false).</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total number of books matching the specified availability status.
    /// </returns>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs.</exception>
    /// <exception cref="ApplicationException">Thrown when an unexpected error occurs during the operation.</exception>
    public async Task<long> CountBooksByAvailabilityAsync(bool isAvailable)
    {
        try
        {
            // Create a filter to count documents based on the availability status
            var availabilityFilter = Builders<Book>.Filter.Eq(_ => _.IsAvailable, isAvailable);
            return await _collection.CountDocumentsAsync(availabilityFilter);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while counting books by availability.");
            throw new MongoException("A MongoDB error occurred while counting books by availability. Please try again later.", ex); ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while counting books by availability.");
            throw new ApplicationException("An unexpected error occurred while counting books by availability. " +
                "Please try again later.", ex);
        }
    }

    /// <summary>
    /// Asynchronously counts the total number of books that exactly match the specified search term across various book attributes
    /// (such as title, author, language, genre, or publisher) and optionally filters by availability status.
    /// </summary>
    /// <remarks>
    /// This method counts books that match the given search term exactly in one or more fields, including "Title", "Language",
    /// "Publisher", "Authors", and "Genres". It also supports an optional filter by availability status. Case-insensitive comparisons
    /// are handled using MongoDB's collation feature.
    /// </remarks>
    /// <param name="searchTerm">
    /// The string to match exactly in book attributes such as "Title", "Language", "Publisher", "Authors", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value indicating whether to filter by availability status. If provided, the method will only count books
    /// that match the availability status (true for available, false for unavailable). If null, the availability filter is ignored.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total number of books that match the specified search term
    /// and, optionally, the availability status.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the search term is null or whitespace.
    /// </exception>
    /// <exception cref="MongoException">
    /// Thrown when an error occurs while communicating with the MongoDB server or querying the collection.
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Thrown when an unexpected error occurs during the execution of the query.
    /// </exception>
    public async Task<long> CountBooksByExactMatchAsync(string searchTerm, bool? isAvailable = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            throw new ArgumentNullException(nameof(searchTerm), "Search tearm cannot be null or empty.");
        }
        try
        {
            var trimmedSearchTerm = searchTerm.Trim();

            // Base filter for matching book attributes
            var attributeFilter = Builders<Book>.Filter.Or(
                Builders<Book>.Filter.Eq("Title", trimmedSearchTerm),
                Builders<Book>.Filter.Eq("Language", trimmedSearchTerm),
                Builders<Book>.Filter.Eq("Publisher", trimmedSearchTerm),
                Builders<Book>.Filter.AnyEq("Authors", trimmedSearchTerm),
                Builders<Book>.Filter.AnyEq("Genres", trimmedSearchTerm)
                );

            // Availability filter (optional)
            var availabilityFilter = isAvailable.HasValue ?
                Builders<Book>.Filter.Eq(book => book.IsAvailable, isAvailable.Value) :
                Builders<Book>.Filter.Empty;

            // Combine both filters
            var combinedFilter = Builders<Book>.Filter.And(attributeFilter, availabilityFilter);

            return await _collection.Find(combinedFilter, 
                new FindOptions { Collation = new Collation("en", strength: CollationStrength.Primary) })
                .CountDocumentsAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while counting books by exact match for the term '{SearchTerm}' and availability '{IsAvailable}'.", searchTerm, isAvailable);
            throw new MongoException("A MongoDB error occurred while counting books by exact match. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while counting books by exact match for the term '{SearchTerm}' and availability '{IsAvailable}'.", searchTerm, isAvailable);
            throw new ApplicationException("An unexpected error occurred while counting books by exact match. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Asynchronously counts the total number of books that partially match a specified search term across various book attributes
    /// (such as title, author, language, genre, publisher, or annotation) and optionally filters by availability status.
    /// </summary>
    /// <remarks>
    /// This method searches for books in the MongoDB collection that contain the search term as a substring in one or more fields,
    /// including "Title", "Language", "Publisher", "Annotation", "Authors", and "Genres". It also supports an optional filter by availability status.
    /// Case-insensitive comparisons are handled using MongoDB's collation feature.
    /// </remarks>
    /// <param name="searchTerm">
    /// The string to search within book attributes such as "Title", "Language", "Publisher", "Annotation", "Authors", or "Genres".
    /// This parameter is required and cannot be null or empty.
    /// </param>
    /// <param name="isAvailable">
    /// An optional boolean value indicating whether to filter by availability status. If provided, the method will only count books
    /// that match the availability status (true for available, false for unavailable). If null, the availability filter is ignored.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the total number of books that partially match the specified search term
    /// and, optionally, the availability status.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the search term is null or whitespace.
    /// </exception>
    /// <exception cref="MongoException">
    /// Thrown when an error occurs while communicating with the MongoDB server or querying the collection.
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Thrown when an unexpected error occurs during the execution of the query.
    /// </exception>
    public async Task<long> CountBooksByPartialMatchAsync(string searchTerm, bool? isAvailable)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            throw new ArgumentException("Search term cannot be null or empty.", nameof(searchTerm));
        }
        try
        {
            var trimmedSearchTerm = searchTerm.Trim();

            var attributeFilter = Builders<Book>.Filter.Or(
                    Builders<Book>.Filter.Where(book => book.Title != null &&
                    book.Title.Contains(trimmedSearchTerm)),
                    Builders<Book>.Filter.Where(book => book.Language != null &&
                    book.Language.Contains(trimmedSearchTerm)),
                    Builders<Book>.Filter.Where(book => book.Publisher != null &&
                    book.Publisher.Contains(trimmedSearchTerm)),
                    Builders<Book>.Filter.Where(book => book.Annotation != null &&
                    book.Annotation.Contains(trimmedSearchTerm)),
                    Builders<Book>.Filter.Where(book =>
                    book.Authors!.Any(author => author.Contains(trimmedSearchTerm))),
                    Builders<Book>.Filter.Where(book =>
                    book.Genres!.Any(genre => genre.Contains(trimmedSearchTerm)))
                    );

            // Availability filter (optional)
            var availabilityFilter = isAvailable.HasValue ?
                Builders<Book>.Filter.Eq(book => book.IsAvailable, isAvailable.Value) :
                Builders<Book>.Filter.Empty;

            // Combine both filters
            var combinedFilter = Builders<Book>.Filter.And(attributeFilter, availabilityFilter);

            return await _collection.Find(combinedFilter,
                new FindOptions { Collation = new Collation("en", strength: CollationStrength.Primary) })
                .CountDocumentsAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while counting books by partial match for the term '{SearchTerm}' and availability '{IsAvailable}'.", searchTerm, isAvailable);
            throw new MongoException("A MongoDB error occurred while counting books by partial match. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while counting books by partial match for the term '{SearchTerm}' and availability '{IsAvailable}'.", searchTerm, isAvailable);
            throw new ApplicationException("An unexpected error occurred while counting books by partial match. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Retrieves all books from the MongoDB collection and sorts them based on the specified order parameter and direction,
    /// with an optional filter for availability status.
    /// </summary>
    /// <param name="isAscendingOrder">A boolean value indicating whether the books should be sorted in ascending order (true) or descending order (false).</param>
    /// <param name="orderParameter">The attribute by which to sort the books. It must be one of the following: "title", "pages", "publisher", or "price".</param>
    /// <param name="isAvailable">An optional boolean value that filters books based on their availability status. If null, no availability filtering is applied.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="List{Book}"/> representing the sorted list of books.</returns>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs during the query or sorting process.</exception>
    /// <exception cref="ApplicationException">Thrown when an unexpected error occurs during the execution of the method.</exception>
    public async Task<List<Book>> SortAllBooksAsync(bool isAscendingOrder, string orderParameter, bool? isAvailable)
    {
        try
        {
            var books = isAvailable.HasValue ?
                await _collection.Find(books => books.IsAvailable == isAvailable.Value).ToListAsync() :
                await _collection.Find(Builders<Book>.Filter.Empty).ToListAsync();

            return SortBooks(books, isAscendingOrder, orderParameter);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error accured while sorting books by the property '{OrderParameter}' and in order '{isAscendingOrder}'.", orderParameter, isAscendingOrder);
            throw new MongoException("MongoDB error accured while sorting books. Please try again letter.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while sorting books by the property '{OrderParameter}' and in order '{isAscendingOrder}'.", orderParameter, isAscendingOrder);
            throw new ApplicationException("An unexpected error occured while sorting books. Please try again latter.", ex);
        }
    }
    #endregion Retrieving Data from MongoDB

    #region Additional Helper Methods

    /// <summary>
    /// Sorts the list of books based on the specified parameter and direction.
    /// </summary>
    /// <param name="books">The list of books to be sorted.</param>
    /// <param name="ascendingOrder">Determines the sorting order; true for ascending, false for descending.</param>
    /// <param name="orderParameter">
    /// The property by which to order the results. 
    /// Supported values: "title", "pages", "publisher", "price".
    /// </param>
    /// <returns>A sorted list of books.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided <paramref name="orderParameter"/> is not valid.</exception>
    /// <remarks>
    /// This method orders the list of books based on the specified parameter <paramref name="orderParameter"/> 
    /// and sorting direction <paramref name="ascendingOrder"/>. 
    /// The <paramref name="ascendingOrder"/> should be one of supported values: "title", "pages", "publisher", or "price".
    /// If the <paramref name="ascendingOrder"/> is invalid, an <see cref="ArgumentException"/> is thrown.
    /// </remarks>
    private static List<Book> SortBooks(List<Book> books, bool ascendingOrder, string orderParameter)
    {
        return orderParameter.ToLower() switch
        {
            "title" => ascendingOrder ?
            [.. books.OrderBy(book => book.Title!.ToLower())] :
            [.. books.OrderByDescending(book => book.Title!.ToLower())],
            "pages" => ascendingOrder ?
            [.. books.OrderBy(book => book.Pages)] :
            [.. books.OrderByDescending(book => book.Pages)],
            "publisher" => ascendingOrder ?
            [.. books.OrderBy(book => book.Publisher!.ToLower())] :
            [.. books.OrderByDescending(book => book.Publisher!.ToLower())],
            "price" => ascendingOrder ?
            [.. books.OrderBy(book => book.Price)] :
            [.. books.OrderByDescending(book => book.Price)],
            _ => throw new ArgumentException($"Invalid order parameter: {orderParameter}", nameof(orderParameter))
        };
    }

    /// <summary>
    /// Asynchronously checks if a book with the specified ID exists in the database.
    /// </summary>
    /// <param name="bookId">The ID of the book to check for existence.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a boolean indicating whether the book exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the bookId is null or whitespace.
    /// </exception>
    /// <exception cref="MongoException">Thrown when a MongoDB-related error occurs.</exception>
    /// <exception cref="ApplicationException">
    /// Thrown when an error occurs during the database operation.
    /// </exception>
    /// <remarks>
    /// This method checks for the existence of a book in the database by its ID. 
    /// It returns true if a book with the given ID exists, otherwise false.
    /// If the <paramref name="bookId"/> is null or empty, an <see cref="ArgumentNullException"/> is thrown.
    /// The method also handles potential errors from MongoDB and unexpected errors with appropriate logging and exception handling.
    /// </remarks>
    public async Task<bool> IsBookExistsAsync(string bookId)
    {
        if (string.IsNullOrWhiteSpace(bookId))
        {
            throw new ArgumentNullException(nameof(bookId), "The parameter ID cannot be null or empty.");
        }
        try
        {
            var filter = Builders<Book>.Filter.Eq(book => book.Id, bookId);
            var count = await _collection.CountDocumentsAsync(filter);
            return count > 0;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while checking if the book with ID '{BookId}' exists in the database.", bookId);
            throw new MongoException("A MongoDB error occurred while checking if the book exists in the database. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while checking if the book with ID '{BookId}' exists in the database.", bookId);
            throw new ApplicationException("An unexpected error occurred while checking if the book exists in the database. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Fixes an invalid list of strings by replacing it with a default value if the list is empty or contains a null or 
    /// whitespace entry as the first element.
    /// </summary>
    /// <param name="listToCompare">
    /// The list of strings to be compared. If the list is empty or the first element is null or whitespace, it will be replaced 
    /// with a list containing a single entry: "undefined".
    /// </param>
    /// <returns>
    /// A <see cref="List{string}"/> containing either the original list (if valid) or a list with a single entry: "undefined".
    /// </returns>
    static private List<string> EnsureValidBookList(List<string> listToCompare)
    {
        return listToCompare.Count == 0 || string.IsNullOrWhiteSpace(listToCompare[0]) ?
            ["undefined"] : listToCompare;
    }

    /// <summary>
    /// Validates a list of strings to ensure that it contains at least one element and that the first element is not null or whitespace.
    /// </summary>
    /// <param name="listValidate">
    /// The list of strings to validate. The list must contain at least one element, and the first element must not be null or whitespace.
    /// </param>
    /// <returns>
    /// <c>true</c> if the list contains at least one valid element (non-null and non-whitespace); otherwise, <c>false</c>.
    /// </returns>
    static private bool IsValidStringList(List<string> listValidate)
    {
        return listValidate.Count > 0 && !string.IsNullOrWhiteSpace(listValidate[0]);
    }

    /// <summary>
    /// Validates a string to ensure it is not null or whitespace.
    /// </summary>
    /// <param name="value">
    /// The string value to validate.
    /// </param>
    /// <param name="fieldName">
    /// The name of the field being validated. This will be included in the exception message if the validation fails.
    /// </param>
    /// <returns>
    /// The validated string if it is not null or whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided string is null or consists only of whitespace.
    /// </exception>
    static private string ValidateString(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(fieldName, $"The '{fieldName}' cannot be null or empty.");
        }
        return value;
    }
    #endregion Additional Helper Methods

    #region Manipulations with Data from DB Collection
    /// <summary>
    /// Adds a new book to the MongoDB collection based on the provided <see cref="BookDTO"/> data transfer object.
    /// </summary>
    /// <remarks>
    /// This method validates the fields from the <see cref="BookDTO"/> before creating and inserting the new book into the MongoDB collection.
    /// If any validation fails, it throws the appropriate exception. The method handles database-related errors and logs them accordingly.
    /// </remarks>
    /// <param name="bookDto">
    /// The <see cref="BookDTO"/> object containing the data for the new book. This includes properties such as Title, Authors, Annotation, Genres,
    /// Language, Link, Price, Pages, Publisher, and IsAvailable.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the newly added <see cref="Book"/> object if the operation succeeds.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required property in the <see cref="BookDTO"/> is null or empty.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// Thrown when the Price or Pages values are invalid (less than or equal to 0).
    /// </exception>
    /// <exception cref="MongoException">
    /// Thrown when a MongoDB-related error occurs while inserting the book into the database.
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Thrown when any unexpected error occurs during the execution of the method.
    /// </exception>
    public async Task<Book> AddBookAsync(BookDTO bookDto)
    {
        if(bookDto == null)
        {
            throw new ArgumentNullException(nameof(bookDto), "The book object cannot be null.");
        }

        try
        {
            Book book = new()
            {
                Title = ValidateString(bookDto.Title, nameof(bookDto.Title)),
                Authors = IsValidStringList(bookDto.Authors!) ?
                bookDto.Authors! :
                throw new ArgumentNullException(nameof(bookDto)),
                Annotation = ValidateString(bookDto.Annotation, nameof(bookDto.Annotation)),
                Genres = IsValidStringList(bookDto.Genres!) ?
                bookDto.Genres! :
                throw new ArgumentNullException(nameof(bookDto)),
                Language = ValidateString(bookDto.Language, nameof(bookDto.Language)),
                Link = string.IsNullOrWhiteSpace(bookDto.Link!.ToString()) ?
                new Uri("about:blank") : bookDto.Link,
                Price = bookDto.Price > 0 ?
                bookDto.Price :
                throw new ArithmeticException(nameof(bookDto)),
                Pages = bookDto.Pages > 0 ?
                bookDto.Pages :
                throw new ArithmeticException(nameof(bookDto)),
                Publisher = ValidateString(bookDto.Publisher, nameof(bookDto.Publisher)),
                IsAvailable = bookDto.IsAvailable
            };

            await _collection.InsertOneAsync(book);
            return book;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while adding a new book.");
            throw new MongoException("A MongoDB error occurred while adding a new book. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while adding a new book.");
            throw new ApplicationException("An unexpected error occurred while adding a new book. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Updates an existing book in the database based on the provided <see cref="BookDTO"/> and book ID.
    /// Returns the previous version of the book and the updated version.
    /// </summary>
    /// <param name="bookDto">
    /// The <see cref="BookDTO"/> object containing the updated data for the book. Fields such as Title, Authors, Annotation, Genres,
    /// Language, Link, Price, Pages, Publisher, and IsAvailable will be updated if provided. If a field is not provided, its previous value will be retained.
    /// </param>
    /// <param name="id">
    /// The unique 24-character identifier (ID) of the book to update.
    /// </param>
    /// <returns>
    /// A task that returns a tuple containing the previous version of the book (<see cref="Book"/> previousBook) and the updated version (<see cref="Book"/> updatedBook).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided <paramref name="bookDto"/> is null or if the <paramref name="id"/> is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the book with the provided <paramref name="id"/> does not exist in the database or if an error occurs during the update process.
    /// </exception>
    /// <exception cref="MongoException">
    /// Thrown when a MongoDB-related error occurs during the update operation.
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Thrown when an unexpected error occurs during the update process.
    /// </exception>
    public async Task<(Book previousBook, Book updatedBook)> UpdateBookAsync(BookDTO bookDto, string id)
    {
        if (bookDto == null || string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(
                bookDto == null ? nameof(bookDto) : nameof(id), 
                "The book object or its 'ID' cannot be null or empty.");
        }
        try
        {
            // Check if the book exists in the database
            bool exists = await IsBookExistsAsync(id);

            if (!exists)
            {
                throw new InvalidOperationException($"The specified book with ID '{id}' was not found in the database.");
            }

            var bookCurrent = await _collection.Find(book => book.Id == id).FirstOrDefaultAsync();

            Book book = new()
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(bookDto.Title) ?
                bookCurrent.Title : bookDto.Title,
                Authors = IsValidStringList(bookDto.Authors) ?
                 bookDto.Authors : EnsureValidBookList(bookCurrent.Authors),
                Annotation = string.IsNullOrWhiteSpace(bookDto.Annotation) ? bookCurrent.Annotation : bookDto.Annotation,
                Genres = IsValidStringList(bookDto.Genres) ?
                bookDto.Genres : EnsureValidBookList(bookCurrent.Genres),
                Language = string.IsNullOrWhiteSpace(bookDto.Language) ? bookCurrent.Language : bookDto.Language,
                Link = bookDto.Link!.IsAbsoluteUri ? bookDto.Link : bookCurrent.Link,
                Price = bookDto.Price > 0 ? bookDto.Price : bookCurrent.Price,
                Pages = bookDto.Pages > 0 ? bookDto.Pages : bookCurrent.Pages,
                Publisher = string.IsNullOrWhiteSpace(bookDto.Publisher) ? bookCurrent.Publisher : bookDto.Publisher,
                IsAvailable = bookDto.IsAvailable
            };

            // Update the book in the database using a filter
            var filter = Builders<Book>.Filter.Eq(b => b.Id, id);
            var previousBook = await _collection.FindOneAndReplaceAsync(filter, book) ?? 
                throw new InvalidOperationException($"Failed to update the book with ID '{id}'. An error occurred while processing the request.");
            
            return (previousBook, book);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, $"A MongoDB error occurred while updating the book with ID '{id}'.");
            throw new MongoException("A MongoDB error occurred while updating the book. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while updating the book with ID '{id}'.");
            throw new ApplicationException("An unexpected error occurred while updating the book. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Asynchronously deletes a book from the database by its ID.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book to be deleted.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentNullException">Throw when the <paramref name="bookId"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a book with specified ID is not found in the database.</exception>
    /// <exception cref="MongoException">Throw when MongoDB error occurs during the specific to MongoDb operations.</exception>
    /// <exception cref="ApplicationException">Thrown when an error occurs during the database operation.</exception>
    /// /// <remarks>
    /// This method deletes a book from the database by its ID.
    /// It first checks if the book exists using <see cref="IsBookExistsAsync"/>.
    /// If the book does not exist, an <see cref="InvalidOperationException"/> is thrown.
    /// Any MongoDB or unexpected errors will be logged, and an <see cref="ApplicationException"/> will be thrown.
    /// </remarks>
    public async Task<Book> DeleteBookByIdAsync(string bookId)
    {
        if (string.IsNullOrWhiteSpace(bookId))
        {
            throw new ArgumentNullException(nameof(bookId), "The book ID cannot be null or empty.");
        }
        try
        {
            bool exists = await IsBookExistsAsync(bookId);
            if (!exists)
            {
                throw new InvalidOperationException($"The specified book with ID '{bookId}' was not found in database.");
            }

            var result = await _collection.FindOneAndDeleteAsync(book => book.Id!.Equals(bookId)) ?? 
                throw new InvalidOperationException($"Failed to delete the book with ID '{bookId}'. An error occurred while processing the request.");
            
            return result;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "A MongoDB error occurred while deleting the book with ID '{BookId}'.", bookId);
            throw new MongoException("A MongoDB error occurred while deleting the book. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while deleting the book with ID '{BookId}'.", bookId);
            throw new ApplicationException("An unexpected error occurred while deleting the book. Please try again later.", ex);
        }
    }
    #endregion Manipulations with Data from DB Collection
}
