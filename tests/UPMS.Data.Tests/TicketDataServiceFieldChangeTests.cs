namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for field change recording and field history retrieval.
/// </summary>
[TestFixture]
public class TicketDataServiceFieldChangeTests : TicketDataServiceTestBase
{
    [Test]
    public async Task RecordFieldChange_AddsFieldValueForTicket()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        string ticketKey = "INC0009010";
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, companyName) });

        // Act
        await TicketDataService.RecordFieldChangeAsync(
            companyName,
            ticketKey,
            TestData.Fields.Status,
            TestData.FieldValues.StatusOpen,
            snapshotDate,
            snapshotId
        );

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First(t => t.TicketKey == ticketKey);

        Assert.That(ticket.Fields, Does.ContainKey(TestData.Fields.Status));
        Assert.That(ticket.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
    }

    [Test]
    public async Task RecordFieldChange_WithNullValue_StoresNullValue()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        string ticketKey = "INC0009011";
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticketKey, companyName) });

        // Act
        await TicketDataService.RecordFieldChangeAsync(
            companyName,
            ticketKey,
            TestData.Fields.Resolution,
            null,
            snapshotDate,
            snapshotId
        );

        // Assert
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
        Ticket ticket = tickets.First(t => t.TicketKey == ticketKey);

        Assert.That(ticket.Fields, Does.ContainKey(TestData.Fields.Resolution));
        Assert.That(ticket.Fields[TestData.Fields.Resolution], Is.Null);
    }

    [Test]
    public async Task GetTicketFieldHistory_ReturnsChronologicalChanges()
    {
        // Arrange
        string companyName = Company1Name;

        // Act
        IEnumerable<FieldChange> history = await TicketDataService.GetTicketFieldHistoryAsync(
            companyName,
            TestData.TicketKeys.Ticket1,
            TestData.Fields.Status
        );
        List<FieldChange> changes = history.ToList();

        // Assert
        Assert.That(changes, Is.Not.Empty);
        for (int i = 1; i < changes.Count; i++)
        {
            Assert.That(changes[i].ObservedAt, Is.GreaterThanOrEqualTo(changes[i - 1].ObservedAt),
                $"Field changes should be in chronological order at index {i}");
        }
    }
}
