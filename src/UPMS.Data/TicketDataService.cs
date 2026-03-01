namespace UPMS.Data;

using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Provides database I/O operations for ticket and field change data.
/// </summary>
public static class TicketDataService
{
    private static Func<IDbConnection>? _connectionFactory;

    /// <summary>
    /// Initializes the service with a connection string.
    /// </summary>
    public static void Initialize(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        _connectionFactory = () => new NpgsqlConnection(connectionString);
    }

    /// <summary>
    /// Initializes the service with database options.
    /// </summary>
    public static void Initialize(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Initialize(options.Value.ConnectionString);
    }

    /// <summary>
    /// Initializes the service with a custom connection factory.
    /// Used by the test suite to inject alternate connections; not for production use.
    /// </summary>
    public static void Initialize(Func<IDbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    private static IDbConnection GetConnection()
    {
        return _connectionFactory == null
            ? throw new InvalidOperationException(
                "TicketDataService has not been initialized. Call Initialize() first.")
            : _connectionFactory();
    }

    private static DateTime ParseDateTime(string? dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr))
        {
            return DateTime.MinValue;
        }

        // Try to parse as ISO 8601 format
        if (DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime result))
        {
            return result;
        }

        // Try standard DateTime parsing
        return DateTime.TryParse(dateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime result2)
            ? result2
            : DateTime.MinValue;
    }

    /// <summary>
    /// Creates a new snapshot for an ITSM source.
    /// </summary>
    public static async Task<Guid> CreateSnapshotAsync(
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy = "system",
        string? uploadMetadata = null)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
        {
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        }

        if (snapshotDate == default)
        {
            throw new ArgumentException("Snapshot date is required.", nameof(snapshotDate));
        }

        Guid snapshotId = Guid.NewGuid();

        const string sql = """
            INSERT INTO raw_snapshot (id, itsm_source, snapshot_date, uploaded_by, uploaded_at, upload_metadata)
            VALUES (@Id, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt, @UploadMetadata)
            """;

        using (IDbConnection connection = GetConnection())
        {
            int unused = await connection.ExecuteAsync(
                sql,
                new
                {
                    Id = snapshotId,
                    ItsmSource = itsmSource,
                    SnapshotDate = snapshotDate,
                    UploadedBy = uploadedBy,
                    UploadedAt = DateTime.UtcNow,
                    UploadMetadata = uploadMetadata
                }
            );
        }

        return snapshotId;
    }

    /// <summary>
    /// Adds tickets to a snapshot with their associated company names.
    /// </summary>
    public static async Task AddTicketsToSnapshotAsync(
        Guid snapshotId,
        IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
    {
        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));
        }

        ArgumentNullException.ThrowIfNull(ticketsWithCompanies);

        List<(string, string)> validTickets = ticketsWithCompanies
            .Where(t => !string.IsNullOrWhiteSpace(t.TicketKey) && !string.IsNullOrWhiteSpace(t.CompanyName))
            .Select(t => (t.TicketKey.Trim(), t.CompanyName.Trim()))
            .Distinct()
            .ToList();

        if (validTickets.Count == 0)
        {
            return;
        }

        using IDbConnection connection = GetConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        string insertSql = BuildSnapshotTicketInsertSql(connection);
        using IDbTransaction transaction = connection.BeginTransaction();

        foreach ((string ticketKey, string companyName) in validTickets)
        {
            int unused = await connection.ExecuteAsync(
                insertSql,
                new
                {
                    Id = Guid.NewGuid(),
                    SnapshotId = snapshotId,
                    CompanyName = companyName,
                    TicketKey = ticketKey
                },
                transaction
            );
        }

        transaction.Commit();
    }

    /// <summary>
    /// Records a field change for a ticket.
    /// </summary>
    public static async Task RecordFieldChangeAsync(
        string companyName,
        string ticketKey,
        string fieldName,
        string? fieldValue,
        DateTime observedAt,
        Guid snapshotId)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("Company name is required.", nameof(companyName));
        }

        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));
        }

        if (string.IsNullOrWhiteSpace(ticketKey))
        {
            throw new ArgumentException("Ticket key is required.", nameof(ticketKey));
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name is required.", nameof(fieldName));
        }

        if (observedAt == default)
        {
            throw new ArgumentException("Observed time is required.", nameof(observedAt));
        }

        const string sql = """
            INSERT INTO field_change (company_name, ticket_key, field_name, field_value, observed_at, snapshot_id)
            VALUES (@CompanyName, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
            """;

        using IDbConnection connection = GetConnection();
        int unused = await connection.ExecuteAsync(
            sql,
            new
            {
                CompanyName = companyName,
                TicketKey = ticketKey,
                FieldName = fieldName,
                FieldValue = fieldValue,
                ObservedAt = observedAt,
                SnapshotId = snapshotId
            }
        );
    }

    /// <summary>
    /// Retrieves tickets for a company from a specific ITSM source as they appeared at a specific point in time.
    /// Reconstructs ticket state by finding the latest field values observed on or before the specified date.
    /// </summary>
    public static async Task<IEnumerable<Ticket>> GetTicketsAsync(string itsmSource, string companyName, DateTime asOfDate)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
        {
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        }

        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("Company name is required.", nameof(companyName));
        }

        const string sql = """
            WITH all_snapshots AS (
                SELECT id, snapshot_date, itsm_source
                FROM raw_snapshot
                WHERE itsm_source = @ItsmSource
                    AND snapshot_date <= @AsOfDate
            ),
            latest_snapshot_per_ticket AS (
                SELECT 
                    st.ticket_key,
                    st.snapshot_id,
                    rs.snapshot_date,
                    rs.itsm_source,
                    ROW_NUMBER() OVER (
                        PARTITION BY st.ticket_key
                        ORDER BY rs.snapshot_date DESC
                    ) as rn
                FROM snapshot_ticket st
                JOIN all_snapshots rs ON st.snapshot_id = rs.id
                WHERE st.company_name = @CompanyName
            ),
            latest_field_values AS (
                SELECT 
                    fc.ticket_key,
                    fc.field_name,
                    fc.field_value,
                    fc.observed_at,
                    ROW_NUMBER() OVER (
                        PARTITION BY fc.ticket_key, fc.field_name 
                        ORDER BY fc.observed_at DESC
                    ) as rn
                FROM field_change fc
                WHERE fc.company_name = @CompanyName
                    AND fc.observed_at <= @AsOfDate
            )
            SELECT DISTINCT
                lspt.ticket_key as TicketKey,
                @CompanyName as CompanyName,
                lspt.itsm_source as ItsmSource,
                lspt.snapshot_id as SnapshotId,
                lspt.snapshot_date as SnapshotDate,
                MAX(lfv.observed_at) as ObservedAt
            FROM latest_snapshot_per_ticket lspt
            LEFT JOIN latest_field_values lfv ON lspt.ticket_key = lfv.ticket_key AND lfv.rn = 1
            WHERE lspt.rn = 1
            GROUP BY lspt.ticket_key, lspt.snapshot_id, lspt.itsm_source, lspt.snapshot_date
            ORDER BY lspt.ticket_key
            """;

        using IDbConnection connection = GetConnection();
        IEnumerable<TicketQueryResult> ticketRows = await connection.QueryAsync<TicketQueryResult>(
            sql,
            new { ItsmSource = itsmSource, CompanyName = companyName, AsOfDate = asOfDate }
        );

        List<Ticket> tickets = [];

        foreach (TicketQueryResult row in ticketRows)
        {
            string ticketKey = row.TicketKey;
            DateTime snapshotDate = ParseDateTime(row.SnapshotDate);
            IDictionary<string, string?> fields = await GetTicketFieldsAsOfDateAsync(
                companyName,
                ticketKey,
                asOfDate
            );

            tickets.Add(new Ticket
            {
                TicketKey = ticketKey,
                CompanyName = companyName,
                ItsmSource = row.ItsmSource,
                Fields = fields,
                ObservedAt = !string.IsNullOrEmpty(row.ObservedAt) ? ParseDateTime(row.ObservedAt) : DateTime.MinValue,
                SnapshotId = Guid.Parse(row.SnapshotId),
                SnapshotDate = snapshotDate
            });
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves all tickets from a specific snapshot.
    /// </summary>
    public static async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
    {
        const string sql = """
            SELECT 
                st.ticket_key as TicketKey,
                st.company_name as CompanyName,
                rs.itsm_source as ItsmSource,
                st.snapshot_id as SnapshotId,
                rs.snapshot_date as SnapshotDate,
                MAX(fc.observed_at) as ObservedAt
            FROM snapshot_ticket st
            JOIN raw_snapshot rs ON st.snapshot_id = rs.id
            LEFT JOIN field_change fc ON st.ticket_key = fc.ticket_key 
                AND st.company_name = fc.company_name
                AND st.snapshot_id = fc.snapshot_id
            WHERE st.snapshot_id = @SnapshotId
            GROUP BY st.ticket_key, st.company_name, st.snapshot_id, rs.itsm_source, rs.snapshot_date
            ORDER BY st.ticket_key
            """;

        using IDbConnection connection = GetConnection();
        IEnumerable<TicketQueryResult> ticketRows = await connection.QueryAsync<TicketQueryResult>(
            sql,
            new { SnapshotId = snapshotId }
        );

        List<Ticket> tickets = [];

        foreach (TicketQueryResult row in ticketRows)
        {
            string companyName = row.CompanyName;
            string ticketKey = row.TicketKey;
            DateTime snapshotDate = ParseDateTime(row.SnapshotDate);

            IDictionary<string, string?> fields = await GetTicketFieldsAsOfDateAsync(
                companyName,
                ticketKey,
                snapshotDate
            );

            tickets.Add(new Ticket
            {
                TicketKey = ticketKey,
                CompanyName = companyName,
                ItsmSource = row.ItsmSource,
                Fields = fields,
                ObservedAt = !string.IsNullOrEmpty(row.ObservedAt) ? ParseDateTime(row.ObservedAt) : DateTime.MinValue,
                SnapshotId = snapshotId,
                SnapshotDate = snapshotDate
            });
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves the complete change history for a specific field of a ticket.
    /// Returns all observed values in chronological order.
    /// </summary>
    public static async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
        string companyName,
        string ticketKey,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("Company name is required.", nameof(companyName));
        }

        const string sql = """
            SELECT 
                id as Id,
                company_name as CompanyName,
                ticket_key as TicketKey,
                field_name as FieldName,
                field_value as FieldValue,
                observed_at as ObservedAt,
                snapshot_id as SnapshotId
            FROM field_change
            WHERE company_name = @CompanyName
                AND ticket_key = @TicketKey
                AND field_name = @FieldName
            ORDER BY observed_at ASC
            """;

        using IDbConnection connection = GetConnection();
        IEnumerable<FieldChangeQueryResult> changes = await connection.QueryAsync<FieldChangeQueryResult>(
            sql,
            new { CompanyName = companyName, TicketKey = ticketKey, FieldName = fieldName }
        );

        List<FieldChange> results = [];
        foreach (FieldChangeQueryResult change in changes)
        {
            results.Add(new FieldChange
            {
                Id = change.Id,
                CompanyName = change.CompanyName,
                TicketKey = change.TicketKey,
                FieldName = change.FieldName,
                FieldValue = change.FieldValue,
                ObservedAt = ParseDateTime(change.ObservedAt),
                SnapshotId = Guid.Parse(change.SnapshotId)
            });
        }

        return results;
    }

    /// <summary>
    /// Retrieves all snapshots, optionally filtered by ITSM source.
    /// Results are ordered by snapshot date descending (most recent first).
    /// </summary>
    public static async Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource = null)
    {
        string sql = """
            SELECT
                id as Id,
                itsm_source as ItsmSource,
                snapshot_date as SnapshotDate,
                uploaded_by as UploadedBy,
                uploaded_at as UploadedAt,
                upload_metadata as UploadMetadata
            FROM raw_snapshot
            """;

        if (!string.IsNullOrWhiteSpace(itsmSource))
        {
            sql += " WHERE itsm_source = @ItsmSource";
        }

        sql += " ORDER BY snapshot_date DESC";

        using IDbConnection connection = GetConnection();
        IEnumerable<SnapshotQueryResult> rows = await connection.QueryAsync<SnapshotQueryResult>(
            sql,
            new { ItsmSource = itsmSource }
        );

        return rows.Select(row => new Snapshot
        {
            Id = row.Id,
            ItsmSource = row.ItsmSource,
            SnapshotDate = ParseDateTime(row.SnapshotDate),
            UploadedBy = row.UploadedBy,
            UploadedAt = ParseDateTime(row.UploadedAt),
            UploadMetadata = row.UploadMetadata
        }).ToList();
    }

    /// <summary>
    /// Retrieves snapshots filtered by optional ITSM source and/or company name.
    /// When <paramref name="companyName"/> is provided a subquery filters to snapshots
    /// that contain at least one ticket for that company.
    /// Results are ordered by snapshot date descending (most recent first).
    /// </summary>
    public static async Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource, string? companyName)
    {
        List<string> whereClauses = [];

        if (!string.IsNullOrWhiteSpace(itsmSource))
        {
            whereClauses.Add("itsm_source = @ItsmSource");
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            whereClauses.Add("id IN (SELECT DISTINCT snapshot_id FROM snapshot_ticket WHERE company_name = @CompanyName)");
        }

        string sql = """
            SELECT
                id as Id,
                itsm_source as ItsmSource,
                snapshot_date as SnapshotDate,
                uploaded_by as UploadedBy,
                uploaded_at as UploadedAt,
                upload_metadata as UploadMetadata
            FROM raw_snapshot
            """;

        if (whereClauses.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", whereClauses);
        }

        sql += " ORDER BY snapshot_date DESC";

        using IDbConnection connection = GetConnection();
        IEnumerable<SnapshotQueryResult> rows = await connection.QueryAsync<SnapshotQueryResult>(
            sql,
            new { ItsmSource = itsmSource, CompanyName = companyName }
        );

        return rows.Select(row => new Snapshot
        {
            Id = row.Id,
            ItsmSource = row.ItsmSource,
            SnapshotDate = ParseDateTime(row.SnapshotDate),
            UploadedBy = row.UploadedBy,
            UploadedAt = ParseDateTime(row.UploadedAt),
            UploadMetadata = row.UploadMetadata
        }).ToList();
    }

    private static async Task<IDictionary<string, string?>> GetTicketFieldsAsOfDateAsync(
        string companyName,
        string ticketKey,
        DateTime asOfDate)
    {
        const string sql = """
            WITH latest_field_values AS (
                SELECT 
                    field_name,
                    field_value,
                    ROW_NUMBER() OVER (
                        PARTITION BY field_name 
                        ORDER BY observed_at DESC
                    ) as rn
                FROM field_change
                WHERE company_name = @CompanyName
                    AND ticket_key = @TicketKey
                    AND observed_at <= @AsOfDate
            )
            SELECT 
                field_name as FieldName,
                field_value as FieldValue
            FROM latest_field_values
            WHERE rn = 1
            ORDER BY field_name
            """;

        using IDbConnection connection = GetConnection();
        IEnumerable<FieldQueryResult> fields = await connection.QueryAsync<FieldQueryResult>(
            sql,
            new { CompanyName = companyName, TicketKey = ticketKey, AsOfDate = asOfDate }
        );

        Dictionary<string, string?> result = [];
        foreach (FieldQueryResult field in fields)
        {
            result[field.FieldName] = field.FieldValue;
        }

        return result;
    }

    private static string BuildSnapshotTicketInsertSql(IDbConnection connection)
    {
        return "INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key) VALUES (@Id, @SnapshotId, @CompanyName, @TicketKey) ON CONFLICT (snapshot_id, ticket_key) DO NOTHING;";
    }
}
