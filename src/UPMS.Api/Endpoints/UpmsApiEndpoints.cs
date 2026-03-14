namespace UPMS.Api.Endpoints;

using System.Security.Claims;
using System.Text;
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
using UPMS.Reporting.Plugins.Templates;
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

        app.MapGet("/report-template-types", (ReportTemplateTypeRegistry typeRegistry) =>
        {
            var rows = typeRegistry.GetAll();
            return Results.Ok(rows.Select(MapTemplateType));
        })
        .WithTags("Report Templates")
        .WithName("GetReportTemplateTypes");

        app.MapGet("/report-templates", (
            string? itsmSource,
            string? company,
            IReportTemplateApplicabilityService templateApplicability,
            ReportTemplateTypeRegistry typeRegistry) =>
        {
            var rows = string.IsNullOrWhiteSpace(itsmSource) && string.IsNullOrWhiteSpace(company)
                ? templateApplicability.GetApplicableTemplates()
                : templateApplicability.GetApplicableTemplates(itsmSource, company, allowPartialContext: false);

            return Results.Ok(rows
                .OrderByDescending(template => template.UpdatedAt ?? template.UploadedAt)
                .ThenBy(template => template.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(template => MapTemplate(template, typeRegistry)));
        })
        .WithTags("Report Templates")
        .WithName("GetReportTemplates");

        app.MapGet("/report-templates/{templateId}", async (
            string templateId,
            IReportTemplateStore templateStore,
            ReportTemplateTypeRegistry typeRegistry,
            CancellationToken ct) =>
        {
            var template = await templateStore.GetTemplateContentAsync(templateId, ct);
            return template is null
                ? Results.NotFound()
                : Results.Ok(MapTemplateDetail(template, typeRegistry));
        })
        .WithTags("Report Templates")
        .WithName("GetReportTemplateById");

        app.MapGet("/report-templates/{templateId}/download", async (
            string templateId,
            IReportTemplateStore templateStore,
            CancellationToken ct) =>
        {
            var template = await templateStore.GetTemplateContentAsync(templateId, ct);
            return template is null
                ? Results.NotFound()
                : Results.File(template.FileContent, template.Metadata.ContentType, template.Metadata.FileName);
        })
        .WithTags("Report Templates")
        .WithName("DownloadReportTemplate");

        app.MapPost("/report-templates", async (
            HttpRequest request,
            IReportTemplateStore templateStore,
            ReportTemplateTypeRegistry typeRegistry,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var displayName = form["displayName"].ToString();
            var templateTypeId = form["templateTypeId"].ToString();
            var kindRaw = form["kind"].ToString();
            var description = form["description"].ToString();
            var subjectTemplate = form["subjectTemplate"].ToString();
            var textContent = form["textContent"].ToString();
            var scopeRaw = form["scope"].ToString();

            if (string.IsNullOrWhiteSpace(displayName))
                return Results.BadRequest(new { error = "displayName is required." });

            ReportTemplateScope scope;
            try
            {
                scope = ParseTemplateScope(scopeRaw);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"scope must be valid JSON: {ex.Message}" });
            }

            var selectedType = string.IsNullOrWhiteSpace(templateTypeId)
                ? null
                : typeRegistry.GetByTypeId(templateTypeId.Trim());

            if (!string.IsNullOrWhiteSpace(templateTypeId) && selectedType is null)
                return Results.BadRequest(new { error = $"Unknown templateTypeId '{templateTypeId}'." });

            ReportTemplateKind kind;
            if (selectedType is not null)
            {
                kind = selectedType.Kind;
            }
            else if (!Enum.TryParse<ReportTemplateKind>(kindRaw, true, out kind))
            {
                return Results.BadRequest(new { error = $"kind must be one of: {string.Join(", ", Enum.GetNames<ReportTemplateKind>())}, or provide templateTypeId." });
            }

            Stream? contentStream = null;
            string? originalFileName = null;

            try
            {
                if (file is not null && file.Length > 0)
                {
                    contentStream = file.OpenReadStream();
                    originalFileName = file.FileName;
                }
                else if (!string.IsNullOrWhiteSpace(textContent))
                {
                    if (selectedType is null)
                        return Results.BadRequest(new { error = "templateTypeId is required when creating a template from inline text content." });

                    contentStream = new MemoryStream(Encoding.UTF8.GetBytes(textContent));
                    originalFileName = $"template{selectedType.PrimaryExtension}";
                }
                else
                {
                    return Results.BadRequest(new { error = "Either a template file or textContent is required." });
                }

                try
                {
                    var metadata = await templateStore.SaveAsync(new ReportTemplateUploadRequest
                    {
                        TemplateTypeId = selectedType?.TypeId,
                        DisplayName = displayName.Trim(),
                        Kind = kind,
                        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                        SubjectTemplate = string.IsNullOrWhiteSpace(subjectTemplate) ? null : subjectTemplate.Trim(),
                        Scope = scope,
                        OriginalFileName = originalFileName!,
                        UploadedBy = ResolveRequestedBy(user) ?? "anonymous"
                    }, contentStream, ct);

                    return Results.Created($"/api/v1/report-templates/{metadata.Id}", MapTemplate(metadata, typeRegistry));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
            finally
            {
                if (contentStream is not null)
                    await contentStream.DisposeAsync();
            }
        })
        .DisableAntiforgery()
        .WithTags("Report Templates")
        .WithName("UploadReportTemplate");

        app.MapPut("/report-templates/{templateId}", async (
            string templateId,
            HttpRequest request,
            IReportTemplateStore templateStore,
            ReportTemplateTypeRegistry typeRegistry,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var existingTemplate = await templateStore.GetTemplateContentAsync(templateId, ct);
            if (existingTemplate is null)
                return Results.NotFound();

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var displayName = form.ContainsKey("displayName") ? form["displayName"].ToString() : existingTemplate.Metadata.DisplayName;
            var templateTypeId = form.ContainsKey("templateTypeId") ? form["templateTypeId"].ToString() : existingTemplate.Metadata.TemplateTypeId;
            var kindRaw = form.ContainsKey("kind") ? form["kind"].ToString() : existingTemplate.Metadata.Kind.ToString();
            var description = form.ContainsKey("description") ? form["description"].ToString() : existingTemplate.Metadata.Description;
            var subjectTemplate = form.ContainsKey("subjectTemplate") ? form["subjectTemplate"].ToString() : existingTemplate.Metadata.SubjectTemplate;
            var scopeRaw = form.ContainsKey("scope") ? form["scope"].ToString() : null;
            var hasInlineText = form.ContainsKey("textContent");
            var textContent = hasInlineText ? form["textContent"].ToString() : null;

            ReportTemplateScope scope;
            try
            {
                scope = scopeRaw is null
                    ? existingTemplate.Metadata.Scope
                    : ParseTemplateScope(scopeRaw);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"scope must be valid JSON: {ex.Message}" });
            }

            var selectedType = string.IsNullOrWhiteSpace(templateTypeId)
                ? ResolveTemplateDefinition(existingTemplate.Metadata, typeRegistry)
                : typeRegistry.GetByTypeId(templateTypeId.Trim());

            if (!string.IsNullOrWhiteSpace(templateTypeId) && selectedType is null)
                return Results.BadRequest(new { error = $"Unknown templateTypeId '{templateTypeId}'." });

            ReportTemplateKind kind;
            if (selectedType is not null)
            {
                kind = selectedType.Kind;
            }
            else if (!Enum.TryParse<ReportTemplateKind>(kindRaw, true, out kind))
            {
                return Results.BadRequest(new { error = $"kind must be one of: {string.Join(", ", Enum.GetNames<ReportTemplateKind>())}, or provide templateTypeId." });
            }

            Stream? contentStream = null;
            var originalFileName = existingTemplate.Metadata.FileName;

            try
            {
                if (file is not null && file.Length > 0)
                {
                    contentStream = file.OpenReadStream();
                    originalFileName = file.FileName;
                }
                else if (hasInlineText)
                {
                    if (selectedType is not null && !selectedType.SupportsInlineEdit)
                        return Results.BadRequest(new { error = "The selected template type does not support inline editing. Upload a replacement file instead." });

                    var inlineExtension = selectedType?.PrimaryExtension ?? existingTemplate.Metadata.Extension;
                    contentStream = new MemoryStream(Encoding.UTF8.GetBytes(textContent ?? string.Empty));
                    originalFileName = $"template{inlineExtension}";
                }

                try
                {
                    var metadata = await templateStore.UpdateAsync(templateId, new ReportTemplateUploadRequest
                    {
                        TemplateTypeId = selectedType?.TypeId,
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? existingTemplate.Metadata.DisplayName : displayName.Trim(),
                        Kind = kind,
                        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                        SubjectTemplate = string.IsNullOrWhiteSpace(subjectTemplate) ? null : subjectTemplate.Trim(),
                        Scope = scope,
                        OriginalFileName = originalFileName,
                        UploadedBy = ResolveRequestedBy(user) ?? "anonymous",
                        IsStarterTemplate = existingTemplate.Metadata.IsStarterTemplate
                    }, contentStream, ct);

                    return Results.Ok(MapTemplate(metadata, typeRegistry));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
            finally
            {
                if (contentStream is not null)
                    await contentStream.DisposeAsync();
            }
        })
        .DisableAntiforgery()
        .WithTags("Report Templates")
        .WithName("UpdateReportTemplate");

        app.MapDelete("/report-templates/{templateId}", async (
            string templateId,
            IReportTemplateStore templateStore,
            CancellationToken ct) =>
        {
            var deleted = await templateStore.DeleteAsync(templateId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Report Templates")
        .WithName("DeleteReportTemplate");

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

            if (!TryBuildSnapshotIngestMetadata(request, form, user, file, SnapshotUploadChannels.ManualSync, automated: false, out var metadata, out var metadataError))
                return Results.BadRequest(new { error = metadataError });

            await using var stream = file.OpenReadStream();
            var result = IsJson(file)
                ? await ingestService.IngestJsonAsync(stream, itsmSource.Trim(), snapshotDate, metadata, ct)
                : await ingestService.IngestCsvAsync(stream, itsmSource.Trim(), snapshotDate, metadata, ct);

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

            if (!TryBuildSnapshotIngestMetadata(request, form, user, file, SnapshotUploadChannels.ManualJob, automated: false, out var metadata, out var metadataError))
                return Results.BadRequest(new { error = metadataError });

            var job = await QueueSnapshotIngestJobAsync(
                artifacts,
                jobs,
                file,
                itsmSource.Trim(),
                snapshotDate,
                metadata,
                ct);

            return Results.Accepted($"/api/v1/jobs/{job.Id}", MapJob(job));
        })
        .DisableAntiforgery()
        .WithTags("Jobs")
        .WithName("QueueSnapshotIngest");

        app.MapPost("/jobs/snapshot-ingest/automated", async (
            HttpRequest request,
            IArtifactStorage artifacts,
            IBackgroundJobService jobs,
            ISnapshotDuplicateDetector duplicateDetector,
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

            if (!TryBuildSnapshotIngestMetadata(request, form, user, file, SnapshotUploadChannels.AutomatedApi, automated: true, out var metadata, out var metadataError))
                return Results.BadRequest(new { error = metadataError });

            var outcome = await SubmitAutomatedSnapshotIngestAsync(
                artifacts,
                jobs,
                duplicateDetector,
                file,
                itsmSource.Trim(),
                snapshotDate,
                metadata,
                ct);

            var response = new AutomatedSnapshotIngestResponse(
                outcome.Queued,
                outcome.DuplicateDetected,
                outcome.ExistingSnapshotId,
                outcome.DuplicateReason,
                outcome.ContentSha256,
                outcome.Job is null ? null : MapJob(outcome.Job));

            return outcome.Queued
                ? Results.Accepted($"/api/v1/jobs/{outcome.Job!.Id}", response)
                : Results.Ok(response);
        })
        .DisableAntiforgery()
        .WithTags("Jobs")
        .WithName("QueueAutomatedSnapshotIngest");

        app.MapPost("/jobs/snapshot-ingest/bulk", async (
            HttpRequest request,
            IArtifactStorage artifacts,
            IBackgroundJobService jobs,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var files = form.Files.GetFiles("files");
            var itsmSource = form["itsmSource"].ToString();

            if (files.Count == 0)
                return Results.BadRequest(new { error = "At least one snapshot file is required." });

            if (string.IsNullOrWhiteSpace(itsmSource))
                return Results.BadRequest(new { error = "itsmSource is required." });

            if (!TryResolveSnapshotDates(form, files.Count, out var snapshotDates, out var snapshotDateError))
                return Results.BadRequest(new { error = snapshotDateError });

            var trimmedSource = itsmSource.Trim();
            var queuedJobs = new List<BackgroundJob>(files.Count);

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                if (file.Length == 0)
                    return Results.BadRequest(new { error = $"File {index + 1} is empty." });

                if (!TryBuildSnapshotIngestMetadata(request, form, user, file, SnapshotUploadChannels.ManualBulkJob, automated: false, out var metadata, out var metadataError))
                    return Results.BadRequest(new { error = metadataError });

                var job = await QueueSnapshotIngestJobAsync(
                    artifacts,
                    jobs,
                    file,
                    trimmedSource,
                    snapshotDates[index],
                    metadata,
                    ct);

                queuedJobs.Add(job);
            }

            return Results.Accepted(
                "/api/v1/jobs",
                new BulkBackgroundJobResponse(
                    queuedJobs.Count,
                    queuedJobs.Select(MapJob).ToArray()));
        })
        .DisableAntiforgery()
        .WithTags("Jobs")
        .WithName("QueueBulkSnapshotIngest");

        app.MapPost("/jobs/report-execution", async (
            ExecuteReportRequest request,
            IBackgroundJobService jobs,
            IReportTemplateApplicabilityService templateApplicability,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PluginId))
                return Results.BadRequest(new { error = "pluginId is required." });

            if (string.Equals(request.PluginId.Trim(), TokenisedTemplateReportPlugin.PluginIdValue, StringComparison.OrdinalIgnoreCase))
            {
                var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                parameters.TryGetValue("template_id", out var templateId);
                parameters.TryGetValue("itsm_source", out var itsmSource);
                parameters.TryGetValue("company", out var company);

                var validation = templateApplicability.ValidateSelection(templateId, itsmSource, company);
                if (!validation.IsValid)
                    return Results.BadRequest(new { error = validation.ErrorMessage });
            }

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

    private static async Task<BackgroundJob> QueueSnapshotIngestJobAsync(
        IArtifactStorage artifacts,
        IBackgroundJobService jobs,
        IFormFile file,
        string itsmSource,
        DateOnly snapshotDate,
        SnapshotIngestMetadata metadata,
        CancellationToken ct)
    {
        var prepared = await PrepareSnapshotIngestPayloadAsync(
            artifacts,
            file,
            itsmSource,
            snapshotDate,
            metadata,
            ct);

        try
        {
            return await jobs.EnqueueAsync(
                BackgroundJobTypes.SnapshotIngest,
                JsonSerializer.Serialize(prepared.Payload, JsonOptions),
                prepared.Metadata.RequestedBy,
                ct);
        }
        catch
        {
            artifacts.Delete(prepared.StoredArtifact.RelativePath);
            throw;
        }
    }

    private static async Task<SnapshotIngestQueueOutcome> SubmitAutomatedSnapshotIngestAsync(
        IArtifactStorage artifacts,
        IBackgroundJobService jobs,
        ISnapshotDuplicateDetector duplicateDetector,
        IFormFile file,
        string itsmSource,
        DateOnly snapshotDate,
        SnapshotIngestMetadata metadata,
        CancellationToken ct)
    {
        var prepared = await PrepareSnapshotIngestPayloadAsync(
            artifacts,
            file,
            itsmSource,
            snapshotDate,
            metadata,
            ct);

        var duplicateJob = await jobs.FindMatchingSnapshotIngestAsync(itsmSource, snapshotDate, prepared.Metadata, ct);
        if (duplicateJob is not null)
        {
            artifacts.Delete(prepared.StoredArtifact.RelativePath);
            return new SnapshotIngestQueueOutcome(
                Queued: false,
                DuplicateDetected: true,
                Job: duplicateJob,
                ExistingSnapshotId: null,
                DuplicateReason: "Matching automated snapshot upload already queued or processed.",
                ContentSha256: prepared.StoredArtifact.Sha256);
        }

        var duplicateSnapshot = await duplicateDetector.FindExistingSnapshotAsync(itsmSource, snapshotDate, prepared.Metadata, ct);
        if (duplicateSnapshot is not null)
        {
            artifacts.Delete(prepared.StoredArtifact.RelativePath);
            return new SnapshotIngestQueueOutcome(
                Queued: false,
                DuplicateDetected: true,
                Job: null,
                ExistingSnapshotId: duplicateSnapshot.SnapshotId,
                DuplicateReason: duplicateSnapshot.Reason,
                ContentSha256: prepared.StoredArtifact.Sha256);
        }

        try
        {
            var job = await jobs.EnqueueAsync(
                BackgroundJobTypes.SnapshotIngest,
                JsonSerializer.Serialize(prepared.Payload, JsonOptions),
                prepared.Metadata.RequestedBy,
                ct);

            return new SnapshotIngestQueueOutcome(
                Queued: true,
                DuplicateDetected: false,
                Job: job,
                ExistingSnapshotId: null,
                DuplicateReason: null,
                ContentSha256: prepared.StoredArtifact.Sha256);
        }
        catch
        {
            artifacts.Delete(prepared.StoredArtifact.RelativePath);
            throw;
        }
    }

    private static async Task<PreparedSnapshotIngestPayload> PrepareSnapshotIngestPayloadAsync(
        IArtifactStorage artifacts,
        IFormFile file,
        string itsmSource,
        DateOnly snapshotDate,
        SnapshotIngestMetadata metadata,
        CancellationToken ct)
    {
        await using var uploadStream = file.OpenReadStream();
        var stored = await artifacts.SaveAsync("uploads", file.FileName, uploadStream, file.ContentType, ct);
        var normalizedMetadata = SnapshotIngestMetadataHelper.Normalize(
            metadata,
            file.FileName,
            file.ContentType,
            metadata.UploadChannel,
            metadata.IsAutomated,
            stored.RelativePath,
            stored.Sha256,
            metadata.RequestedBy);

        var payload = new SnapshotIngestJobPayload(
            itsmSource,
            snapshotDate,
            stored.RelativePath,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            normalizedMetadata);

        return new PreparedSnapshotIngestPayload(stored, normalizedMetadata, payload);
    }

    private static bool TryBuildSnapshotIngestMetadata(
        HttpRequest request,
        IFormCollection form,
        ClaimsPrincipal user,
        IFormFile file,
        string defaultUploadChannel,
        bool automated,
        out SnapshotIngestMetadata metadata,
        out string? error)
    {
        metadata = default!;
        error = null;

        var timestampRaw = FirstNonEmpty(form["timestamp"].ToString(), form["sourceTimestampUtc"].ToString(), form["submittedAtUtc"].ToString());
        DateTime? sourceTimestampUtc = null;
        if (!string.IsNullOrWhiteSpace(timestampRaw))
        {
            if (!DateTimeOffset.TryParse(timestampRaw, out var parsedTimestamp))
            {
                error = "timestamp must be a valid ISO-8601 date/time.";
                return false;
            }

            sourceTimestampUtc = parsedTimestamp.UtcDateTime;
        }

        var requestedBy = ResolveRequestedBy(user);
        metadata = SnapshotIngestMetadataHelper.Normalize(
            new SnapshotIngestMetadata
            {
                UploadChannel = FirstNonEmpty(form["uploadChannel"].ToString(), defaultUploadChannel) ?? defaultUploadChannel,
                SourceSystem = FirstNonEmpty(form["sourceSystem"].ToString()),
                Producer = FirstNonEmpty(form["producer"].ToString()),
                OriginalFileName = file.FileName,
                CorrelationId = FirstNonEmpty(form["correlationId"].ToString(), form["referenceId"].ToString(), request.Headers["X-Correlation-Id"].ToString()),
                SubmittedAtUtc = DateTime.UtcNow,
                SourceTimestampUtc = sourceTimestampUtc,
                ContentType = file.ContentType,
                ContentSha256 = null,
                SourceFileIdentity = FirstNonEmpty(form["sourceFileIdentity"].ToString(), form["sourceFileId"].ToString()),
                IdempotencyKey = FirstNonEmpty(form["idempotencyKey"].ToString(), request.Headers["Idempotency-Key"].ToString()),
                ArtifactPath = null,
                RequestedBy = requestedBy,
                IsAutomated = automated
            },
            file.FileName,
            file.ContentType,
            defaultUploadChannel,
            automated,
            artifactPath: null,
            contentSha256: null,
            requestedBy: requestedBy);

        return true;
    }

    private static bool TryResolveSnapshotDates(
        IFormCollection form,
        int expectedCount,
        out IReadOnlyList<DateOnly> snapshotDates,
        out string? error)
    {
        snapshotDates = Array.Empty<DateOnly>();
        error = null;

        var explicitDates = form["snapshotDates"]
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (explicitDates.Length == 0)
        {
            var snapshotDateRaw = form["snapshotDate"].ToString();
            if (!DateOnly.TryParse(snapshotDateRaw, out var snapshotDate))
            {
                error = "snapshotDate must be a valid date (yyyy-MM-dd).";
                return false;
            }

            snapshotDates = Enumerable.Repeat(snapshotDate, expectedCount).ToArray();
            return true;
        }

        if (explicitDates.Length != expectedCount)
        {
            error = $"snapshotDates must contain exactly {expectedCount} value(s).";
            return false;
        }

        var resolvedDates = new DateOnly[expectedCount];
        for (var index = 0; index < explicitDates.Length; index++)
        {
            if (!DateOnly.TryParse(explicitDates[index], out var parsedDate))
            {
                error = $"snapshotDates[{index}] must be a valid date (yyyy-MM-dd).";
                return false;
            }

            resolvedDates[index] = parsedDate;
        }

        snapshotDates = resolvedDates;
        return true;
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private sealed record PreparedSnapshotIngestPayload(
        StoredArtifact StoredArtifact,
        SnapshotIngestMetadata Metadata,
        SnapshotIngestJobPayload Payload);

    private sealed record SnapshotIngestQueueOutcome(
        bool Queued,
        bool DuplicateDetected,
        BackgroundJob? Job,
        Guid? ExistingSnapshotId,
        string? DuplicateReason,
        string? ContentSha256);

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
            result.DuplicateDetected,
            result.DuplicateOfSnapshotId,
            result.DuplicateReason,
            result.ErrorMessage,
            result.Warnings.ToArray());
    }

    private static ReportTemplateTypeDefinition? ResolveTemplateDefinition(ReportTemplateMetadata template, ReportTemplateTypeRegistry typeRegistry)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(typeRegistry);

        return typeRegistry.Resolve(template.TemplateTypeId, template.Extension, template.Kind);
    }

    private static ReportTemplateResponse MapTemplate(ReportTemplateMetadata template, ReportTemplateTypeRegistry typeRegistry)
    {
        var typeDefinition = ResolveTemplateDefinition(template, typeRegistry);
        var updatedAt = template.UpdatedAt ?? template.UploadedAt;
        var updatedBy = string.IsNullOrWhiteSpace(template.UpdatedBy) ? template.UploadedBy : template.UpdatedBy;

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
            template.UploadedBy,
            updatedAt,
            updatedBy,
            typeDefinition?.TypeId ?? template.TemplateTypeId,
            typeDefinition?.DisplayName,
            typeDefinition?.SupportsInlineEdit ?? ReportTemplateContentTypeMapper.IsTextLike(template.Extension),
            template.IsStarterTemplate,
            MapTemplateScope(template.Scope));
    }

    private static ReportTemplateDetailResponse MapTemplateDetail(StoredReportTemplate template, ReportTemplateTypeRegistry typeRegistry)
    {
        var typeDefinition = ResolveTemplateDefinition(template.Metadata, typeRegistry);
        var updatedAt = template.Metadata.UpdatedAt ?? template.Metadata.UploadedAt;
        var updatedBy = string.IsNullOrWhiteSpace(template.Metadata.UpdatedBy) ? template.Metadata.UploadedBy : template.Metadata.UpdatedBy;
        var supportsInlineEdit = typeDefinition?.SupportsInlineEdit ?? ReportTemplateContentTypeMapper.IsTextLike(template.Metadata.Extension);
        var editableTextContent = supportsInlineEdit && ReportTemplateContentTypeMapper.IsTextLike(template.Metadata.Extension)
            ? Encoding.UTF8.GetString(template.FileContent)
            : null;

        return new ReportTemplateDetailResponse(
            template.Metadata.Id,
            template.Metadata.DisplayName,
            template.Metadata.Kind.ToString(),
            template.Metadata.Description,
            template.Metadata.SubjectTemplate,
            template.Metadata.FileName,
            template.Metadata.Extension,
            template.Metadata.ContentType,
            template.Metadata.UploadedAt,
            template.Metadata.UploadedBy,
            updatedAt,
            updatedBy,
            typeDefinition?.TypeId ?? template.Metadata.TemplateTypeId,
            typeDefinition?.DisplayName,
            supportsInlineEdit,
            template.Metadata.IsStarterTemplate,
            MapTemplateScope(template.Metadata.Scope),
            typeDefinition?.AuthoringGuidance,
            editableTextContent);
    }

    private static ReportTemplateScopeResponse MapTemplateScope(ReportTemplateScope? scope)
    {
        var normalized = ReportTemplateScopeEvaluator.Normalize(scope);
        return new ReportTemplateScopeResponse(
            normalized.IsGlobal,
            normalized.ItsmSources,
            normalized.Companies,
            normalized.ItsmSourceCompanies
                .Select(pair => new ReportTemplateScopeCombinationResponse(pair.ItsmSource, pair.Company))
                .ToArray());
    }

    private static ReportTemplateScope ParseTemplateScope(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ReportTemplateScopeEvaluator.Normalize(null);

        var parsed = JsonSerializer.Deserialize<ReportTemplateScope>(raw, JsonOptions);
        return ReportTemplateScopeEvaluator.Normalize(parsed);
    }

    private static ReportTemplateTypeResponse MapTemplateType(ReportTemplateTypeDefinition typeDefinition)
    {
        return new ReportTemplateTypeResponse(
            typeDefinition.TypeId,
            typeDefinition.DisplayName,
            typeDefinition.Kind.ToString(),
            typeDefinition.PrimaryExtension,
            typeDefinition.Extensions.ToArray(),
            typeDefinition.ContentType,
            typeDefinition.SupportsInlineEdit,
            typeDefinition.IsTextLike,
            typeDefinition.IsOoxmlPackage,
            typeDefinition.Description,
            typeDefinition.AuthoringGuidance,
            typeDefinition.StarterTemplateDisplayName,
            typeDefinition.StarterTemplateDescription,
            typeDefinition.DefaultSubjectTemplate);
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
    bool DuplicateDetected,
    Guid? DuplicateOfSnapshotId,
    string? DuplicateReason,
    string? ErrorMessage,
    IReadOnlyList<string> Warnings);

