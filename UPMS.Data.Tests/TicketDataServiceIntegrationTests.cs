namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Integration tests covering complex scenarios and workflows.
/// </summary>
[TestFixture]
public class TicketDataServiceIntegrationTests : TicketDataServiceTestBase
{
    [Test]
    public async Task UploadCompleteSnapshot_CreatesFullTicketHierarchy()
    {
        // Arrange - Simulates a complete ITSM export
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        var ticketData = new Dictionary<string, Dictionary<string, string?>>
        {
            ["INC0009020"] = new Dictionary<string, string?>
            {
                [TestData.Fields.Status] = TestData.FieldValues.StatusOpen,
                [TestData.Fields.Priority] = TestData.FieldValues.PriorityHigh,
                [TestData.Fields.Assignee] = "john.doe@example.com",
                [TestData.Fields.Description] = "Server down"
            },
            ["INC0009021"] = new Dictionary<string, string?>
            {
                [TestData.Fields.Status] = TestData.FieldValues.StatusResolved,
                [TestData.Fields.Priority] = TestData.FieldValues.PriorityLow,
                [TestData.Fields.Assignee] = "jane.smith@example.com",
                [TestData.Fields.Description] = "Password reset",
                [TestData.Fields.Resolution] = "Reset completed"
            }
        };

        // Act
        var ticketsWithCompanies = ticketData.Keys.Select(key => (key, companyName)).ToArray();
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

        foreach (var (ticketKey, fields) in ticketData)
        {
            foreach (var (fieldName, fieldValue) in fields)
            {
                await TicketDataService.RecordFieldChangeAsync(
                    companyName,
                    ticketKey,
                    fieldName,
                    fieldValue,
                    snapshotDate,
                    snapshotId
                );
            }
        }

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets.Count(), Is.EqualTo(2));

