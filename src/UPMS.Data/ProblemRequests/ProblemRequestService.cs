namespace UPMS.Data.ProblemRequests;

using System.Net.Mail;
using Microsoft.EntityFrameworkCore;

public class ProblemRequestService : IProblemRequestService
{
    private readonly UpmsDbContext _context;

    public ProblemRequestService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ProblemRequest>> GetAllAsync(ProblemRequestQuery? query = null, CancellationToken ct = default)
    {
        IQueryable<ProblemRequest> requests = _context.ProblemRequests.AsNoTracking();

        if (query is not null)
        {
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var normalizedStatus = ProblemRequestStatuses.Normalize(query.Status);
                requests = requests.Where(request => request.Status == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(query.Assignee))
            {
                var assignee = query.Assignee.Trim().ToLowerInvariant();
                requests = requests.Where(request => request.Assignee != null && request.Assignee.ToLower().Contains(assignee));
            }

            if (!string.IsNullOrWhiteSpace(query.CompanyName))
            {
                var companyName = query.CompanyName.Trim().ToLowerInvariant();
                requests = requests.Where(request => request.CompanyName != null && request.CompanyName.ToLower().Contains(companyName));
            }

            if (!string.IsNullOrWhiteSpace(query.ItsmSource))
            {
                var itsmSource = query.ItsmSource.Trim().ToLowerInvariant();
                requests = requests.Where(request => request.ItsmSource != null && request.ItsmSource.ToLower().Contains(itsmSource));
            }
        }

        return await requests
            .OrderByDescending(request => request.UpdatedAt)
            .ThenByDescending(request => request.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ProblemRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var request = await _context.ProblemRequests
            .AsNoTracking()
            .Include(row => row.Comments)
            .FirstOrDefaultAsync(row => row.Id == id, ct);

        if (request is null)
            return null;

        request.Comments = request.Comments
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToList();

        return request;
    }

    public async Task<ProblemRequest> CreateAsync(CreateProblemRequestCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = DateTime.UtcNow;
        var request = new ProblemRequest
        {
            Id = Guid.NewGuid(),
            RequesterName = NormalizeRequired(command.RequesterName, "requesterName", "Requester name"),
            RequesterEmail = NormalizeEmail(command.RequesterEmail),
            RequesterTeam = NormalizeOptional(command.RequesterTeam),
            CompanyName = NormalizeOptional(command.CompanyName),
            ItsmSource = NormalizeOptional(command.ItsmSource),
            Title = NormalizeRequired(command.Title, "title", "Title"),
            Description = NormalizeRequired(command.Description, "description", "Description"),
            Justification = NormalizeRequired(command.Justification, "justification", "Reason / justification"),
            Status = ProblemRequestStatuses.New,
            Assignee = NormalizeOptional(command.Assignee),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ProblemRequests.Add(request);
        await _context.SaveChangesAsync(ct);
        return request;
    }

    public async Task<ProblemRequest> UpdateAsync(Guid id, UpdateProblemRequestCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _context.ProblemRequests
            .Include(row => row.Comments)
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"Problem request '{id}' was not found.");

        var now = DateTime.UtcNow;
        var changed = false;

        if (command.RequesterName is not null)
        {
            var value = NormalizeRequired(command.RequesterName, "requesterName", "Requester name");
            if (!string.Equals(request.RequesterName, value, StringComparison.Ordinal))
            {
                request.RequesterName = value;
                changed = true;
            }
        }

        if (command.RequesterEmail is not null)
        {
            var value = NormalizeEmail(command.RequesterEmail);
            if (!string.Equals(request.RequesterEmail, value, StringComparison.Ordinal))
            {
                request.RequesterEmail = value;
                changed = true;
            }
        }

        if (command.RequesterTeam is not null)
        {
            var value = NormalizeOptional(command.RequesterTeam);
            if (!string.Equals(request.RequesterTeam, value, StringComparison.Ordinal))
            {
                request.RequesterTeam = value;
                changed = true;
            }
        }

        if (command.CompanyName is not null)
        {
            var value = NormalizeOptional(command.CompanyName);
            if (!string.Equals(request.CompanyName, value, StringComparison.Ordinal))
            {
                request.CompanyName = value;
                changed = true;
            }
        }

        if (command.ItsmSource is not null)
        {
            var value = NormalizeOptional(command.ItsmSource);
            if (!string.Equals(request.ItsmSource, value, StringComparison.Ordinal))
            {
                request.ItsmSource = value;
                changed = true;
            }
        }

        if (command.Title is not null)
        {
            var value = NormalizeRequired(command.Title, "title", "Title");
            if (!string.Equals(request.Title, value, StringComparison.Ordinal))
            {
                request.Title = value;
                changed = true;
            }
        }

        if (command.Description is not null)
        {
            var value = NormalizeRequired(command.Description, "description", "Description");
            if (!string.Equals(request.Description, value, StringComparison.Ordinal))
            {
                request.Description = value;
                changed = true;
            }
        }

        if (command.Justification is not null)
        {
            var value = NormalizeRequired(command.Justification, "justification", "Reason / justification");
            if (!string.Equals(request.Justification, value, StringComparison.Ordinal))
            {
                request.Justification = value;
                changed = true;
            }
        }

        if (command.Status is not null)
        {
            var value = ProblemRequestStatuses.Normalize(command.Status);
            ValidateStatusUpdate(request.Status, value);
            if (!string.Equals(request.Status, value, StringComparison.Ordinal))
            {
                request.Status = value;
                changed = true;
            }
        }

        if (command.Assignee is not null)
        {
            var value = NormalizeOptional(command.Assignee);
            if (!string.Equals(request.Assignee, value, StringComparison.Ordinal))
            {
                request.Assignee = value;
                changed = true;
            }
        }

        if (command.DecisionReason is not null)
        {
            var value = NormalizeOptional(command.DecisionReason);
            if (!string.Equals(request.DecisionReason, value, StringComparison.Ordinal))
            {
                request.DecisionReason = value;
                changed = true;
            }
        }

        if (command.Comment is not null)
        {
            AddCommentEntity(request, command.UpdatedBy, command.Comment, now);
            changed = true;
        }

        if (changed)
        {
            request.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
        }

        request.Comments = request.Comments
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToList();

        return request;
    }

    public async Task<ProblemRequestComment> AddCommentAsync(Guid id, AddProblemRequestCommentCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _context.ProblemRequests
            .Include(row => row.Comments)
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"Problem request '{id}' was not found.");

        var now = DateTime.UtcNow;
        var comment = AddCommentEntity(request, command.AuthorName, command.CommentText, now);
        request.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<ProblemRequest> LinkAsync(Guid id, LinkProblemRequestCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _context.ProblemRequests
            .Include(row => row.Comments)
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"Problem request '{id}' was not found.");

        if (string.Equals(request.Status, ProblemRequestStatuses.Rejected, StringComparison.Ordinal))
        {
            throw new ProblemRequestValidationException("status", "Rejected requests cannot be linked. Move the request back under review or accepted first.");
        }

        var reference = NormalizeOptional(command.ProblemReference);
        var ticketKey = NormalizeOptional(command.ProblemTicketKey);
        if (reference is null && ticketKey is null)
        {
            throw new ProblemRequestValidationException("problemReference", "Problem reference or problem ticket key is required when linking a request.");
        }

        var now = DateTime.UtcNow;
        request.ProblemReference = reference ?? ticketKey;
        request.ProblemItsmSource = NormalizeOptional(command.ProblemItsmSource);
        request.ProblemCompanyName = NormalizeOptional(command.ProblemCompanyName);
        request.ProblemTicketKey = ticketKey;
        request.Status = ProblemRequestStatuses.ConvertedLinked;
        request.LinkedAt = now;
        request.UpdatedAt = now;

        var linkComment = string.IsNullOrWhiteSpace(command.Comment)
            ? $"Linked to problem reference '{request.ProblemReference}'."
            : $"Linked to problem reference '{request.ProblemReference}'. {command.Comment.Trim()}";

        AddCommentEntity(request, command.LinkedBy, linkComment, now);

        await _context.SaveChangesAsync(ct);

        request.Comments = request.Comments
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToList();

        return request;
    }

