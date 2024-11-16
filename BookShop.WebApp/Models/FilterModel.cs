namespace BookShop.WebApp.Models;

/// <summary>
/// Represents filter details for a collection of items.
/// </summary>
public class FilterModel
{
    /// <summary>
    /// Gets or sets the selected genre.
    /// </summary>
    /// <value>
    /// The selected genre.
    /// </value>
    public string SelectedGenre { get; set; } = "undefined";

    /// <summary>
    /// Gets or sets the search tearm.
    /// </summary>
    /// <value>
    /// The search tearm.
    /// </value>
    public string SearchTearm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is ascending order.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is ascending order; otherwise, <c>false</c>.
    /// </value>
    public bool IsAscendingOrder { get; set; } = true;
}