        Ticket inc20 = tickets.First(t => t.TicketKey == "INC0009020");
        Assert.That(inc20.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
        Assert.That(inc20.Fields[TestData.Fields.Priority], Is.EqualTo(TestData.FieldValues.PriorityHigh));
        Assert.That(inc20.Fields.Count, Is.EqualTo(4));

        Ticket inc21 = tickets.First(t => t.TicketKey == "INC0009021");
        Assert.That(inc21.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusResolved));
        Assert.That(inc21.Fields[TestData.Fields.Resolution], Is.EqualTo("Reset completed"));
        Assert.That(inc21.Fields.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task UploadMultipleSnapshots_TracksFieldChangesOverTime()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        string ticketKey = "INC0009030";

        // Snapshot 1 - ticket is Open
        var snapshot1Date = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot1Id = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshot1Date);
        await TicketDataService.AddTicketsToSnapshotAsync(snapshot1Id, new[] { (ticketKey, companyName) });
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusOpen, snapshot1Date, snapshot1Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot1Date, snapshot1Id);

        // Snapshot 2 - ticket is In Progress
        var snapshot2Date = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot2Id = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshot2Date);
        await TicketDataService.AddTicketsToSnapshotAsync(snapshot2Id, new[] { (ticketKey, companyName) });
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusInProgress, snapshot2Date, snapshot2Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot2Date, snapshot2Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Assignee, "tech@example.com", snapshot2Date, snapshot2Id);

        // Snapshot 3 - ticket is Resolved
        var snapshot3Date = new DateTime(2025, 1, 3, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot3Id = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshot3Date);
        await TicketDataService.AddTicketsToSnapshotAsync(snapshot3Id, new[] { (ticketKey, companyName) });
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusResolved, snapshot3Date, snapshot3Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot3Date, snapshot3Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Assignee, "tech@example.com", snapshot3Date, snapshot3Id);
        await TicketDataService.RecordFieldChangeAsync(companyName, ticketKey, TestData.Fields.Resolution, "Fixed", snapshot3Date, snapshot3Id);

        // Assert - Verify each snapshot shows correct state
        IEnumerable<Ticket> tickets1 = await TicketDataService.GetTicketsBySnapshotAsync(snapshot1Id);
        Ticket ticket1 = tickets1.First();
        Assert.That(ticket1.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
        Assert.That(ticket1.Fields.ContainsKey(TestData.Fields.Assignee), Is.False);

        IEnumerable<Ticket> tickets2 = await TicketDataService.GetTicketsBySnapshotAsync(snapshot2Id);
        Ticket ticket2 = tickets2.First();
        Assert.That(ticket2.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusInProgress));
        Assert.That(ticket2.Fields[TestData.Fields.Assignee], Is.EqualTo("tech@example.com"));

        IEnumerable<Ticket> tickets3 = await TicketDataService.GetTicketsBySnapshotAsync(snapshot3Id);
        Ticket ticket3 = tickets3.First();
        Assert.That(ticket3.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusResolved));
        Assert.That(ticket3.Fields[TestData.Fields.Resolution], Is.EqualTo("Fixed"));

        // Verify field history
        IEnumerable<FieldChange> history = await TicketDataService.GetTicketFieldHistoryAsync(companyName, ticketKey, TestData.Fields.Status);
        string[] historyValues = history.Select(h => h.FieldValue).ToArray();
        Assert.That(historyValues, Is.EqualTo(new[] { TestData.FieldValues.StatusOpen, TestData.FieldValues.StatusInProgress, TestData.FieldValues.StatusResolved }));
    }

    [Test]
    public async Task UploadSnapshot_WithLargeFieldValues_HandlesCorrectly()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        string ticketKey = "INC0009040";
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, companyName) });

        string largeDescription = new string('A', 5000);

        // Act
        await TicketDataService.RecordFieldChangeAsync(
            companyName,
            ticketKey,
            TestData.Fields.Description,
            largeDescription,
            snapshotDate,
            snapshotId
        );

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First();
        Assert.That(ticket.Fields[TestData.Fields.Description], Has.Length.EqualTo(5000));
    }

    [Test]
    public async Task UploadSnapshot_WithSpecialCharactersInFields_HandlesCorrectly()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        string ticketKey = "INC0009050";
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, companyName) });

        var specialCharsData = new Dictionary<string, string?>
        {
            ["Description"] = "Line1\nLine2\rLine3",
            ["Notes"] = "Contains 'quotes' and \"double quotes\"",
            ["Unicode"] = "Unicode: ??? ?? ???",
            ["Symbols"] = "Symbols: @#$%^&*()_+-=[]{}|;:,.<>?/",
            ["Json"] = "{\"key\": \"value\", \"array\": [1, 2, 3]}"
        };

        // Act
        foreach (var (fieldName, fieldValue) in specialCharsData)
        {
            await TicketDataService.RecordFieldChangeAsync(
                companyName,
                ticketKey,
                fieldName,
                fieldValue,
                snapshotDate,
                snapshotId
            );
        }

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First();

        foreach (var (fieldName, expectedValue) in specialCharsData)
        {
            Assert.That(ticket.Fields[fieldName], Is.EqualTo(expectedValue), $"Field {fieldName} value mismatch");
        }
    }

    [Test]
    public async Task UploadSnapshot_WithManyTickets_PerformsEfficiently()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        var ticketKeys = Enumerable.Range(1, 100).Select(i => ($"INC{i:D7}", companyName)).ToArray();

        // Act
        DateTime startTime = DateTime.UtcNow;

        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, ticketKeys);

        foreach (var (key, _) in ticketKeys)
        {
            await TicketDataService.RecordFieldChangeAsync(companyName, key, TestData.Fields.Status, TestData.FieldValues.StatusOpen, snapshotDate, snapshotId);
            await TicketDataService.RecordFieldChangeAsync(companyName, key, TestData.Fields.Priority, TestData.FieldValues.PriorityMedium, snapshotDate, snapshotId);
            await TicketDataService.RecordFieldChangeAsync(companyName, key, TestData.Fields.Assignee, "user@example.com", snapshotDate, snapshotId);
            await TicketDataService.RecordFieldChangeAsync(companyName, key, TestData.Fields.Description, "Test ticket", snapshotDate, snapshotId);
            await TicketDataService.RecordFieldChangeAsync(companyName, key, "CreatedAt", snapshotDate.ToString("O"), snapshotDate, snapshotId);
        }

        TimeSpan duration = DateTime.UtcNow - startTime;

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets.Count(), Is.EqualTo(100));

        Assert.That(duration.TotalSeconds, Is.LessThan(30),
            $"Upload of 100 tickets with 5 fields each took {duration.TotalSeconds} seconds");
    }

    [Test]
    public async Task UploadSnapshot_FromDifferentITSMSources_MaintainsSeparation()
    {
        // Arrange
        string companyName = Company1Name;
        DateTime snapshotDate = DateTime.UtcNow;

        Guid serviceNowSnapshotId = await TicketDataService.CreateSnapshotAsync(TestData.Itsm.ServiceNow, snapshotDate);
        Guid jiraSnapshotId = await TicketDataService.CreateSnapshotAsync(TestData.Itsm.Jira, snapshotDate.AddMinutes(1));

        // Add tickets with same key but from different sources
        await TicketDataService.AddTicketsToSnapshotAsync(serviceNowSnapshotId, new[] { ("TICKET-001", companyName) });
        await TicketDataService.RecordFieldChangeAsync(companyName, "TICKET-001", TestData.Fields.Status, "ServiceNow Open", snapshotDate, serviceNowSnapshotId);

        await TicketDataService.AddTicketsToSnapshotAsync(jiraSnapshotId, new[] { ("TICKET-001", companyName) });
        await TicketDataService.RecordFieldChangeAsync(companyName, "TICKET-001", TestData.Fields.Status, "Jira To Do", snapshotDate.AddMinutes(1), jiraSnapshotId);

        // Assert - Each snapshot maintains its own data
        IEnumerable<Ticket> serviceNowTickets = await TicketDataService.GetTicketsBySnapshotAsync(serviceNowSnapshotId);
        Ticket serviceNowTicket = serviceNowTickets.First();
        Assert.That(serviceNowTicket.ItsmSource, Is.EqualTo(TestData.Itsm.ServiceNow));
        Assert.That(serviceNowTicket.Fields[TestData.Fields.Status], Is.EqualTo("ServiceNow Open"));

        IEnumerable<Ticket> jiraTickets = await TicketDataService.GetTicketsBySnapshotAsync(jiraSnapshotId);
        Ticket jiraTicket = jiraTickets.First();
        Assert.That(jiraTicket.ItsmSource, Is.EqualTo(TestData.Itsm.Jira));
        Assert.That(jiraTicket.Fields[TestData.Fields.Status], Is.EqualTo("Jira To Do"));
    }
}
