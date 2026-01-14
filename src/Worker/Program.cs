using UPMS.Worker.Configuration;
using UPMS.Worker.Data;
using UPMS.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Configure database settings from environment or config
var databaseSettings = new DatabaseSettings();
builder.Configuration.GetSection("ConnectionStrings").Bind(databaseSettings);

// Override with environment variable if present (Docker)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    databaseSettings.Postgres = databaseUrl;
}

builder.Services.AddSingleton(databaseSettings);

// Register database connection factory
builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

// Register background workers
builder.Services.AddHostedService<ReportProcessorWorker>();

// Configure structured JSON logging for production
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.UseUtcTimestamp = true;
});

var host = builder.Build();
host.Run();
