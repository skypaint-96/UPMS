namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for fetching tickets as of a point in time.
/// </summary>
[TestFixture]
public class TicketDataServicePointInTimeTests : TicketDataServiceTestBase
{
    [Test]
    public async Task GetTickets_AsOfDate_ReturnsSnapshotState()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime requestedDate = TestSnapshotDate;

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, requestedDate);

        // Assert
        Assert.That(tickets, Is.Not.Null);
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.SnapshotDate, Is.LessThanOrEqualTo(requestedDate));
        }
    }

    [Test]
    public async Task GetTickets_AsOfDate_NoSnapshots_ReturnsEmpty()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime requestedDate = new DateTime(2000, 1, 1);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, requestedDate);

        // Assert
        Assert.That(tickets, Is.Empty);
    }
}
