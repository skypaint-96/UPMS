namespace UPMS.Data.ProblemRequests;

public interface IProblemRequestService
{
    Task<IReadOnlyList<ProblemRequest>> GetAllAsync(ProblemRequestQuery? query = null, CancellationToken ct = default);
    Task<ProblemRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProblemRequest> CreateAsync(CreateProblemRequestCommand command, CancellationToken ct = default);
    Task<ProblemRequest> UpdateAsync(Guid id, UpdateProblemRequestCommand command, CancellationToken ct = default);
    Task<ProblemRequestComment> AddCommentAsync(Guid id, AddProblemRequestCommentCommand command, CancellationToken ct = default);
    Task<ProblemRequest> LinkAsync(Guid id, LinkProblemRequestCommand command, CancellationToken ct = default);
}
