namespace BookSales.API.Services;

/// <summary>
/// Represent the settings required to connect to a MongoDB database.
/// </summary>
/// <remarks>
/// This class encapsulates all necessary configuration details such as the 
/// connection string, database name, and collection name to facilitate
/// communication with a MongoDB database.
/// </remarks>
public class MongoDBSettings
{
    public string ConnectionString { get; set; } = string.Empty!;
    public string DatabaseName { get; set;} = string.Empty!;
    public string CollectionName { get; set; } = string.Empty!;
}
