
/// <summary>
/// A data transfer object (DTO) representing a book's information.
/// </summary>
/// <remarks>
/// This DTO is used for passing book data between the API and the client.
/// It includes various properties such as title, authors, price, and other attributes of the book.
/// </remarks>
namespace BookSales.API.Models;

public record BookDTO
{
    /// <summary>
    /// Gets or sets the title of the book.
    /// </summary>
    /// <value>The title of the book as a non-empty string.</value>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of authors for the book.
    /// </summary>
    /// <value>A list of authors, each as a string. The list can be empty if no authors are specified.</value>
    public List<string> Authors { get; set; } = [];

    /// <summary>
    /// Gets or sets the price of the book.
    /// </summary>
    /// <value>A decimal representing the price of the book.</value>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the number of pages in the book.
    /// </summary>
    /// <value>An integer representing the page count of the book.</value>
    public int Pages { get; set; }

    /// <summary>
    /// Gets or sets the publisher of the book.
    /// </summary>
    /// <value>The publisher's name as a non-empty string.</value>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language in which the book is written.
    /// </summary>
    /// <value>A string representing the language of the book.</value>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of genres the book belongs to.
    /// </summary>
    /// <value>A list of genres, each as a string. The list can be empty if no genres are specified.</value>
    public List<string> Genres { get; set; } = [];

    /// <summary>
    /// Gets or sets the link to the book's online resource.
    /// </summary>
    /// <value>An optional <see cref="Uri"/> object representing a link to the book.</value>
    public Uri? Link { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the book is available.
    /// </summary>
    /// <value><c>true</c> if the book is available; otherwise, <c>false</c>.</value>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the annotation for the book.
    /// </summary>
    /// <value>A string representing additional information or a description of the book.</value>
    public string Annotation { get; set; } = string.Empty;
}
