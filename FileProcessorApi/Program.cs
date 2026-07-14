using FileProcessorApi.Data;
using FileProcessorApi.Middleware;
using FileProcessorApi.Services;
using FileProcessorApi.Services.FileProcessor;
using FileProcessorApi.Services.FileTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);

builder.Services.AddDbContext<FileTrackingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Tracking")
        ?? $"Data Source={Path.Combine(dataDir, "tracking.db")}"));

builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddScoped<IFileTrackingService, SqliteFileTrackingService>();
builder.Services.AddScoped<IFileProcessor, CsvFileProcessor>();
builder.Services.AddScoped<IFileProcessor, JsonFileProcessor>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FileTrackingDbContext>("database");

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key needed to access the endpoints."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FileTrackingDbContext>();
    Directory.CreateDirectory("data");
    db.Database.EnsureCreated();
}
app.UseSwagger();
app.UseSwaggerUI();

// Catch-all safety net for unhandled exceptions
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();