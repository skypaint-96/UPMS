namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for point-in-time ticket state reconstruction.
/// </summary>
[TestFixture]
public class TicketDataServicePointInTimeTests : TicketDataServiceTestBase
{
    [Test]
    public async Task GetTickets_ReconstructsStateAtSpecificDate()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime requestedDate = TestData.Dates.OlderSnapshotDate;

        // Act
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsAsync(itsmSource, companyName, requestedDate);

        // Assert
        Ticket? ticket = tickets.FirstOrDefault(t => t.TicketKey == TestData.TicketKeys.Ticket1);
        Assert.That(ticket, Is.Not.Null);
        Assert.That(ticket!.ObservedAt, Is.LessThanOrEqualTo(requestedDate));
        Assert.That(ticket.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusOpen));
    }

    [Test]
    public async Task GetTickets_UsesLatestObservationBeforeDate()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime requestedDate = TestData.Dates.NewSnapshotDate;

        // Act
        IEnumerable<Ticket> tickets = await TicketDataService.GetTicketsAsync(itsmSource, companyName, requestedDate);

        // Assert
        Ticket? ticket = tickets.FirstOrDefault(t => t.TicketKey == TestData.TicketKeys.Ticket1);
        Assert.That(ticket, Is.Not.Null);
        Assert.That(ticket!.Fields[TestData.Fields.Status], Is.EqualTo(TestData.FieldValues.StatusInProgress));
    }
}
