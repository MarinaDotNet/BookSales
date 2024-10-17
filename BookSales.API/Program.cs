using Asp.Versioning;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore.Internal;
using Amazon.Runtime.Internal.Transform;
using Microsoft.AspNetCore.Builder;
using ApiUtilities.Services;
using BooksStock.API.Services;
using ApiUtilities.Services.ApiKey;
using ApiUtilities.Constants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddUserSecrets<StartupBase>();
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<MongoDBServices>();

//Adding services for Api Middleware
builder.Services.AddTransient<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("MyAdministrationPolicy", policy =>
    {
        policy.WithOrigins("https://localhost:7201")
        .WithHeaders("Api-Version", SecurityConstants.AuthApiKey)
        .SetIsOriginAllowed(origin => false)
        .AllowAnyMethod()
        .DisallowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
    });

    options.AddPolicy("MyUserPolicy", policy =>
    {
        policy.WithOrigins("https://localhost:7201")
        .WithHeaders("Api-Version", SecurityConstants.AuthApiKey)
        .SetIsOriginAllowed(origin => false)
        .WithMethods("GET")
        .DisallowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromHours(3));
    });

    options.DefaultPolicyName = "MyUserPolicy";
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.ReportApiVersions = true;
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(SecurityConstants.AuthApiKey, new OpenApiSecurityScheme()
    {
        In = ParameterLocation.Header,
        Description = "Please enter valid Stock API key.",
        Name = SecurityConstants.AuthApiKey,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme()
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = SecurityConstants.AuthApiKey
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("MyUserPolicy");

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<ApiMiddleware>();

app.MapControllers();

app.Run();
