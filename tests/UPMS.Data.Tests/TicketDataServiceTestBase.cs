namespace UPMS.Data.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Base class for TicketDataService tests providing common setup and utilities.
/// </summary>
[TestFixture]
public abstract class TicketDataServiceTestBase
{
    protected string Company1Name { get; private set; } = string.Empty;
    protected string Company2Name { get; private set; } = string.Empty;
    protected string Company3Name { get; private set; } = string.Empty;
    protected Guid TestSnapshotId { get; private set; }
    protected DateTime TestSnapshotDate { get; private set; }

    protected TicketDataService DataService { get; private set; } = null!;

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
    public virtual void SetUp()
    {
        Company1Name = TestData.Companies.Company1;
        Company2Name = TestData.Companies.Company2;
        Company3Name = TestData.Companies.Company3;
        TestSnapshotId = Guid.Parse(TestDatabaseFixture.Company1NewSnapshotId);
        TestSnapshotDate = TestData.Dates.NewSnapshotDate;

        // Provide instance-based data service for tests
        DataService = TestDatabaseFixture.CreateTicketDataService();
    }

    /// <summary>
    /// Resolves ITSM source for a given company.
    /// </summary>
    protected static string ResolveItsmSource(string companyName)
    {
        return companyName switch
        {
            var name when name == TestData.Companies.Company1 => TestData.Itsm.ServiceNow,
            var name when name == TestData.Companies.Company2 => TestData.Itsm.Jira,
            var name when name == TestData.Companies.Company3 => TestData.Itsm.ServiceNow,
            _ => string.Empty
        };
    }
}
