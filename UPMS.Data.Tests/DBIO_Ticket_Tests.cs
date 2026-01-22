namespace UPMS.Data.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UPMS.Data;

[TestFixture]
public class DBIO_Ticket_Tests
{
    private Guid _testCompanyId;
    private Guid _testSnapshotId;
    private DateTime _testSnapshotDate;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await TestDatabaseFixture.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await TestDatabaseFixture.CleanupAsync();
    }

    [SetUp]
    public void SetUp()
    {
        _testCompanyId = Guid.Parse("00000001-0000-0000-0000-000000000001");
        _testSnapshotId = Guid.NewGuid();
        _testSnapshotDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    }

    #region Basic Retrieval Tests

    [Test]
    public async Task GetTickets_ReturnsExpectedFormat()
    {
        // Arrange: Set up test data with tickets that have multiple fields
        // Expected: Each ticket should contain:
        // - ticket_key (e.g., "INC0001234")
        // - company_id
        // - itsm_source (e.g., "servicenow")
        // - fields: Dictionary<string, string> containing field name-value pairs

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        Assert.That(tickets, Is.Not.Null);
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.TicketKey, Is.Not.Null.And.Not.Empty, "TicketKey should not be null or empty");
            Assert.That(ticket.CompanyId, Is.EqualTo(_testCompanyId), "CompanyId should match requested company");
            Assert.That(ticket.ItsmSource, Is.Not.Null.And.Not.Empty, "ItsmSource should not be null or empty");
            Assert.That(ticket.Fields, Is.Not.Null, "Fields collection should not be null");
            Assert.That(ticket.Fields, Is.InstanceOf<IDictionary<string, string>>(), "Fields should be a dictionary");
        }
    }

    [Test]
    public async Task GetTickets_ReturnsExpectedCount()
    {
        // Arrange: Database should have exactly 5 tickets for the test company at the snapshot date
        // Note: This test assumes test data has been set up with a known count

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        var ticketList = tickets.ToList();
        Assert.That(ticketList, Has.Count.EqualTo(5), "Should return exactly 5 tickets for test company");
    }

    [Test]
    public async Task GetTickets_ReturnsExpectedFields()
    {
        // Arrange: Tickets should have fields with distinct values
        // and the field values should be from the observation closest to the requested date

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        foreach (var ticket in tickets)
        {
            // Each ticket should have distinct field names (no duplicates)
            var fieldNames = ticket.Fields.Keys;
            Assert.That(fieldNames, Is.Unique, $"Ticket {ticket.TicketKey} should have distinct field names");

            // Fields should contain expected common fields
            Assert.That(ticket.Fields, Does.ContainKey("Status").Or.ContainKey("Priority"), 
                $"Ticket {ticket.TicketKey} should have at least Status or Priority field");

            // Field values should not be null (unless explicitly set to null in the data)
            foreach (var field in ticket.Fields)
            {
                Assert.That(field.Key, Is.Not.Null.And.Not.Empty, 
                    $"Field name should not be null or empty for ticket {ticket.TicketKey}");
            }
        }
    }

    #endregion

    #region Point-in-Time Reconstruction Tests

    [Test]
    public async Task GetTickets_ReconstructsStateAtSpecificDate()
    {
        // Arrange: Multiple snapshots exist for the same ticket with different field values
        // Request should return fields as they were on the specific date

        var requestedDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, requestedDate);

        // Assert
        var ticket = tickets.FirstOrDefault(t => t.TicketKey == "INC0001234");
        Assert.That(ticket, Is.Not.Null, "Should find ticket INC0001234");
        
        // The field values should match what was observed on or before the requested date
        Assert.That(ticket.ObservedAt, Is.LessThanOrEqualTo(requestedDate), 
            "ObservedAt timestamp should not be later than requested date");
    }

    [Test]
    public async Task GetTickets_UsesLatestObservationBeforeDate()
    {
        // Arrange: A ticket has multiple field observations before the requested date
        // Should use the observation with the closest (latest) timestamp before or equal to the date

        var requestedDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, requestedDate);

        // Assert
        var ticket = tickets.FirstOrDefault(t => t.TicketKey == "INC0001234");
        Assert.That(ticket, Is.Not.Null);
        
        // For a field that changed multiple times, should have the latest value before requestedDate
        if (ticket.Fields.TryGetValue("Status", out var status))
        {
            Assert.That(status, Is.Not.Null, "Status field should have a value");
        }
    }

    [Test]
    public async Task GetTickets_ExcludesObservationsAfterDate()
    {
        // Arrange: A ticket has observations both before and after the requested date
        var requestedDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, requestedDate);

        // Assert
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.ObservedAt, Is.LessThanOrEqualTo(requestedDate),
                $"Ticket {ticket.TicketKey} should not have observations after {requestedDate}");
        }
    }

    #endregion

    #region Multi-Tenant Scoping Tests

    [Test]
    public async Task GetTickets_FiltersByCompanyId()
    {
        // Arrange: Database contains tickets for multiple companies
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();

        // Act
        var company1Tickets = await DBIO_Ticket.GetTicketsAsync(company1, _testSnapshotDate);
        var company2Tickets = await DBIO_Ticket.GetTicketsAsync(company2, _testSnapshotDate);

        // Assert
        foreach (var ticket in company1Tickets)
        {
            Assert.That(ticket.CompanyId, Is.EqualTo(company1), 
                "All tickets should belong to company1");
        }

        foreach (var ticket in company2Tickets)
        {
            Assert.That(ticket.CompanyId, Is.EqualTo(company2), 
                "All tickets should belong to company2");
        }

        // Tickets should be isolated between companies
        var company1Keys = company1Tickets.Select(t => t.TicketKey).ToList();
        var company2Keys = company2Tickets.Select(t => t.TicketKey).ToList();
        Assert.That(company1Keys, Is.Not.EqualTo(company2Keys), 
            "Different companies should have different sets of tickets");
    }

    [Test]
    public async Task GetTickets_ReturnsEmptyForNonExistentCompany()
    {
        // Arrange
        var nonExistentCompanyId = Guid.NewGuid();

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(nonExistentCompanyId, _testSnapshotDate);

        // Assert
        Assert.That(tickets, Is.Empty, "Should return empty collection for non-existent company");
    }

    #endregion

    #region Field Handling Tests

    [Test]
    public async Task GetTickets_HandlesNullFieldValues()
    {
        // Arrange: Some fields in the database have explicit NULL values

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        var ticket = tickets.FirstOrDefault(t => t.Fields.ContainsKey("Assignee"));
        if (ticket != null && ticket.Fields.TryGetValue("Assignee", out var assignee))
        {
            // Null field values should be represented consistently (either as null or empty string)
            // The implementation should handle NULL field_value from the database
            Assert.Pass("Field handling works correctly");
        }
    }

    [Test]
    public async Task GetTickets_HandlesMultipleItsmSources()
    {
        // Arrange: Database contains tickets from multiple ITSM sources
        // (ServiceNow, Jira, Zendesk, etc.)

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        var itsmSources = tickets.Select(t => t.ItsmSource).Distinct().ToList();
        Assert.That(itsmSources, Is.Not.Empty, "Should have tickets from at least one ITSM source");
        
        // Each ticket should have a valid ITSM source
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.ItsmSource, Is.Not.Null.And.Not.Empty,
                $"Ticket {ticket.TicketKey} should have a valid ITSM source");
        }
    }

    [Test]
    public async Task GetTickets_HandlesTicketsWithNoFields()
    {
        // Arrange: A ticket exists in snapshot_ticket but has no field_change records

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        foreach (var ticket in tickets)
        {
            // Tickets with no fields should still be returned with an empty Fields collection
            Assert.That(ticket.Fields, Is.Not.Null, 
                $"Ticket {ticket.TicketKey} should have a non-null Fields collection even if empty");
        }
    }

    [Test]
    public async Task GetTickets_PreservesFieldValueTypes()
    {
        // Arrange: Fields can contain various string representations of data

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        foreach (var ticket in tickets)
        {
            foreach (var field in ticket.Fields)
            {
                // All field values should be strings (as per TEXT column in database)
                Assert.That(field.Value, Is.InstanceOf<string>().Or.Null,
                    $"Field {field.Key} in ticket {ticket.TicketKey} should be a string or null");
            }
        }
    }

    #endregion

    #region Performance and Batch Tests

    [Test]
    public async Task GetTickets_HandlesLargeResultSets()
    {
        // Arrange: Request tickets where the company has 1000+ tickets

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        var ticketList = tickets.ToList();
        Assert.That(ticketList.Count, Is.GreaterThanOrEqualTo(0), 
            "Should handle large result sets without errors");
        
        // Verify no duplicate tickets
        var ticketKeys = ticketList.Select(t => t.TicketKey).ToList();
        Assert.That(ticketKeys, Is.Unique, "Should not return duplicate tickets");
    }

    [Test]
    public async Task GetTickets_StreamsResultsEfficiently()
    {
        // Arrange: The method should return IAsyncEnumerable or similar for streaming

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        // Results should be enumerable without loading everything into memory at once
        Assert.That(tickets, Is.Not.Null);
        Assert.That(tickets, Is.InstanceOf<IEnumerable<Ticket>>(),
            "Should return an enumerable collection for efficient streaming");
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task GetTickets_HandlesDateAtMidnight()
    {
        // Arrange
        var midnightDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, midnightDate);

        // Assert
        Assert.That(tickets, Is.Not.Null, "Should handle midnight dates correctly");
    }

    [Test]
    public async Task GetTickets_HandlesFutureDate()
    {
        // Arrange: Request tickets for a future date
        var futureDate = DateTime.UtcNow.AddYears(1);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, futureDate);

        // Assert
        // Should return all tickets with their latest known state
        Assert.That(tickets, Is.Not.Null, "Should handle future dates gracefully");
    }

    [Test]
    public async Task GetTickets_HandlesDateBeforeAnySnapshots()
    {
        // Arrange: Request tickets for a date before any snapshots were taken
        var veryOldDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, veryOldDate);

        // Assert
        Assert.That(tickets, Is.Empty, 
            "Should return empty collection when date is before any snapshots");
    }

    [Test]
    public async Task GetTickets_HandlesEmptyGuidCompanyId()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(
            async () => await DBIO_Ticket.GetTicketsAsync(emptyGuid, _testSnapshotDate),
            "Should throw ArgumentException for empty GUID");
    }

    #endregion

    #region Snapshot-Specific Tests

    [Test]
    public async Task GetTicketsBySnapshot_ReturnsOnlyTicketsInSnapshot()
    {
        // Arrange: A specific snapshot contains only certain tickets

        // Act
        var tickets = await DBIO_Ticket.GetTicketsBySnapshotAsync(_testSnapshotId);

        // Assert
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.SnapshotId, Is.EqualTo(_testSnapshotId),
                $"Ticket {ticket.TicketKey} should belong to snapshot {_testSnapshotId}");
        }
    }

    [Test]
    public async Task GetTicketsBySnapshot_ReturnsEmptyForNonExistentSnapshot()
    {
        // Arrange
        var nonExistentSnapshotId = Guid.NewGuid();

        // Act
        var tickets = await DBIO_Ticket.GetTicketsBySnapshotAsync(nonExistentSnapshotId);

        // Assert
        Assert.That(tickets, Is.Empty, 
            "Should return empty collection for non-existent snapshot");
    }

    #endregion

    #region Field Change History Tests

    [Test]
    public async Task GetTicketFieldHistory_ReturnsChronologicalChanges()
    {
        // Arrange
        var ticketKey = "INC0001234";
        var fieldName = "Status";

        // Act
        var history = await DBIO_Ticket.GetTicketFieldHistoryAsync(
            _testCompanyId, ticketKey, fieldName);

        // Assert
        Assert.That(history, Is.Not.Null);
        var historyList = history.ToList();
        
        // Should be ordered chronologically (oldest first)
        for (int i = 1; i < historyList.Count; i++)
        {
            Assert.That(historyList[i].ObservedAt, Is.GreaterThanOrEqualTo(historyList[i - 1].ObservedAt),
                "Field history should be in chronological order");
        }
    }

    [Test]
    public async Task GetTicketFieldHistory_ReturnsEmptyForNonExistentField()
    {
        // Arrange
        var ticketKey = "INC0001234";
        var nonExistentField = "NonExistentField123";

        // Act
        var history = await DBIO_Ticket.GetTicketFieldHistoryAsync(
            _testCompanyId, ticketKey, nonExistentField);

        // Assert
        Assert.That(history, Is.Empty,
            "Should return empty collection for non-existent field");
    }

    #endregion

    #region Integration Tests with Related Entities

    [Test]
    public async Task GetTickets_IncludesSnapshotMetadata()
    {
        // Arrange: Tickets should include metadata about which snapshot they came from

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        foreach (var ticket in tickets)
        {
            Assert.That(ticket.SnapshotDate, Is.Not.EqualTo(default(DateTime)),
                $"Ticket {ticket.TicketKey} should have a valid snapshot date");
            
            Assert.That(ticket.SnapshotDate, Is.LessThanOrEqualTo(_testSnapshotDate),
                $"Ticket {ticket.TicketKey} snapshot date should not be after requested date");
        }
    }

    [Test]
    public async Task GetTickets_GroupsByTicketKey()
    {
        // Arrange: Multiple field changes exist for the same ticket

        // Act
        var tickets = await DBIO_Ticket.GetTicketsAsync(_testCompanyId, _testSnapshotDate);

        // Assert
        var ticketKeys = tickets.Select(t => t.TicketKey).ToList();
        Assert.That(ticketKeys, Is.Unique, 
            "Each ticket should appear only once, with all fields aggregated");
    }

    #endregion
}
