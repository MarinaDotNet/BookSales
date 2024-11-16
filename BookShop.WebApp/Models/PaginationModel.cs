namespace BookShop.WebApp.Models;

/// <summary>
/// Represents pagination details for a collection of items.
/// </summary>
public class PaginationModel(int itemsPerPage, int currentPage)
{
    /// <summary>
    /// Gets the current page.
    /// </summary>
    /// <value>
    /// The current page.
    /// </value>
    public int CurrentPage { get; private set; } = currentPage > 0 ? currentPage : 1;

    /// <summary>
    /// Gets the items per page.
    /// </summary>
    /// <value>
    /// The items per page.
    /// </value>
    public int ItemsPerPage { get; } = itemsPerPage >= 5 ? itemsPerPage : 5;

    /// <summary>
    /// Gets the total pages.
    /// </summary>
    /// <value>
    /// The total pages.
    /// </value>
    public int TotalPages { get; private set; }

    /// <summary>
    /// Converts to skipitems.
    /// </summary>
    /// <value>
    /// To skip items.
    /// </value>
    public int ToSkipItems { get; private set; }
    /// <summary>
    /// Calculates the total pages.
    /// </summary>
    /// <param name="totalItemsCount">The total items count.</param>
    public void CalculateTotalPages(int totalItemsCount)
    {
        TotalPages = (int)Math.Ceiling((double)totalItemsCount / (double)ItemsPerPage);

        //Ensures that at least one page exists
        TotalPages = Math.Max(TotalPages, 1);

        //Ensures that CurrentPage in range of TotalPages
        CurrentPage = Math.Min(CurrentPage, TotalPages);

        CalculateSkipItems();
    }

    /// <summary>
    /// Calculates the skip items.
    /// </summary>
    private void CalculateSkipItems()
    {
        ToSkipItems = CurrentPage == 1 ?
            0 : (CurrentPage - 1) * ItemsPerPage;
    }
}
