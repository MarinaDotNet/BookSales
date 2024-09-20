using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace BookSales.API.Models;
/// <summary>
/// Represents a book entity with various attributes for storing in a MongoDB collection.
/// </summary>
/// <remarks>
/// Initialize the new instance of the <see cref="Book"/> class with required parameters.
/// </remarks>
public class Book
{
    /// <summary>
    /// Gets or sets the unique identifier for the book.
    /// </summary>
    /// <remarks>This property is marked as read-only and uses the MongoDB type for representation.</remarks>
    [BsonId]
    [ReadOnly(true)]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the book.
    /// </summary>
    /// <remarks>This property is stored in the MongoDB document as the "title" element.</remarks>
    [BsonElement("title")]
    [BsonRepresentation(BsonType.String)]
    public string Title { get; set;  } = string.Empty!;

    ///<summary>Gets or sets the list of authors for the book.</summary>
    ///<remarks>This property is stored as an array in the MongoDB document under the "authors" element.</remarks>
    [BsonElement("authors")]
    public List<string> Authors { get; set; } = [];

    ///<summary>Gets or sets the price of the book.</summary>
    ///<remarks>This property is stored as a Decimal128 type in the MongoDB document as the "price" element.</remarks>
    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    [Precision(18, 2)]
    public decimal Price { get; set; }

    ///<summary>Gets or sets the number of pages in the book.</summary>
    ///<remarks>This property is stored as Int32 type in the MongoDB document as the "pages" element.</remarks>
    [BsonElement("pages")]
    [BsonRepresentation(BsonType.Int32)]
    public int Pages { get; set; }

    ///<summary>Gets or sets the publisher of the book.</summary>
    ///<remarks>This property is stored in the MongoDB document as the "publisher" element.</remarks>
    [BsonElement("publisher")]
    [BsonRepresentation(BsonType.String)]
    public string Publisher { get; set; } = string.Empty!;

    ///<summary>Gets or sets the language of the book.</summary>
    ///<remarks>This property is stored in the MongoDB document as the "language" element.</remarks>
    [BsonElement("language")]
    [BsonRepresentation(BsonType.String)]
    public string Language { get; set; } = string.Empty!;

    ///<summary>Gets or sets the list of the genres for the book.</summary>
    ///<remarks>This property is stored as an array in the MongoDB document as the "genres" element.</remarks>
    [BsonElement("genres")]
    public List<string> Genres { get; set; } = [];

    ///<summary>Gets or sets the link to additonal information about the book.</summary>
    ///<remarks>
    ///This property is stored in MongoDB document as the "link" element and
    ///contains an URI pointing to more details about the book.
    ///</remarks>
    [BsonElement("link")]
    public Uri Link { get; set; } = new Uri("about:black", UriKind.Absolute);

    ///<summary>Gets or sets the value indecating whether the book is available.</summary>
    ///<remarks>This property is stored as Boolean in the MongoDB document under "isAvailable" element.</remarks>
    [BsonElement("isAvailable")]
    [BsonRepresentation(BsonType.Boolean)]
    public bool IsAvailable { get; set; }

    ///<summary>Gets or sets the annotation for the book.</summary>
    ///<remarks>This property is stored in the MongoDB document under "annotation" element.</remarks>
    [BsonElement("annotation")]
    [BsonRepresentation(BsonType.String)]
    public string Annotation { get; set; } = string.Empty!;

}
