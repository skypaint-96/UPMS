using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using UPMS.Api.Endpoints;
using UPMS.Api.Security;
using UPMS.Data;
using UPMS.Ingestion;
using UPMS.Reporting;
using UPMS.Reporting.Templates;

var builder = WebApplication.CreateBuilder(args);

string authMode = builder.Configuration["Auth:Mode"]
    ?? (builder.Environment.IsDevelopment() ? "None" : "ApiKey");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

builder.Services.AddCors(options =>
{
    options.AddPolicy("upms-frontend", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationDefaults.SchemeName, options =>
    {
        builder.Configuration.GetSection("Auth:ApiKey").Bind(options);

        if (options.Keys.Count == 0 && builder.Environment.IsDevelopment())
        {
            options.Keys.Add(new ApiKeyAuthenticationKey
            {
                Name = "local-dev",
                Value = builder.Configuration["Auth:DevelopmentApiKey"] ?? "upms-dev-key",
                Roles = ["upms-reader", "upms-writer"]
            });
        }
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddUpmsData(builder.Configuration);
builder.Services.AddUpmsIngestion();
builder.Services.AddUpmsReporting(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi("/openapi/{documentName}.json");
app.UseCors("upms-frontend");

if (!string.Equals(authMode, "None", StringComparison.OrdinalIgnoreCase))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

using (var scope = app.Services.CreateScope())
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

app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

var api = app.MapGroup("/api/v1");
if (!string.Equals(authMode, "None", StringComparison.OrdinalIgnoreCase))
{
    api.RequireAuthorization();
}
api.MapUpmsApi();

app.Run();

public partial class Program;
