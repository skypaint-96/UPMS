namespace UPMS.Api.Endpoints;

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UPMS.Data.ProblemRequests;

public static class ProblemRequestApiEndpoints
{
    public static IEndpointRouteBuilder MapProblemRequestApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problem-requests", async (
            string? status,
            string? assignee,
            string? company,
            string? itsmSource,
            IProblemRequestService requests,
            CancellationToken ct) =>
        {
            try
            {
                var rows = await requests.GetAllAsync(new ProblemRequestQuery(status, assignee, company, itsmSource), ct);
                return Results.Ok(rows.Select(MapSummary));
            }
            catch (ProblemRequestValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
        .WithTags("Problem Requests")
        .WithName("GetProblemRequests");

        app.MapGet("/problem-requests/{id:guid}", async (Guid id, IProblemRequestService requests, CancellationToken ct) =>
        {
            var request = await requests.GetByIdAsync(id, ct);
            return request is null ? Results.NotFound() : Results.Ok(MapDetail(request));
        })
        .WithTags("Problem Requests")
        .WithName("GetProblemRequestById");

        app.MapPost("/problem-requests", async (
            CreateProblemRequestRequest request,
            IProblemRequestService requests,
            CancellationToken ct) =>
        {
            try
            {
                var created = await requests.CreateAsync(new CreateProblemRequestCommand(
                    request.RequesterName,
                    request.RequesterEmail,
                    request.RequesterTeam,
                    request.CompanyName,
                    request.ItsmSource,
                    request.Title,
                    request.Description,
                    request.Justification,
                    request.Assignee),
                    ct);

                var loaded = await requests.GetByIdAsync(created.Id, ct) ?? created;
                return Results.Created($"/api/v1/problem-requests/{loaded.Id}", MapDetail(loaded));
            }
            catch (ProblemRequestValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
        .WithTags("Problem Requests")
        .WithName("CreateProblemRequest");

        app.MapPatch("/problem-requests/{id:guid}", async (
            Guid id,
            UpdateProblemRequestRequest request,
            IProblemRequestService requests,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await requests.UpdateAsync(id, new UpdateProblemRequestCommand(
                    request.RequesterName,
                    request.RequesterEmail,
                    request.RequesterTeam,
                    request.CompanyName,
                    request.ItsmSource,
                    request.Title,
                    request.Description,
                    request.Justification,
                    request.Status,
                    request.Assignee,
                    request.DecisionReason,
                    request.Comment,
                    ResolveActor(user)),
                    ct);

                return Results.Ok(MapDetail(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ProblemRequestValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
        .WithTags("Problem Requests")
        .WithName("UpdateProblemRequest");

        app.MapPost("/problem-requests/{id:guid}/comments", async (
            Guid id,
            AddProblemRequestCommentRequest request,
            IProblemRequestService requests,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            try
            {
                await requests.AddCommentAsync(id, new AddProblemRequestCommentCommand(
                    request.CommentText,
                    string.IsNullOrWhiteSpace(request.AuthorName) ? ResolveActor(user) : request.AuthorName),
                    ct);

                var loaded = await requests.GetByIdAsync(id, ct);
                return loaded is null ? Results.NotFound() : Results.Ok(MapDetail(loaded));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ProblemRequestValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
        .WithTags("Problem Requests")
        .WithName("AddProblemRequestComment");

        app.MapPost("/problem-requests/{id:guid}/link", async (
            Guid id,
            LinkProblemRequestRequest request,
            IProblemRequestService requests,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            try
            {
                var linked = await requests.LinkAsync(id, new LinkProblemRequestCommand(
                    request.ProblemReference,
                    request.ProblemItsmSource,
                    request.ProblemCompanyName,
                    request.ProblemTicketKey,
                    request.Comment,
                    string.IsNullOrWhiteSpace(request.LinkedBy) ? ResolveActor(user) : request.LinkedBy),
                    ct);

                return Results.Ok(MapDetail(linked));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ProblemRequestValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
        .WithTags("Problem Requests")
        .WithName("LinkProblemRequest");

        return app;
    }

    private static IResult ValidationProblem(ProblemRequestValidationException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [ex.FieldName] = [ex.Message]
        });
    }

    private static string ResolveActor(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated ?? false)
            return user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        return "anonymous";
    }

    private static ProblemRequestSummaryResponse MapSummary(ProblemRequest request)
    {
        return new ProblemRequestSummaryResponse(
            request.Id,
            request.RequesterName,
            request.RequesterEmail,
            request.RequesterTeam,
            request.CompanyName,
            request.ItsmSource,
            request.Title,
            request.Status,
            request.Assignee,
            request.ProblemReference,
            request.CreatedAt,
            request.UpdatedAt,
            request.LinkedAt);
    }

    private static ProblemRequestDetailResponse MapDetail(ProblemRequest request)
    {
        return new ProblemRequestDetailResponse(
            request.Id,
            request.RequesterName,
            request.RequesterEmail,
            request.RequesterTeam,
            request.CompanyName,
            request.ItsmSource,
            request.Title,
            request.Description,
            request.Justification,
            request.Status,
            request.Assignee,
            request.DecisionReason,
            request.ProblemReference,
            request.ProblemItsmSource,
            request.ProblemCompanyName,
            request.ProblemTicketKey,
            request.CreatedAt,
            request.UpdatedAt,
            request.LinkedAt,
            request.Comments
                .OrderBy(comment => comment.CreatedAt)
                .ThenBy(comment => comment.Id)
                .Select(MapComment)
                .ToArray());
    }

    private static ProblemRequestCommentResponse MapComment(ProblemRequestComment comment)
    {
        return new ProblemRequestCommentResponse(
            comment.Id,
            comment.AuthorName,
            comment.CommentText,
            comment.CreatedAt);
    }
}

public sealed record ProblemRequestSummaryResponse(
    Guid Id,
    string RequesterName,
    string? RequesterEmail,
    string? RequesterTeam,
    string? CompanyName,
    string? ItsmSource,
    string Title,
    string Status,
    string? Assignee,
    string? ProblemReference,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LinkedAt);

public sealed record ProblemRequestDetailResponse(
    Guid Id,
    string RequesterName,
    string? RequesterEmail,
    string? RequesterTeam,
    string? CompanyName,
    string? ItsmSource,
    string Title,
    string Description,
    string Justification,
    string Status,
    string? Assignee,
    string? DecisionReason,
    string? ProblemReference,
    string? ProblemItsmSource,
    string? ProblemCompanyName,
    string? ProblemTicketKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LinkedAt,
    IReadOnlyList<ProblemRequestCommentResponse> Comments);

public sealed record ProblemRequestCommentResponse(
    Guid Id,
    string? AuthorName,
    string CommentText,
    DateTime CreatedAt);

public sealed record CreateProblemRequestRequest(
    string RequesterName,
    string? RequesterEmail,
    string? RequesterTeam,
    string? CompanyName,
    string? ItsmSource,
    string Title,
    string Description,
    string Justification,
    string? Assignee);

public sealed record UpdateProblemRequestRequest(
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterTeam,
    string? CompanyName,
    string? ItsmSource,
    string? Title,
    string? Description,
    string? Justification,
    string? Status,
    string? Assignee,
    string? DecisionReason,
    string? Comment);

public sealed record AddProblemRequestCommentRequest(
    string CommentText,
    string? AuthorName);

public sealed record LinkProblemRequestRequest(
    string? ProblemReference,
    string? ProblemItsmSource,
    string? ProblemCompanyName,
    string? ProblemTicketKey,
    string? Comment,
    string? LinkedBy);
