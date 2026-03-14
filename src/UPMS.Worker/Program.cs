using Microsoft.EntityFrameworkCore;
using UPMS.Data;
using UPMS.Ingestion;
using UPMS.Reporting;
using UPMS.Reporting.Templates;
using UPMS.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<FileSharePollingOptions>(builder.Configuration.GetSection("FileSharePolling"));
builder.Services.AddUpmsData(builder.Configuration);
builder.Services.AddUpmsIngestion();
builder.Services.AddUpmsReporting(builder.Configuration);
builder.Services.AddHostedService<BackgroundJobWorker>();
builder.Services.AddHostedService<FileSharePollingService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    var schemaBootstrapper = scope.ServiceProvider.GetRequiredService<UpmsSchemaBootstrapper>();
    await schemaBootstrapper.EnsureAsync();
    var templateBootstrapper = scope.ServiceProvider.GetRequiredService<IReportTemplateBootstrapper>();
    await templateBootstrapper.EnsureSeededAsync();
}

await host.RunAsync();
