namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for recording and retrieving field changes.
/// </summary>
[TestFixture]
public class TicketDataServiceFieldChangeTests : TicketDataServiceTestBase
{
    [Test]
    public async Task RecordFieldChange_AddsAndRetrievesFieldValue()
    {
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(ResolveItsmSource(Company1Name), snapshotDate);

        string ticketKey = "INC0009010";
        await DataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, Company1Name) });

        // Act
        await DataService.RecordFieldChangeAsync(
            Company1Name,
            ticketKey,
            TestData.Fields.Status,
            TestData.FieldValues.StatusOpen,
            snapshotDate,
            snapshotId);

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First(t => t.TicketKey == ticketKey);
        Assert.That(ticket.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
    }

    [Test]
    public async Task RecordFieldChange_PersistsMultipleChanges()
    {
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(ResolveItsmSource(Company1Name), snapshotDate);

        string ticketKey = "INC0009011";
        await DataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, Company1Name) });

        // Act
        await DataService.RecordFieldChangeAsync(
            Company1Name,
            ticketKey,
            TestData.Fields.Status,
            TestData.FieldValues.StatusOpen,
            snapshotDate,
            snapshotId);

        await DataService.RecordFieldChangeAsync(
            Company1Name,
            ticketKey,
            TestData.Fields.Priority,
            TestData.FieldValues.PriorityHigh,
            snapshotDate,
            snapshotId);

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First(t => t.TicketKey == ticketKey);
        Assert.That(ticket.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
        Assert.That(ticket.Fields[TestData.Fields.Priority], Is.EqualTo(TestData.FieldValues.PriorityHigh));
    }

    [Test]
    public async Task GetTicketFieldHistory_ReturnsOrderedHistory()
    {
        // Act
        IEnumerable<FieldChange> history = await DataService.GetTicketFieldHistoryAsync(
            Company1Name,
            TestData.TicketKeys.Ticket1,
            TestData.Fields.Status);

        // Assert
        Assert.That(history, Is.Not.Null);
        Assert.That(history, Is.Ordered.By("ObservedAt"));
    }
}
