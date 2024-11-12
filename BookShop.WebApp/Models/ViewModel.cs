using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BookShop.WebApp.Models;

/// <summary>
/// ViewModel representing various data related to product, filtering, ordering, and errors.
/// </summary>
public class ViewModel
{
    /// <summary>
    /// Gets or sets the genres associated with the current product.
    /// Initialized with a default value of <c>"undefined"</c>.
    /// </summary>
    public string[] Genres { get; set; } = ["undefined"];

    /// <summary>
    /// Gets or sets the product-related information.
    /// Initialized with a new instance of the <see cref="ProductViewModel"/> class.
    /// </summary>
    public ProductViewModel Product { get; set; } = new();

    /// <summary>
    /// Gets or sets the filtering criteria for products.
    /// Initialized with a new instance of the <see cref="FilterModel"/> class.
    /// </summary>
    public FilterModel Filter { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordering information for sorting products.
    /// Initialized with a new instance of the <see cref="OrderViewModel"/> class.
    /// </summary>
    public OrderViewModel Order { get; set; } = new();

    /// <summary>
    /// Gets or sets the error details if an error occurs.
    /// Initialized with a new instance of the <see cref="ErrorViewModel"/> class.
    /// </summary>
    public ErrorViewModel ErrorView { get; set; } = new();

    /// <summary>
    /// Gets or sets the account view details.
    /// </summary>
    /// <value>
    /// The account view.
    /// </value>
    public AccountViewModel AccountView { get; set; } = new();

    /// <summary>
    /// Gets or sets the pagination.
    /// </summary>
    /// <value>
    /// The pagination.
    /// </value>
    public PaginationModel Pagination { get; set; } = new(5, 1);
}
