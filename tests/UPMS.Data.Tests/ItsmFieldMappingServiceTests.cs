namespace UPMS.Data.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Tests for the <see cref="ItsmFieldMappingService"/> canonical field name lookup feature.
/// All tests use the shared SQLite test database initialised by <see cref="TicketDataServiceTestBase"/>.
/// </summary>
[TestFixture]
public class ItsmFieldMappingServiceTests : TicketDataServiceTestBase
{
    // -------------------------------------------------------------------------
    // GetCanonicalName
    // -------------------------------------------------------------------------

    [Test]
    public void GetCanonicalName_WhenMappingExistsForServiceNow_ReturnsCanonicalName()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        string canonical = service.GetCanonicalName(
            TestData.Itsm.ServiceNow,
            TestData.SourceFields.ServiceNowIncidentState);

        // Assert
        Assert.That(canonical, Is.EqualTo(TestData.CanonicalFields.Status));
    }

    [Test]
    public void GetCanonicalName_WhenMappingExistsForJira_ReturnsCanonicalName()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        string canonical = service.GetCanonicalName(
            TestData.Itsm.Jira,
            TestData.SourceFields.JiraStatus);

        // Assert
        Assert.That(canonical, Is.EqualTo(TestData.CanonicalFields.Status));
    }

    [Test]
    public void GetCanonicalName_WhenNoMappingExists_ReturnsFallbackSourceFieldName()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        string canonical = service.GetCanonicalName(
            TestData.Itsm.ServiceNow,
            TestData.SourceFields.UnknownField);

        // Assert — graceful fallback: source field name returned unchanged
        Assert.That(canonical, Is.EqualTo(TestData.SourceFields.UnknownField));
    }

    [Test]
    public void GetCanonicalName_IsCaseSensitiveForSourceFieldName()
    {
        // Arrange — seeded mapping uses lowercase "incident_state"; lookup with wrong case should fall back
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();
        string wrongCaseFieldName = "Incident_State";

        // Act
        string canonical = service.GetCanonicalName(TestData.Itsm.ServiceNow, wrongCaseFieldName);

        // Assert — no mapping found for wrong-case name; fallback returns input unchanged
        Assert.That(canonical, Is.EqualTo(wrongCaseFieldName));
    }

    [Test]
    public void GetCanonicalName_WithNullItsmSource_ThrowsArgumentException()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.GetCanonicalName(null!, TestData.SourceFields.ServiceNowIncidentState));
    }

    [Test]
    public void GetCanonicalName_WithEmptyItsmSource_ThrowsArgumentException()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.GetCanonicalName(string.Empty, TestData.SourceFields.ServiceNowIncidentState));
    }

    [Test]
    public void GetCanonicalName_WithNullSourceFieldName_ThrowsArgumentException()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.GetCanonicalName(TestData.Itsm.ServiceNow, null!));
    }

    [Test]
    public void GetCanonicalName_WithEmptySourceFieldName_ThrowsArgumentException()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.GetCanonicalName(TestData.Itsm.ServiceNow, string.Empty));
    }

    // -------------------------------------------------------------------------
    // GetMappingsForSource
    // -------------------------------------------------------------------------

    [Test]
    public void GetMappingsForSource_ReturnsAllMappingsForItsmSource()
    {
        // Arrange — seeded: incident_state, assigned_to, short_description for servicenow
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        IEnumerable<ItsmFieldMapping> mappings = service.GetMappingsForSource(TestData.Itsm.ServiceNow);

        // Assert
        List<ItsmFieldMapping> list = mappings.ToList();
        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list.Select(m => m.SourceFieldName), Is.EquivalentTo(new[]
        {
            TestData.SourceFields.ServiceNowIncidentState,
            TestData.SourceFields.ServiceNowAssignedTo,
            TestData.SourceFields.ServiceNowShortDescription
        }));
    }

    [Test]
    public void GetMappingsForSource_ReturnsEmptyCollectionForUnknownSource()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        IEnumerable<ItsmFieldMapping> mappings = service.GetMappingsForSource("unknown_itsm_source");

        // Assert
        Assert.That(mappings, Is.Empty);
    }

    [Test]
    public void GetMappingsForSource_ReturnsMappingsInAlphabeticalOrderBySourceFieldName()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act
        IEnumerable<ItsmFieldMapping> mappings = service.GetMappingsForSource(TestData.Itsm.ServiceNow);

        // Assert — ORDER BY source_field_name: assigned_to, incident_state, short_description
        List<string> sourceFieldNames = mappings.Select(m => m.SourceFieldName).ToList();
        Assert.That(sourceFieldNames, Is.EqualTo(sourceFieldNames.OrderBy(n => n).ToList()));
    }

    // -------------------------------------------------------------------------
    // UpsertMappingAsync
    // -------------------------------------------------------------------------

    [Test]
    public async Task UpsertMappingAsync_InsertsNewMappingRetrievableViaGetCanonicalName()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();
        string itsmSource = "test_itsm_insert";
        string sourceField = "new_field";
        string canonicalField = "NewCanonical";

        // Act
        await service.UpsertMappingAsync(itsmSource, sourceField, canonicalField);

        // Assert
        string retrieved = service.GetCanonicalName(itsmSource, sourceField);
        Assert.That(retrieved, Is.EqualTo(canonicalField));
    }

    [Test]
    public async Task UpsertMappingAsync_UpdatesExistingMappingWhenCalledWithSameSourceAndField()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();
        string itsmSource = "test_itsm_update";
        string sourceField = "updatable_field";

        await service.UpsertMappingAsync(itsmSource, sourceField, "OriginalCanonical");

        // Act — update the canonical name
        await service.UpsertMappingAsync(itsmSource, sourceField, "UpdatedCanonical");

        // Assert
        string retrieved = service.GetCanonicalName(itsmSource, sourceField);
        Assert.That(retrieved, Is.EqualTo("UpdatedCanonical"));
    }

    [Test]
    public void UpsertMappingAsync_WithNullOrEmptyInputs_ThrowsArgumentException()
    {
        // Arrange
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        // Act & Assert — null itsmSource
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertMappingAsync(null!, "field", "Canonical"));

        // Act & Assert — empty sourceFieldName
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertMappingAsync(TestData.Itsm.ServiceNow, string.Empty, "Canonical"));

        // Act & Assert — whitespace canonicalFieldName
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertMappingAsync(TestData.Itsm.ServiceNow, "field", "   "));
    }

    // -------------------------------------------------------------------------
    // Cross-source / multi-mapping scenarios
    // -------------------------------------------------------------------------

    [Test]
    public void GetCanonicalName_DifferentSourcesSameSourceFieldName_BothMapToSameCanonical()
    {
        // Both servicenow's "incident_state" and jira's "status" map to canonical "Status",
        // demonstrating that different sources can independently map to the same canonical name.
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        string serviceNowCanonical = service.GetCanonicalName(
            TestData.Itsm.ServiceNow,
            TestData.SourceFields.ServiceNowIncidentState);

        string jiraCanonical = service.GetCanonicalName(
            TestData.Itsm.Jira,
            TestData.SourceFields.JiraStatus);

        Assert.That(serviceNowCanonical, Is.EqualTo(TestData.CanonicalFields.Status));
        Assert.That(jiraCanonical, Is.EqualTo(TestData.CanonicalFields.Status));
    }

    [Test]
    public void GetMappingsForSource_SingleSourceHasMultipleDistinctMappings()
    {
        // ServiceNow has three seeded mappings (incident_state, assigned_to, short_description)
        IItsmFieldMappingService service = TestDatabaseFixture.GetMappingService();

        IEnumerable<ItsmFieldMapping> mappings = service.GetMappingsForSource(TestData.Itsm.ServiceNow);
        List<ItsmFieldMapping> list = mappings.ToList();

        // All belong to the same source
        Assert.That(list.Select(m => m.ItsmSource), Is.All.EqualTo(TestData.Itsm.ServiceNow));

        // All canonical names are distinct within those three mappings
        List<string> canonicals = list.Select(m => m.CanonicalFieldName).Distinct().ToList();
        Assert.That(canonicals, Has.Count.EqualTo(3));
    }
}
