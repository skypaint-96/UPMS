namespace UPMS.Data.Tests;

using Microsoft.EntityFrameworkCore;
using UPMS.Data.ProblemRequests;

[TestFixture]
public sealed class ProblemRequestServiceTests
{
    private static UpmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase($"problem-requests-{Guid.NewGuid():N}")
            .Options;

        return new UpmsDbContext(options);
    }

    private static CreateProblemRequestCommand BuildCreateCommand()
    {
        return new CreateProblemRequestCommand(
            RequesterName: "Alex Morgan",
            RequesterEmail: "alex.morgan@example.com",
            RequesterTeam: "Operations",
            CompanyName: "Contoso",
            ItsmSource: "servicenow-prod",
            Title: "Recurring payment job failures",
            Description: "Multiple incidents indicate the same overnight payment batch is failing.",
            Justification: "The issue is cross-team, recurring, and likely needs problem review.",
            Assignee: "problem.coordinator");
    }

    [Test]
    public async Task CreateAsync_persists_request_with_new_status()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);

        var created = await service.CreateAsync(BuildCreateCommand());
        var loaded = await service.GetByIdAsync(created.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Status, Is.EqualTo(ProblemRequestStatuses.New));
        Assert.That(loaded.Title, Is.EqualTo("Recurring payment job failures"));
        Assert.That(loaded.RequesterName, Is.EqualTo("Alex Morgan"));
        Assert.That(loaded.Comments, Is.Empty);
    }

    [Test]
    public async Task CreateAsync_throws_validation_exception_when_required_fields_are_missing()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);

        Assert.ThrowsAsync<ProblemRequestValidationException>(async () =>
            await service.CreateAsync(new CreateProblemRequestCommand(
                RequesterName: "",
                RequesterEmail: null,
                RequesterTeam: null,
                CompanyName: null,
                ItsmSource: null,
                Title: "",
                Description: "",
                Justification: "",
                Assignee: null)));
    }

    [Test]
    public async Task UpdateAsync_allows_triage_state_transitions_between_non_linked_statuses()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);
        var created = await service.CreateAsync(BuildCreateCommand());

        await service.UpdateAsync(created.Id, new UpdateProblemRequestCommand(
            RequesterName: null,
            RequesterEmail: null,
            RequesterTeam: null,
            CompanyName: null,
            ItsmSource: null,
            Title: null,
            Description: null,
            Justification: null,
            Status: ProblemRequestStatuses.UnderReview,
            Assignee: "triage.owner",
            DecisionReason: null,
            Comment: null,
            UpdatedBy: null));

        var accepted = await service.UpdateAsync(created.Id, new UpdateProblemRequestCommand(
            RequesterName: null,
            RequesterEmail: null,
            RequesterTeam: null,
            CompanyName: null,
            ItsmSource: null,
            Title: null,
            Description: null,
            Justification: null,
            Status: ProblemRequestStatuses.Accepted,
            Assignee: "triage.owner",
            DecisionReason: "Pattern confirmed and accepted for problem review.",
            Comment: null,
            UpdatedBy: null));

        Assert.That(accepted.Status, Is.EqualTo(ProblemRequestStatuses.Accepted));
        Assert.That(accepted.Assignee, Is.EqualTo("triage.owner"));
        Assert.That(accepted.DecisionReason, Does.Contain("accepted"));
    }

    [Test]
    public async Task UpdateAsync_rejects_direct_transition_to_converted_linked()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);
        var created = await service.CreateAsync(BuildCreateCommand());

        var ex = Assert.ThrowsAsync<ProblemRequestValidationException>(async () =>
            await service.UpdateAsync(created.Id, new UpdateProblemRequestCommand(
                RequesterName: null,
                RequesterEmail: null,
                RequesterTeam: null,
                CompanyName: null,
                ItsmSource: null,
                Title: null,
                Description: null,
                Justification: null,
                Status: ProblemRequestStatuses.ConvertedLinked,
                Assignee: null,
                DecisionReason: null,
                Comment: null,
                UpdatedBy: null)));

        Assert.That(ex!.FieldName, Is.EqualTo("status"));
    }

    [Test]
    public async Task LinkAsync_sets_problem_reference_status_and_audit_comment()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);
        var created = await service.CreateAsync(BuildCreateCommand());

        var linked = await service.LinkAsync(created.Id, new LinkProblemRequestCommand(
            ProblemReference: "PRB000123",
            ProblemItsmSource: "servicenow-prod",
            ProblemCompanyName: "Contoso",
            ProblemTicketKey: "PRB000123",
            Comment: "Raised in ServiceNow after triage approval.",
            LinkedBy: "problem.coordinator"));

        Assert.That(linked.Status, Is.EqualTo(ProblemRequestStatuses.ConvertedLinked));
        Assert.That(linked.ProblemReference, Is.EqualTo("PRB000123"));
        Assert.That(linked.LinkedAt, Is.Not.Null);
        Assert.That(linked.Comments, Has.Count.EqualTo(1));
        Assert.That(linked.Comments.Single().CommentText, Does.Contain("Linked to problem reference 'PRB000123'"));
    }

    [Test]
    public async Task LinkAsync_rejected_request_must_be_reopened_first()
    {
        await using var context = CreateContext();
        var service = new ProblemRequestService(context);
        var created = await service.CreateAsync(BuildCreateCommand());

        await service.UpdateAsync(created.Id, new UpdateProblemRequestCommand(
            RequesterName: null,
            RequesterEmail: null,
            RequesterTeam: null,
            CompanyName: null,
            ItsmSource: null,
            Title: null,
            Description: null,
            Justification: null,
            Status: ProblemRequestStatuses.Rejected,
            Assignee: null,
            DecisionReason: "Insufficient evidence.",
            Comment: null,
            UpdatedBy: null));

        var ex = Assert.ThrowsAsync<ProblemRequestValidationException>(async () =>
            await service.LinkAsync(created.Id, new LinkProblemRequestCommand(
                ProblemReference: "PRB000999",
                ProblemItsmSource: null,
                ProblemCompanyName: null,
                ProblemTicketKey: null,
                Comment: null,
                LinkedBy: null)));

        Assert.That(ex!.FieldName, Is.EqualTo("status"));
    }
}
