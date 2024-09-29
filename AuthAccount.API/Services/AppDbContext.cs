using AuthAccount.API.Models;
using AuthAccount.API.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthAccount.API.Services;

/// <summary>
/// Represents the application's database context for managing authentication, users, roles, and orders.
/// Inherits from IdentityDbContext to integrate Identity framework capabilities for managing user and role information.
/// </summary>
/// <param name="options">Database context options to configure the context.</param>
/// <param name="configuration">The configuration object to access application settings (e.g., admin and user credentials).</param>
public class AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration) : IdentityDbContext<ApiUser, IdentityRole, string>(options)
{
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Configures the model and relationships between entities in the database.
    /// Seeds initial data for the default admin and user accounts, roles, and order entities.
    /// </summary>
    /// <param name="builder">The <see cref="ModelBuilder"/> object used to configure the model.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Configure the relationship between ApiUser and Order entities.
        builder.Entity<ApiUser>()
            .HasMany(user => user.Orders)
            .WithOne(order => order.User)
            .HasForeignKey(user => user.UserId);

        // Retrieve admin and user account credentials from configuration.
        string adminEmail = _configuration[AuthConstants.AdminEmailKey]! ??
            throw new InvalidOperationException("The section in Configuration 'AdminEmail' should not be null or empty.");
        string userEmail = _configuration[AuthConstants.UserEmailKey]! ??
            throw new InvalidOperationException("The section in Configuration 'UserEmail' should not be null or empty."); ; 
        string password = _configuration[AuthConstants.PasswordKey]! ??
            throw new InvalidOperationException("The section in Configuration 'AccountsPassword' should not be null or empty."); ;

        // Generate unique IDs for user and role entities.
        string userId = Guid.NewGuid().ToString();
        string adminId = Guid.NewGuid().ToString();
        string userRoleId = Guid.NewGuid().ToString();  
        string adminRoleId = Guid.NewGuid().ToString();

        var hasher = new PasswordHasher<ApiUser>();

        // Seed default ApiUser data for admin and regular user accounts.
        builder.Entity<ApiUser>().HasData(
            new ApiUser
            {
                Id = adminId,
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                UserName = AuthConstants.Admin,
                NormalizedUserName = AuthConstants.Admin.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                PasswordHash = hasher.HashPassword(new ApiUser(), password)
            },
            new ApiUser
            {
                Id = userId,
                Email = userEmail,
                NormalizedEmail = userEmail.ToUpperInvariant(),
                UserName = AuthConstants.User,
                NormalizedUserName = AuthConstants.User.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                PasswordHash = hasher.HashPassword(new ApiUser(), password)
            }
            );

        // Seed default IdentityRole data for admin and regular user roles.
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id= adminRoleId,
                Name = AuthConstants.Role.Admin.ToString(),
                NormalizedName = AuthConstants.Role.Admin.ToString().ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            },
            new IdentityRole
            {
                Id = userRoleId,
                Name = AuthConstants.Role.User.ToString(),
                NormalizedName = AuthConstants.Role.User.ToString().ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            }
            );

        // Seed default user-role assignments (admin and user roles).
        builder.Entity<IdentityUserRole<string>>().HasData(
            new IdentityUserRole<string>
            {
                RoleId = adminRoleId,
                UserId = adminId
            },
            new IdentityUserRole<string>
            {
                RoleId = userRoleId,
                UserId = userId
            }
            );

        // Configure the Order entity to use a specific table name.
        builder.Entity<Order>(entity => entity.ToTable("Orders"));

        base.OnModelCreating(builder);
    }

    /// <summary>
    /// Gets or sets the DbSet of orders. 
    /// Represents the Orders table in the database.
    /// </summary>
    public DbSet<Order> Orders { get; set; }
}

