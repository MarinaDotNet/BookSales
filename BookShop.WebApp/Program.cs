using ApiUtilities.Models;
using ApiUtilities.Services;
using BookShop.WebApp.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<StartupBase>();
ApiEndpoints.BaseBookApiUrl = builder.Configuration["ApiEndpointsSettings:BaseBookApiUrl"]!;

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddIdentity<ApiUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
