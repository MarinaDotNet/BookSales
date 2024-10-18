using Asp.Versioning;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Builder;
using ApiUtilities.Services;
using BooksStock.API.Services;
using ApiUtilities.Services.ApiKey;
using ApiUtilities.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
        policy
        .WithOrigins(builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidAudienceKey]!)
        .WithHeaders("Api-Version", SecurityConstants.AuthApiKey)
        .SetIsOriginAllowed(origin => false)
        .AllowAnyMethod()
        .DisallowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
    });

    options.AddPolicy("MyUsersPolicy", policy =>
    {
        policy
        .WithOrigins(builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidAudienceKey]!)
        .WithHeaders("Api-Version", SecurityConstants.AuthApiKey)
        .SetIsOriginAllowed(origin => false)
        .WithMethods("GET")
        .DisallowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromHours(3));
    });

    options.AddPolicy("MyGuestsPolicy", policy =>
    {
        policy
        .WithOrigins(builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidAudienceKey]!)
        .WithHeaders("Api-Version")
        .SetIsOriginAllowed(origin => false)
        .WithMethods("GET")
        .DisallowCredentials();
    });
    options.DefaultPolicyName = "MyGuestsPolicy";
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

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidIssuerKey],
            ValidAudience = builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidAudienceKey],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenAdminSecretKey]!))
        };
    });

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

    options.AddSecurityDefinition(SecurityConstants.TokenDataKey, new OpenApiSecurityScheme()
    {
        In = ParameterLocation.Header,
        Description = "Please enter valid authorization token.",
        Name= "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = SecurityConstants.TokenDataKey,
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference()
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = SecurityConstants.TokenDataKey
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

app.UseCors("MyGuestsPolicy");

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<ApiMiddleware>();

app.MapControllers();

app.Run();
