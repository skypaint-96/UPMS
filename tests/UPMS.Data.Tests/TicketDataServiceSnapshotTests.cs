namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for snapshot-specific operations and snapshot creation.
/// </summary>
[TestFixture]
public class TicketDataServiceSnapshotTests : TicketDataServiceTestBase
{
    [Test]
    public async Task GetTicketsBySnapshot_ReturnsOnlyTicketsInSnapshot()
    {
        // Arrange
        Guid snapshotId = TestSnapshotId;

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);

        // Assert
        foreach (Ticket ticket in tickets)
        {
            Assert.That(ticket.SnapshotId, Is.EqualTo(snapshotId));
            Assert.That(ticket.ItsmSource, Is.EqualTo(TestData.Itsm.ServiceNow));
        }
    }

    [Test]
    public async Task GetTicketsBySnapshot_ReturnsEmptyForNonExistentSnapshot()
    {
        // Arrange
        Guid nonExistentSnapshotId = Guid.NewGuid();

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(nonExistentSnapshotId);

        // Assert
        Assert.That(tickets, Is.Empty);
    }

    [Test]
    public async Task CreateSnapshot_CreatesSnapshotWithMetadata()
    {
        // Arrange
        DateTime snapshotDate = DateTime.UtcNow;
        string itsmSource = TestData.Itsm.ServiceNow;

        // Act
        Guid snapshotId = await DataService.CreateSnapshotAsync(
            itsmSource,
            snapshotDate
        );

        // Assert
        Assert.That(snapshotId, Is.Not.EqualTo(Guid.Empty));

        // Verify snapshot can be retrieved (even without tickets)
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets, Is.Not.Null);
    }

    [Test]
    public void CreateSnapshot_WithNullItsmSource_ThrowsArgumentException()
    {
        // Arrange
        DateTime snapshotDate = DateTime.UtcNow;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await DataService.CreateSnapshotAsync(null!, snapshotDate)
        );
    }

    [Test]
    public async Task AddTicketsToSnapshot_CreatesTicketRecords()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        var ticketKeys = new[] { "INC0009001", "INC0009002", "INC0009003" };
        var ticketsWithCompanies = ticketKeys.Select(key => (key, companyName)).ToArray();

        // Act
        await DataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

        // Assert
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        string[] retrievedKeys = tickets.Select(t => t.TicketKey).ToArray();

        Assert.That(retrievedKeys, Is.EquivalentTo(ticketKeys));
    }

    [Test]
    public void AddTicketsToSnapshot_WithInvalidSnapshotId_ThrowsArgumentException()
    {
        // Arrange
        string companyName = Company1Name;
        var ticketsWithCompanies = new[] { ("INC0009001", companyName) };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await DataService.AddTicketsToSnapshotAsync(Guid.Empty, ticketsWithCompanies)
        );
    }

    [Test]
    public async Task AddTicketsToSnapshot_WithDuplicateTicketKeys_HandlesGracefully()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);
        DateTime snapshotDate = DateTime.UtcNow;
        Guid snapshotId = await DataService.CreateSnapshotAsync(itsmSource, snapshotDate);

        var ticketsWithCompanies = new[]
        {
            ("INC0009001", companyName),
            ("INC0009001", companyName),
            ("INC0009002", companyName)
        };

        // Act
        await DataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

        // Assert - should only have 2 unique tickets
        IEnumerable<Ticket> tickets = await DataService.GetTicketsBySnapshotAsync(snapshotId);
        Assert.That(tickets.Count(), Is.EqualTo(2));
    }
}