public sealed record AutomatedSnapshotIngestResponse(
    bool Queued,
    bool DuplicateDetected,
    Guid? ExistingSnapshotId,
    string? DuplicateReason,
    string? ContentSha256,
    BackgroundJobResponse? Job);

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

public sealed record ReportTemplateTypeResponse(
    string TypeId,
    string DisplayName,
    string Kind,
    string PrimaryExtension,
    IReadOnlyList<string> Extensions,
    string ContentType,
    bool SupportsInlineEdit,
    bool IsTextLike,
    bool IsOoxmlPackage,
    string? Description,
    string? AuthoringGuidance,
    string? StarterTemplateDisplayName,
    string? StarterTemplateDescription,
    string? DefaultSubjectTemplate);

public sealed record ReportTemplateScopeCombinationResponse(
    string ItsmSource,
    string Company);

public sealed record ReportTemplateScopeResponse(
    bool IsGlobal,
    IReadOnlyList<string> ItsmSources,
    IReadOnlyList<string> Companies,
    IReadOnlyList<ReportTemplateScopeCombinationResponse> ItsmSourceCompanies);

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
    string UploadedBy,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy,
    string? TemplateTypeId,
    string? TypeDisplayName,
    bool SupportsInlineEdit,
    bool IsStarterTemplate,
    ReportTemplateScopeResponse Scope);

public sealed record ReportTemplateDetailResponse(
    string Id,
    string DisplayName,
    string Kind,
    string? Description,
    string? SubjectTemplate,
    string FileName,
    string Extension,
    string ContentType,
    DateTimeOffset UploadedAt,
    string UploadedBy,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy,
    string? TemplateTypeId,
    string? TypeDisplayName,
    bool SupportsInlineEdit,
    bool IsStarterTemplate,
    ReportTemplateScopeResponse Scope,
    string? AuthoringGuidance,
    string? EditableTextContent);

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

public sealed record BulkBackgroundJobResponse(
    int QueuedCount,
    IReadOnlyList<BackgroundJobResponse> Jobs);
