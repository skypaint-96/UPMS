namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Integration-style tests exercising multiple TicketDataService operations.
/// </summary>
[TestFixture]
public class TicketDataServiceIntegrationTests : TicketDataServiceTestBase
{
    [Test]
    public async Task CreateSnapshot_AddTickets_RecordFieldChanges_Workflow()
    {
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        var ticketKeys = new[] { TestData.TicketKeys.Ticket1, TestData.TicketKeys.Ticket2 };
        var ticketsWithCompanies = ticketKeys.Select(key => (key, companyName)).ToArray();
        await DataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

        foreach (var key in ticketsWithCompanies.Select(t => t.Item1))
        {
            await DataService.RecordFieldChangeAsync(
                companyName,
                key,
                TestData.Fields.Status,
                TestData.FieldValues.StatusOpen,
                snapshotDate,
                snapshotId);
        }

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets.Count(), Is.EqualTo(ticketsWithCompanies.Length));
    }

    [Test]
    public async Task SnapshotHistoryAcrossMultipleDates()
    {
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        var snapshot1Date = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot1Id = await DataService.CreateSnapshotAsync(itsmSource, snapshot1Date);
        await DataService.AddTicketsToSnapshotAsync(snapshot1Id, new[] { (TestData.TicketKeys.Ticket1, companyName) });
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Status, TestData.FieldValues.StatusOpen, snapshot1Date, snapshot1Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot1Date, snapshot1Id);

        var snapshot2Date = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot2Id = await DataService.CreateSnapshotAsync(itsmSource, snapshot2Date);
        await DataService.AddTicketsToSnapshotAsync(snapshot2Id, new[] { (TestData.TicketKeys.Ticket1, companyName) });
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Status, TestData.FieldValues.StatusInProgress, snapshot2Date, snapshot2Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot2Date, snapshot2Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Assignee, "tech@example.com", snapshot2Date, snapshot2Id);

        var snapshot3Date = new DateTime(2025, 1, 3, 12, 0, 0, DateTimeKind.Utc);
        Guid snapshot3Id = await DataService.CreateSnapshotAsync(itsmSource, snapshot3Date);
        await DataService.AddTicketsToSnapshotAsync(snapshot3Id, new[] { (TestData.TicketKeys.Ticket1, companyName) });
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Status, TestData.FieldValues.StatusResolved, snapshot3Date, snapshot3Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, snapshot3Date, snapshot3Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Assignee, "tech@example.com", snapshot3Date, snapshot3Id);
        await DataService.RecordFieldChangeAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Resolution, "Fixed", snapshot3Date, snapshot3Id);

        // Assert - Verify each snapshot shows correct state
        IEnumerable<Ticket> tickets1 = await DataService.GetTicketsBySnapshotAsync(snapshot1Id);
        Ticket ticket1 = tickets1.First();

        IEnumerable<Ticket> tickets2 = await DataService.GetTicketsBySnapshotAsync(snapshot2Id);
        Ticket ticket2 = tickets2.First();

        IEnumerable<Ticket> tickets3 = await DataService.GetTicketsBySnapshotAsync(snapshot3Id);
        Ticket ticket3 = tickets3.First();

        // Verify field history
        IEnumerable<FieldChange> history = await DataService.GetTicketFieldHistoryAsync(companyName, TestData.TicketKeys.Ticket1, TestData.Fields.Status);
        string?[] historyValues = history.Select(h => h.FieldValue).ToArray();
        Assert.That(historyValues.Length, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public async Task RecordFieldChange_And_RetrieveLatestState()
    {
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(ResolveItsmSource(Company1Name), snapshotDate);

        string ticketKey = "INC0009040";
        await DataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, Company1Name) });

        // Act
        await DataService.RecordFieldChangeAsync(
            Company1Name,
            ticketKey,
            TestData.Fields.Status,
            "Open",
            snapshotDate,
            snapshotId);

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First();
        Assert.That(ticket.Fields.ContainsKey(TestData.Fields.Status));
    }

    [Test]
    public async Task LargeSnapshot_HandlesManyTickets()
    {
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(ResolveItsmSource(Company1Name), snapshotDate);

        var ticketKeys = Enumerable.Range(1, 100).Select(i => $"TKT{1000 + i:D4}").ToArray();
        await DataService.AddTicketsToSnapshotAsync(snapshotId, ticketKeys.Select(k => (k, Company1Name)));

        // Act - record one field change per ticket
        foreach (var key in ticketKeys)
        {
            await DataService.RecordFieldChangeAsync(Company1Name, key, TestData.Fields.Status, TestData.FieldValues.StatusOpen, snapshotDate, snapshotId);
        }

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets.Count(), Is.EqualTo(100));
    }

    [Test]
    public async Task Snapshots_MaintainIsolationAcrossSources()
    {
        DateTime snapshotDate = DateTime.UtcNow;
        Guid serviceNowSnapshotId = await DataService.CreateSnapshotAsync(TestData.Itsm.ServiceNow, snapshotDate);
        Guid jiraSnapshotId = await DataService.CreateSnapshotAsync(TestData.Itsm.Jira, snapshotDate.AddMinutes(1));

        // Add tickets with same key but from different sources
        await DataService.AddTicketsToSnapshotAsync(serviceNowSnapshotId, new[] { ("TICKET-001", Company1Name) });
        await DataService.RecordFieldChangeAsync(Company1Name, "TICKET-001", TestData.Fields.Status, "ServiceNow Open", snapshotDate, serviceNowSnapshotId);

        await DataService.AddTicketsToSnapshotAsync(jiraSnapshotId, new[] { ("TICKET-001", Company1Name) });
        await DataService.RecordFieldChangeAsync(Company1Name, "TICKET-001", TestData.Fields.Status, "Jira To Do", snapshotDate.AddMinutes(1), jiraSnapshotId);

        // Assert - Each snapshot maintains its own data
        IEnumerable<Ticket> serviceNowTickets = await DataService.GetTicketsBySnapshotAsync(serviceNowSnapshotId);
        Ticket serviceNowTicket = serviceNowTickets.First();

        IEnumerable<Ticket> jiraTickets = await DataService.GetTicketsBySnapshotAsync(jiraSnapshotId);
        Ticket jiraTicket = jiraTickets.First();
    }
}
