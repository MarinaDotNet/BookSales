# BookSales.API - Web API

## ASP.NET Core Web API
ASP.NET Core controller-based RESTful Web API for e-commerce Book Store website that uses Mongo Atlas shared Replica Set database for retrieving products(books information) and local SQL (localdb)\MSSQLLocalDB Server for user accounts and orders.<br/>

### About API:
  Async methods in API provide the ability to handle several concurrent HTTP requests. They are not blocking the main thread while waiting for the database response. <br>
  This API consumes and produces data in JSON format because this format is simple and lightweight.

  ### About Mongo Atlas Database:
Mongo Atlas Shared Replica set contains all data about the books library.
| Field in MongoDB: | Data type in MongoDB: | Data type in C#: | Field name in C# class: |
| ----------------- | --------------------- | ---------------- | ----------------------- |
| _id | ObjectId | string | Id |
| title | String | string | Title |
| annotation | String | string | Annotation |
| authors | Array String | List<string> | Authors |
| price | Decimal128 | decimal | Price |
| language | String | string | Language |
| publisher | String | string | Publisher |
| genres | Array String | List<string> | Genres |
| link | String | Uri | Link |
| isAvailable | Boolean | bool | IsAvailable |
| annotation | String | string | Annotation |
