namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for multi-tenant data isolation and scoping.
/// </summary>
[TestFixture]
public class TicketDataServiceMultiTenantTests : TicketDataServiceTestBase
{
    [Test]
    public async Task GetTickets_FiltersByOriginAndCompanyName()
    {
        // Arrange
        string company1 = Company1Name;
        string company2 = Company2Name;
        string company3 = Company3Name;

        // Act
        IEnumerable<Ticket> company1Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company1), company1, TestSnapshotDate);
        IEnumerable<Ticket> company2Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company2), company2, TestSnapshotDate);
        IEnumerable<Ticket> company3Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company3), company3, TestSnapshotDate);

        // Assert - Each company sees only its own ITSM source
        Assert.That(company1Tickets.Select(t => t.ItsmSource).Distinct(), Is.EquivalentTo(new[] { TestData.Itsm.ServiceNow }));
        Assert.That(company2Tickets.Select(t => t.ItsmSource).Distinct(), Is.EquivalentTo(new[] { TestData.Itsm.Jira }));
        Assert.That(company3Tickets.Select(t => t.ItsmSource).Distinct(), Is.EquivalentTo(new[] { TestData.Itsm.ServiceNow }));
    }

    [Test]
    public async Task GetTickets_MaintainsCompanyIsolation()
    {
        // Arrange
        string company1 = Company1Name;
        string company3 = Company3Name;

        // Act
        IEnumerable<Ticket> company1Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company1), company1, TestSnapshotDate);
        IEnumerable<Ticket> company3Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company3), company3, TestSnapshotDate);

        // Assert - Overlapping ticket keys remain isolated per company
        Assert.That(company1Tickets.Count(t => t.TicketKey == TestData.TicketKeys.Ticket1), Is.EqualTo(1));
        Assert.That(company3Tickets.Count(t => t.TicketKey == TestData.TicketKeys.Ticket1), Is.EqualTo(1));
        Assert.That(company1Tickets.First(t => t.TicketKey == TestData.TicketKeys.Ticket1).CompanyName, Is.EqualTo(company1));
        Assert.That(company3Tickets.First(t => t.TicketKey == TestData.TicketKeys.Ticket1).CompanyName, Is.EqualTo(company3));
    }

    [Test]
    public async Task GetTickets_PreventsCrossCompanyLeakage()
    {
        // Arrange
        string company1 = Company1Name;
        string company2 = Company2Name;

        // Act
        IEnumerable<Ticket> company1Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company1), company1, TestSnapshotDate);
        IEnumerable<Ticket> company2Tickets = await TicketDataService.GetTicketsAsync(ResolveItsmSource(company2), company2, TestSnapshotDate);

        // Assert - No cross-company leakage
        IEnumerable<string> intersection = company1Tickets.Select(t => t.TicketKey)
            .Intersect(company2Tickets.Select(t => t.TicketKey));
        Assert.That(intersection, Is.Empty);
    }
}
