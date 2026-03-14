namespace UPMS.Data.ProblemRequests;

public class ProblemRequestComment
{
    public Guid Id { get; set; }
    public Guid ProblemRequestId { get; set; }
    public string? AuthorName { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ProblemRequest? ProblemRequest { get; set; }
}
