namespace UPMS.Api.Endpoints;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Ingestion;
using UPMS.Reporting;
using UPMS.Reporting.Plugins;
using UPMS.Reporting.Templates;

public static class UpmsApiEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static IEndpointRouteBuilder MapUpmsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            service = "UPMS.Api",
            utcNow = DateTime.UtcNow,
            status = "ok"
        }))
        .AllowAnonymous()
        .WithTags("System")
        .WithName("GetHealth");

        app.MapGet("/canonical-fields", async (ICanonicalFieldService canonicalFields, CancellationToken ct) =>
        {
            var rows = await canonicalFields.GetAllAsync(ct);
            return Results.Ok(rows.Select(row => new CanonicalFieldResponse(
                row.Name,
                row.DataType.ToString(),
                row.IsSystemRequired)));
        })
        .WithTags("Canonical Fields")
        .WithName("GetCanonicalFields");

        app.MapGet("/itsm-sources", async (IItsmSourceService sources) =>
        {
            var rows = await sources.GetAllSourcesAsync();
            return Results.Ok(rows.Select(row => new ItsmSourceSummaryResponse(
                row.Id,
                row.Name,
                row.DisplayLabel)));
        })
        .WithTags("ITSM Sources")
        .WithName("GetItsmSources");

        app.MapGet("/itsm-sources/{name}", async (string name, IItsmSourceService sources) =>
        {
            try
            {
                var definition = await sources.GetSourceDefinitionAsync(name);
                return Results.Ok(new ItsmSourceDefinitionResponse(
                    definition.Source.Id,
                    definition.Source.Name,
                    definition.Source.DisplayLabel,
                    definition.Mappings
                        .OrderBy(m => m.SourceFieldName, StringComparer.OrdinalIgnoreCase)
                        .Select(m => new ItsmFieldMappingResponse(
                            m.ItsmSource,
                            m.SourceFieldName,
                            m.CanonicalFieldName,
                            m.IsRequired))
                        .ToArray()));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithTags("ITSM Sources")
        .WithName("GetItsmSourceByName");

        app.MapPost("/itsm-sources", async (CreateItsmSourceRequest request, IItsmSourceService sources) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.DisplayLabel))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name is required."],
                    ["displayLabel"] = ["Display label is required."]
                });
            }

            var created = await sources.CreateSourceAsync(request.Name.Trim(), request.DisplayLabel.Trim());
            return Results.Created($"/api/v1/itsm-sources/{created.Name}", new ItsmSourceSummaryResponse(
                created.Id,
                created.Name,
                created.DisplayLabel));
        })
        .WithTags("ITSM Sources")
        .WithName("CreateItsmSource");

        app.MapPut("/itsm-sources/{name}/mappings/{sourceFieldName}", async (
            string name,
            string sourceFieldName,
            UpsertItsmFieldMappingRequest request,
            IItsmSourceService sources) =>
        {
            if (string.IsNullOrWhiteSpace(request.CanonicalFieldName))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["canonicalFieldName"] = ["Canonical field name is required."]
                });
            }

            await sources.UpsertMappingAsync(
                name,
                sourceFieldName,
                request.CanonicalFieldName.Trim(),
                request.IsRequired);

            return Results.NoContent();
        })
        .WithTags("ITSM Sources")
        .WithName("UpsertItsmFieldMapping");

        app.MapDelete("/itsm-sources/{name}/mappings/{sourceFieldName}", async (string name, string sourceFieldName, IItsmSourceService sources) =>
        {
            await sources.DeleteMappingAsync(name, sourceFieldName);
            return Results.NoContent();
        })
        .WithTags("ITSM Sources")
        .WithName("DeleteItsmFieldMapping");


        app.MapDelete("/itsm-sources/{name}", async (string name, IItsmSourceService sources) =>
        {
            await sources.DeleteSourceAsync(name);
            return Results.NoContent();
        })
        .WithTags("ITSM Sources")
        .WithName("DeleteItsmSource");

        app.MapGet("/report-templates", (IReportTemplateStore templateStore) =>
        {
            var rows = templateStore.GetAllTemplates();
            return Results.Ok(rows
                .OrderByDescending(template => template.UploadedAt)
                .Select(MapTemplate));
        })
        .WithTags("Report Templates")
        .WithName("GetReportTemplates");

        app.MapPost("/report-templates", async (
            HttpRequest request,
            IReportTemplateStore templateStore,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var displayName = form["displayName"].ToString();
            var kindRaw = form["kind"].ToString();
            var description = form["description"].ToString();
            var subjectTemplate = form["subjectTemplate"].ToString();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A template file is required." });

            if (string.IsNullOrWhiteSpace(displayName))
                return Results.BadRequest(new { error = "displayName is required." });

            if (!Enum.TryParse<ReportTemplateKind>(kindRaw, true, out var kind))
                return Results.BadRequest(new { error = $"kind must be one of: {string.Join(", ", Enum.GetNames<ReportTemplateKind>())}." });

            await using var stream = file.OpenReadStream();
            var metadata = await templateStore.SaveAsync(new ReportTemplateUploadRequest
            {
                DisplayName = displayName.Trim(),
                Kind = kind,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                SubjectTemplate = string.IsNullOrWhiteSpace(subjectTemplate) ? null : subjectTemplate.Trim(),
                OriginalFileName = file.FileName,
                UploadedBy = ResolveRequestedBy(user) ?? "anonymous"
            }, stream, ct);

            return Results.Created($"/api/v1/report-templates/{metadata.Id}", MapTemplate(metadata));
        })
        .DisableAntiforgery()
        .WithTags("Report Templates")
        .WithName("UploadReportTemplate");

        app.MapGet("/snapshots", async (string? itsmSource, string? company, TicketDataServiceInstance data) =>
        {
            var rows = await data.GetSnapshotsAsync(itsmSource, company);
            return Results.Ok(rows.Select(row => new SnapshotResponse(
                row.Id,
                row.ItsmSource,
                row.SnapshotDate,
                row.UploadedBy,
                row.UploadedAt,
                row.UploadMetadata)));
        })
        .WithTags("Snapshots")
        .WithName("GetSnapshots");

        app.MapGet("/snapshots/{id:guid}", async (Guid id, TicketDataServiceInstance data) =>
        {
            var snapshot = await data.GetSnapshotByIdAsync(id);
            return snapshot is null
                ? Results.NotFound()
                : Results.Ok(new SnapshotResponse(
                    snapshot.Id,
                    snapshot.ItsmSource,
                    snapshot.SnapshotDate,
                    snapshot.UploadedBy,
                    snapshot.UploadedAt,
                    snapshot.UploadMetadata));
        })
        .WithTags("Snapshots")
        .WithName("GetSnapshotById");

        app.MapGet("/snapshots/{id:guid}/tickets", async (Guid id, TicketDataServiceInstance data) =>
        {
            var rows = await data.GetTicketsBySnapshotAsync(id);
            return Results.Ok(rows.Select(MapTicket));
        })
        .WithTags("Snapshots")
        .WithName("GetSnapshotTickets");

        app.MapPost("/snapshots/ingest", async (
            HttpRequest request,
            ISnapshotIngestService ingestService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var itsmSource = form["itsmSource"].ToString();
            var snapshotDateRaw = form["snapshotDate"].ToString();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A snapshot file is required." });

            if (string.IsNullOrWhiteSpace(itsmSource))
                return Results.BadRequest(new { error = "itsmSource is required." });

            if (!DateOnly.TryParse(snapshotDateRaw, out var snapshotDate))
                return Results.BadRequest(new { error = "snapshotDate must be a valid date (yyyy-MM-dd)." });

            await using var stream = file.OpenReadStream();
            var result = IsJson(file)
                ? await ingestService.IngestJsonAsync(stream, itsmSource.Trim(), snapshotDate, ct)
                : await ingestService.IngestCsvAsync(stream, itsmSource.Trim(), snapshotDate, ct);

            return result.Success
                ? Results.Ok(MapIngestResult(result))
                : Results.BadRequest(MapIngestResult(result));
        })
        .DisableAntiforgery()
        .WithTags("Snapshots")
        .WithName("IngestSnapshotSync");

        app.MapGet("/tickets", async (
            string itsmSource,
            string? company,
            DateTime? asOf,
            string? fieldName,
            string? fieldValue,
            TicketDataServiceInstance data) =>
        {
            if (string.IsNullOrWhiteSpace(itsmSource))
                return Results.BadRequest(new { error = "itsmSource is required." });

            var effectiveAsOf = asOf ?? DateTime.UtcNow;
            IEnumerable<Ticket> tickets;

            if (!string.IsNullOrWhiteSpace(company))
            {
                tickets = await data.GetTicketsAsync(itsmSource.Trim(), company.Trim(), effectiveAsOf);

                if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(fieldValue))
                {
                    tickets = tickets.Where(ticket => TryMatchField(ticket.Fields, fieldName, fieldValue));
                }
            }
            else
            {
                var filters = new List<TicketFieldFilter>();
                if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(fieldValue))
                {
                    filters.Add(new TicketFieldFilter(fieldName, fieldValue));
                }

                tickets = await data.GetTicketsFilteredAsync(itsmSource.Trim(), effectiveAsOf, filters);
            }

            return Results.Ok(tickets.Select(MapTicket));
        })
        .WithTags("Tickets")
        .WithName("GetTickets");

        app.MapGet("/tickets/{company}/{ticketKey}", async (
            string company,
            string ticketKey,
            string? itsmSource,
            DateTime? asOf,
            TicketDataServiceInstance data) =>
        {
            string? resolvedSource = itsmSource;
            if (string.IsNullOrWhiteSpace(resolvedSource)
                && TicketKeyFactory.TryParse(ticketKey, out var parsedSource, out _, out _))
            {
                resolvedSource = parsedSource;
            }

            if (string.IsNullOrWhiteSpace(resolvedSource))
                return Results.BadRequest(new { error = "itsmSource is required when it cannot be derived from the ticket key." });

            var tickets = await data.GetTicketsAsync(resolvedSource, company, asOf ?? DateTime.UtcNow);
            var ticket = tickets.FirstOrDefault(t => string.Equals(t.TicketKey, ticketKey, StringComparison.Ordinal));
            return ticket is null ? Results.NotFound() : Results.Ok(MapTicket(ticket));
        })
        .WithTags("Tickets")
        .WithName("GetTicketByKey");

        app.MapGet("/tickets/{company}/{ticketKey}/history", async (string company, string ticketKey, TicketDataServiceInstance data) =>
        {
            var history = await data.GetTicketHistoryAsync(company, ticketKey);
            return Results.Ok(history.Select(change => new FieldChangeResponse(
                change.Id,
                change.CompanyName,
                change.TicketKey,
                change.FieldName,
                change.CanonicalFieldName,
                change.DisplayFieldName,
                change.FieldValue,
                change.ObservedAt,
                change.SnapshotId,
                change.RegisteredDataType?.ToString(),
                change.IsValueValid)));
        })
        .WithTags("Tickets")
        .WithName("GetTicketHistory");

        app.MapGet("/reports/plugins", (IReportExecutionService reporting) =>
        {
            var rows = reporting.GetPlugins();
            return Results.Ok(rows.Select(plugin => new ReportPluginResponse(
                plugin.PluginId,
                plugin.DisplayName,
                plugin.Description,
                plugin.Parameters.Select(parameter => new ReportParameterResponse(
                    parameter.Key,
                    parameter.DisplayName,
                    parameter.Type.ToString(),
                    parameter.IsRequired,
                    parameter.Description,
                    parameter.Placeholder,
                    parameter.CanonicalFieldName,
                    parameter.Options?.ToArray() ?? Array.Empty<string>())).ToArray())));
        })
        .WithTags("Reports")
        .WithName("GetReportPlugins");

        app.MapPost("/reports/execute", async (ExecuteReportRequest request, IReportExecutionService reporting, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PluginId))
                return Results.BadRequest(new { error = "pluginId is required." });

            var result = await reporting.ExecuteAsync(
                request.PluginId.Trim(),
                request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ResolveRequestedBy(user),
                ct);

            if (!result.Success)
            {
                return Results.BadRequest(new ReportExecutionResponse(
                    result.Success,
                    result.OutputType.ToString(),
                    result.FileName,
                    result.ContentType,
                    result.HtmlContent,
                    result.ErrorMessage));
            }

            if (result.OutputType == ReportOutputType.FileDownload && result.FileContent is not null)
            {
                return Results.File(
                    result.FileContent,
                    result.ContentType ?? "application/octet-stream",
                    result.FileName ?? "upms-report.bin");
            }

            return Results.Ok(new ReportExecutionResponse(
                result.Success,
                result.OutputType.ToString(),
                result.FileName,
                result.ContentType,
                result.HtmlContent,
                result.ErrorMessage));
        })
        .WithTags("Reports")
        .WithName("ExecuteReportSync");

        app.MapPost("/jobs/snapshot-ingest", async (
            HttpRequest request,
            IArtifactStorage artifacts,
            IBackgroundJobService jobs,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var itsmSource = form["itsmSource"].ToString();
            var snapshotDateRaw = form["snapshotDate"].ToString();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A snapshot file is required." });

            if (string.IsNullOrWhiteSpace(itsmSource))
                return Results.BadRequest(new { error = "itsmSource is required." });

            if (!DateOnly.TryParse(snapshotDateRaw, out var snapshotDate))
                return Results.BadRequest(new { error = "snapshotDate must be a valid date (yyyy-MM-dd)." });

            await using var uploadStream = file.OpenReadStream();
            var stored = await artifacts.SaveAsync("uploads", file.FileName, uploadStream, file.ContentType, ct);
            var payload = new SnapshotIngestJobPayload(
                itsmSource.Trim(),
                snapshotDate,
                stored.RelativePath,
                file.FileName,
                file.ContentType ?? "application/octet-stream");

            var job = await jobs.EnqueueAsync(
                BackgroundJobTypes.SnapshotIngest,
                JsonSerializer.Serialize(payload, JsonOptions),
                ResolveRequestedBy(user),
                ct);

            return Results.Accepted($"/api/v1/jobs/{job.Id}", MapJob(job));
        })
        .DisableAntiforgery()
        .WithTags("Jobs")
        .WithName("QueueSnapshotIngest");

        app.MapPost("/jobs/report-execution", async (ExecuteReportRequest request, IBackgroundJobService jobs, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PluginId))
                return Results.BadRequest(new { error = "pluginId is required." });

            var payload = new ReportExecutionJobPayload(
                request.PluginId.Trim(),
                request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ResolveRequestedBy(user));

            var job = await jobs.EnqueueAsync(
                BackgroundJobTypes.ReportExecution,
                JsonSerializer.Serialize(payload, JsonOptions),
                ResolveRequestedBy(user),
                ct);

            return Results.Accepted($"/api/v1/jobs/{job.Id}", MapJob(job));
        })
        .WithTags("Jobs")
        .WithName("QueueReportExecution");

        app.MapGet("/jobs", async (int? take, IBackgroundJobService jobs, CancellationToken ct) =>
        {
            var rows = await jobs.GetRecentAsync(take ?? 50, ct);
            return Results.Ok(rows.Select(MapJob));
        })
        .WithTags("Jobs")
        .WithName("GetJobs");

        app.MapGet("/jobs/{id:guid}", async (Guid id, IBackgroundJobService jobs, CancellationToken ct) =>
        {
            var job = await jobs.GetByIdAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(MapJob(job));
        })
        .WithTags("Jobs")
        .WithName("GetJobById");

        app.MapGet("/jobs/{id:guid}/download", async (Guid id, IBackgroundJobService jobs, IArtifactStorage artifacts, CancellationToken ct) =>
        {
            var job = await jobs.GetByIdAsync(id, ct);
            if (job is null)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(job.OutputFilePath) || !artifacts.Exists(job.OutputFilePath))
                return Results.NotFound(new { error = "No downloadable artifact exists for this job." });

            var stream = artifacts.OpenRead(job.OutputFilePath);
            return Results.File(
                stream,
                job.OutputContentType ?? "application/octet-stream",
                job.OutputFileName ?? "upms-artifact.bin",
                enableRangeProcessing: true);
        })
        .WithTags("Jobs")
        .WithName("DownloadJobArtifact");

        return app;
    }

    private static bool IsJson(IFormFile file)
    {
        if (file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(file.ContentType)
            && file.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveRequestedBy(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated ?? false)
            return user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return "anonymous";
    }

    private static bool TryMatchField(IDictionary<string, string?> fields, string fieldName, string fieldValue)
    {
        foreach (var entry in fields)
        {
            if (string.Equals(entry.Key, fieldName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Value ?? string.Empty, fieldValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static TicketResponse MapTicket(Ticket ticket)
    {
        return new TicketResponse(
            ticket.TicketKey,
            ticket.CompanyName,
            ticket.ItsmSource,
            ticket.ObservedAt,
            ticket.SnapshotId,
            ticket.SnapshotDate,
            new Dictionary<string, string?>(ticket.Fields, StringComparer.OrdinalIgnoreCase));
    }

    private static IngestResultResponse MapIngestResult(IngestResult result)
    {
        return new IngestResultResponse(
            result.Success,
            result.SnapshotId,
            result.TicketsIngested,
            result.FieldChangesRecorded,
            result.ErrorMessage,
            result.Warnings.ToArray());
    }

    private static ReportTemplateResponse MapTemplate(ReportTemplateMetadata template)
    {
        return new ReportTemplateResponse(
            template.Id,
            template.DisplayName,
            template.Kind.ToString(),
            template.Description,
            template.SubjectTemplate,
            template.FileName,
            template.Extension,
            template.ContentType,
            template.UploadedAt,
            template.UploadedBy);
    }

    private static BackgroundJobResponse MapJob(BackgroundJob job)
    {
        var downloadUrl = string.IsNullOrWhiteSpace(job.OutputFilePath)
            ? null
            : $"/api/v1/jobs/{job.Id}/download";

        return new BackgroundJobResponse(
            job.Id,
            job.JobType,
            job.Status,
            job.RequestedBy,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage,
            job.ResultJson,
            job.OutputFileName,
            job.OutputContentType,
            downloadUrl);
    }
}

public sealed record CanonicalFieldResponse(string Name, string DataType, bool IsSystemRequired);

public sealed record ItsmSourceSummaryResponse(int Id, string Name, string DisplayLabel);

public sealed record ItsmFieldMappingResponse(string ItsmSource, string SourceFieldName, string CanonicalFieldName, bool IsRequired);

public sealed record ItsmSourceDefinitionResponse(int Id, string Name, string DisplayLabel, IReadOnlyList<ItsmFieldMappingResponse> Mappings);

public sealed record CreateItsmSourceRequest(string Name, string DisplayLabel);

public sealed record UpsertItsmFieldMappingRequest(string CanonicalFieldName, bool IsRequired);

public sealed record SnapshotResponse(Guid Id, string ItsmSource, DateTime SnapshotDate, string UploadedBy, DateTime UploadedAt, string? UploadMetadata);

public sealed record TicketResponse(
    string TicketKey,
    string CompanyName,
    string ItsmSource,
    DateTime ObservedAt,
    Guid SnapshotId,
    DateTime SnapshotDate,
    IReadOnlyDictionary<string, string?> Fields);

public sealed record FieldChangeResponse(
    long Id,
    string CompanyName,
    string TicketKey,
    string FieldName,
    string? CanonicalFieldName,
    string DisplayFieldName,
    string? FieldValue,
    DateTime ObservedAt,
    Guid SnapshotId,
    string? RegisteredDataType,
    bool? IsValueValid);

public sealed record IngestResultResponse(
    bool Success,
    Guid SnapshotId,
    int TicketsIngested,
    int FieldChangesRecorded,
    string? ErrorMessage,
    IReadOnlyList<string> Warnings);

public sealed record ExecuteReportRequest(string PluginId, Dictionary<string, string>? Parameters);

public sealed record ReportParameterResponse(
    string Key,
    string DisplayName,
    string Type,
    bool IsRequired,
    string? Description,
    string? Placeholder,
    string? CanonicalFieldName,
    IReadOnlyList<string> Options);

public sealed record ReportPluginResponse(
    string PluginId,
    string DisplayName,
    string Description,
    IReadOnlyList<ReportParameterResponse> Parameters);

public sealed record ReportExecutionResponse(
    bool Success,
    string OutputType,
    string? FileName,
    string? ContentType,
    string? HtmlContent,
    string? ErrorMessage);

public sealed record ReportTemplateResponse(
    string Id,
    string DisplayName,
    string Kind,
    string? Description,
    string? SubjectTemplate,
    string FileName,
    string Extension,
    string ContentType,
    DateTimeOffset UploadedAt,
    string UploadedBy);

public sealed record BackgroundJobResponse(
    Guid Id,
    string JobType,
    string Status,
    string? RequestedBy,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    string? ResultJson,
    string? OutputFileName,
    string? OutputContentType,
    string? DownloadUrl);
