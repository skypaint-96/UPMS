using UPMS.Api.Configuration;
using UPMS.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON logging for production
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
}

// Bind database settings from configuration
var databaseSettings = new DatabaseSettings();
builder.Configuration.GetSection("ConnectionStrings").Bind(databaseSettings);

// Support DATABASE_URL environment variable (Docker/Heroku style)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    databaseSettings.Postgres = databaseUrl;
}

builder.Services.AddSingleton(databaseSettings);

// Register database connection factory
builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "UPMS API",
        Version = "v1",
        Description = "Snapshot-Based Reporting & Analysis Platform API"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "UPMS API v1");
    });
}

app.MapControllers();

// Minimal health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .WithName("Health")
    .WithTags("Health");

app.Run();
