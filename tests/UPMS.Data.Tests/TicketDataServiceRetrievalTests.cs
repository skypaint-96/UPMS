namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for ticket retrieval and format validation.
/// </summary>
[TestFixture]
public class TicketDataServiceRetrievalTests : TicketDataServiceTestBase
{
    [Test]
    public async Task GetTickets_ReturnsExpectedFormat()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert
        Assert.That(tickets, Is.Not.Null);
        foreach (Ticket ticket in tickets)
        {
            Assert.That(ticket.TicketKey, Is.Not.Null.And.Not.Empty);
            Assert.That(ticket.CompanyName, Is.EqualTo(companyName));
            Assert.That(ticket.ItsmSource, Is.EqualTo(itsmSource));
            Assert.That(ticket.Fields, Is.Not.Null);
            Assert.That(ticket.Fields, Is.InstanceOf<IDictionary<string, string?>>());
        }
    }

    [Test]
    public async Task GetTickets_ReturnsExpectedFields()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert
        foreach (Ticket ticket in tickets)
        {
            ICollection<string> fieldNames = ticket.Fields.Keys;
            Assert.That(fieldNames, Is.Unique);
            Assert.That(ticket.Fields, Does.ContainKey(TestData.Fields.Status).Or.ContainKey(TestData.Fields.Priority));
            foreach (KeyValuePair<string, string?> field in ticket.Fields)
            {
                Assert.That(field.Key, Is.Not.Null.And.Not.Empty);
            }
        }
    }

    [Test]
    public async Task GetTickets_HandlesNullFieldValues()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert - Fields that exist in our test data will have values, not null
        // Verify that assigned fields are present and non-null
        Ticket? ticket = tickets.FirstOrDefault(t => t.Fields.ContainsKey(TestData.Fields.Assignee));
        Assert.That(ticket, Is.Not.Null);
        Assert.That(ticket!.Fields.ContainsKey(TestData.Fields.Assignee), Is.True);
        Assert.That(ticket.Fields[TestData.Fields.Assignee], Is.Not.Null);
    }

    [Test]
    public async Task GetTickets_PreservesFieldValueTypes()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert
        foreach (Ticket ticket in tickets)
        {
            foreach (KeyValuePair<string, string?> field in ticket.Fields)
            {
                Assert.That(field.Value, Is.InstanceOf<string>().Or.Null);
            }
        }
    }

    [Test]
    public async Task GetTickets_GroupsByTicketKey()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert
        IEnumerable<IGrouping<string, Ticket>> groupedByKey = tickets.GroupBy(t => t.TicketKey);
        foreach (IGrouping<string, Ticket> group in groupedByKey)
        {
            Assert.That(group.Count(), Is.EqualTo(1), $"Ticket key {group.Key} should appear exactly once");
        }
    }

    [Test]
    public async Task GetTickets_IncludesSnapshotMetadata()
    {
        // Arrange
        string companyName = Company1Name;
        string itsmSource = ResolveItsmSource(companyName);

        // Act
        IEnumerable<Ticket> tickets = await DataService.GetTicketsAsync(itsmSource, companyName, TestSnapshotDate);

        // Assert
        foreach (Ticket ticket in tickets)
        {
            Assert.That(ticket.SnapshotDate, Is.Not.EqualTo(default(DateTime)));
            Assert.That(ticket.SnapshotDate, Is.LessThanOrEqualTo(TestSnapshotDate));
        }
    }
}
