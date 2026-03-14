namespace UPMS.Data.ProblemRequests;

public class ProblemRequest
{
    public Guid Id { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterEmail { get; set; }
    public string? RequesterTeam { get; set; }
    public string? CompanyName { get; set; }
    public string? ItsmSource { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string Status { get; set; } = ProblemRequestStatuses.New;
    public string? Assignee { get; set; }
    public string? DecisionReason { get; set; }
    public string? ProblemReference { get; set; }
    public string? ProblemItsmSource { get; set; }
    public string? ProblemCompanyName { get; set; }
    public string? ProblemTicketKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LinkedAt { get; set; }

    public ICollection<ProblemRequestComment> Comments { get; set; } = new List<ProblemRequestComment>();
}