    private static void ValidateStatusUpdate(string currentStatus, string requestedStatus)
    {
        var current = ProblemRequestStatuses.Normalize(currentStatus);
        var requested = ProblemRequestStatuses.Normalize(requestedStatus);

        if (string.Equals(current, ProblemRequestStatuses.ConvertedLinked, StringComparison.Ordinal)
            && !string.Equals(requested, ProblemRequestStatuses.ConvertedLinked, StringComparison.Ordinal))
        {
            throw new ProblemRequestValidationException("status", "Converted / linked requests are treated as final in v1 and cannot be moved back to another state.");
        }

        if (!string.Equals(current, ProblemRequestStatuses.ConvertedLinked, StringComparison.Ordinal)
            && string.Equals(requested, ProblemRequestStatuses.ConvertedLinked, StringComparison.Ordinal))
        {
            throw new ProblemRequestValidationException("status", "Use the problem-link endpoint to move a request to 'Converted / Linked'.");
        }
    }

    private static ProblemRequestComment AddCommentEntity(ProblemRequest request, string? authorName, string commentText, DateTime createdAt)
    {
        var comment = new ProblemRequestComment
        {
            Id = Guid.NewGuid(),
            ProblemRequestId = request.Id,
            AuthorName = NormalizeOptional(authorName),
            CommentText = NormalizeRequired(commentText, "commentText", "Comment"),
            CreatedAt = createdAt
        };

        request.Comments.Add(comment);
        return comment;
    }

    private static string NormalizeRequired(string? value, string fieldName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ProblemRequestValidationException(fieldName, $"{displayName} is required.");

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
            return null;

        try
        {
            return new MailAddress(normalized).Address;
        }
        catch (FormatException)
        {
            throw new ProblemRequestValidationException("requesterEmail", "Requester email must be a valid email address.");
        }
    }
}
