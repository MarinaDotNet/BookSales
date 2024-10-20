using ApiUtilities.Models;

namespace BookShop.WebApp.Models;

/// <summary>
/// ViewModel representing a product with details of a single book and a collection of books.
/// </summary>
public class ProductViewModel
{
    /// <summary>
    /// Gets or sets the current book being viewed or managed.
    /// This is initialized with a new instance of the <see cref="ApiUtilities.Models.Book"/> class by default.
    /// </summary>
    public Book Book { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of books.
    /// By default, this is initialized as an empty collection of <see cref="ApiUtilities.Models.Book"/>.
    /// </summary>
    public IEnumerable<Book> Books { get; set; } = [];
}
