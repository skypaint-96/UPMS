namespace UPMS.Data.ProblemRequests;

public sealed record ProblemRequestQuery(
    string? Status,
    string? Assignee,
    string? CompanyName,
    string? ItsmSource);

public sealed record CreateProblemRequestCommand(
    string RequesterName,
    string? RequesterEmail,
    string? RequesterTeam,
    string? CompanyName,
    string? ItsmSource,
    string Title,
    string Description,
    string Justification,
    string? Assignee);

public sealed record UpdateProblemRequestCommand(
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
    string? Comment,
    string? UpdatedBy);

public sealed record AddProblemRequestCommentCommand(
    string CommentText,
    string? AuthorName);

public sealed record LinkProblemRequestCommand(
    string? ProblemReference,
    string? ProblemItsmSource,
    string? ProblemCompanyName,
    string? ProblemTicketKey,
    string? Comment,
    string? LinkedBy);
