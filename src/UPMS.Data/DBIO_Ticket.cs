namespace UPMS.Data;

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Options;

// Internal DTOs for query results
internal class TicketQueryResult
{
    public string TicketKey { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ItsmSource { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public string? ObservedAt { get; set; }
}

internal class FieldQueryResult
{
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
}

internal class FieldChangeQueryResult
{
    public long Id { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public string ObservedAt { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
}

public static class DBIO_Ticket
{
    private static Func<IDbConnection>? _connectionFactory;

    public static void Initialize(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        _connectionFactory = () => new NpgsqlConnection(connectionString);
    }

    public static void Initialize(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Initialize(options.Value.ConnectionString);
    }

    public static void Initialize(Func<IDbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    private static IDbConnection GetConnection()
    {
        if (_connectionFactory == null)
        {
            throw new InvalidOperationException(
                "DBIO_Ticket has not been initialized. Call Initialize() first.");
        }
        return _connectionFactory();
    }

    private static DateTime ParseDateTime(string? dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr))
        {
            return DateTime.MinValue;
        }

        // Try to parse as ISO 8601 format
        if (DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
        {
            return result;
        }

        // Try standard DateTime parsing
        if (DateTime.TryParse(dateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result2))
        {
            return result2;
        }

        return DateTime.MinValue;
    }

    /// <summary>
    /// Retrieves tickets for a company as they appeared at a specific point in time.
    /// Reconstructs ticket state by finding the latest field values observed on or before the specified date.
    /// </summary>
    public static async Task<IEnumerable<Ticket>> GetTicketsAsync(Guid companyId, DateTime asOfDate)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID cannot be empty.", nameof(companyId));
        }

        const string sql = """
            WITH all_snapshots AS (
                SELECT id, snapshot_date, itsm_source
                FROM raw_snapshot
                WHERE company_id = @CompanyId
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
                WHERE st.company_id = @CompanyId
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
                WHERE fc.company_id = @CompanyId
                    AND fc.observed_at <= @AsOfDate
            )
            SELECT DISTINCT
                lspt.ticket_key as TicketKey,
                @CompanyId as CompanyId,
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

        using (var connection = GetConnection())
        {
            var ticketRows = await connection.QueryAsync<TicketQueryResult>(
                sql,
                new { CompanyId = companyId.ToString(), AsOfDate = asOfDate }
            );

            var tickets = new List<Ticket>();

            foreach (var row in ticketRows)
            {
                var ticketKey = row.TicketKey;
                var snapshotDate = ParseDateTime(row.SnapshotDate);
                var fields = await GetTicketFieldsAsOfDateAsync(
                    companyId, 
                    ticketKey, 
                    asOfDate
                );

                tickets.Add(new Ticket
                {
                    TicketKey = ticketKey,
                    CompanyId = companyId,
                    ItsmSource = row.ItsmSource,
                    Fields = fields,
                    ObservedAt = !string.IsNullOrEmpty(row.ObservedAt) ? ParseDateTime(row.ObservedAt) : DateTime.MinValue,
                    SnapshotId = Guid.Parse(row.SnapshotId),
                    SnapshotDate = snapshotDate
                });
            }

            // Return as List<Ticket> instead of IEnumerable to ensure proper equality comparison
            return (IEnumerable<Ticket>)tickets;
        }
    }

    /// <summary>
    /// Retrieves all tickets from a specific snapshot.
    /// </summary>
    public static async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
    {
        const string sql = """
            SELECT 
                st.ticket_key as TicketKey,
                st.company_id as CompanyId,
                rs.itsm_source as ItsmSource,
                st.snapshot_id as SnapshotId,
                rs.snapshot_date as SnapshotDate,
                MAX(fc.observed_at) as ObservedAt
            FROM snapshot_ticket st
            JOIN raw_snapshot rs ON st.snapshot_id = rs.id
            LEFT JOIN field_change fc ON st.ticket_key = fc.ticket_key 
                AND st.company_id = fc.company_id
                AND st.snapshot_id = fc.snapshot_id
            WHERE st.snapshot_id = @SnapshotId
            GROUP BY st.ticket_key, st.company_id, st.snapshot_id, rs.itsm_source, rs.snapshot_date
            ORDER BY st.ticket_key
            """;

        using (var connection = GetConnection())
        {
            var ticketRows = await connection.QueryAsync<TicketQueryResult>(
                sql, 
                new { SnapshotId = snapshotId.ToString() }
            );

            var tickets = new List<Ticket>();

            foreach (var row in ticketRows)
            {
                var companyId = Guid.Parse(row.CompanyId);
                var ticketKey = row.TicketKey;
                var snapshotDate = ParseDateTime(row.SnapshotDate);

                var fields = await GetTicketFieldsAsOfDateAsync(
                    companyId,
                    ticketKey,
                    snapshotDate
                );

                tickets.Add(new Ticket
                {
                    TicketKey = ticketKey,
                    CompanyId = companyId,
                    ItsmSource = row.ItsmSource,
                    Fields = fields,
                    ObservedAt = !string.IsNullOrEmpty(row.ObservedAt) ? ParseDateTime(row.ObservedAt) : DateTime.MinValue,
                    SnapshotId = snapshotId,
                    SnapshotDate = snapshotDate
                });
            }

            return tickets;
        }
    }

    /// <summary>
    /// Retrieves the complete change history for a specific field of a ticket.
    /// Returns all observed values in chronological order.
    /// </summary>
    public static async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
        Guid companyId,
        string ticketKey,
        string fieldName)
    {
        const string sql = """
            SELECT 
                id as Id,
                company_id as CompanyId,
                ticket_key as TicketKey,
                field_name as FieldName,
                field_value as FieldValue,
                observed_at as ObservedAt,
                snapshot_id as SnapshotId
            FROM field_change
            WHERE company_id = @CompanyId
                AND ticket_key = @TicketKey
                AND field_name = @FieldName
            ORDER BY observed_at ASC
            """;

        using (var connection = GetConnection())
        {
            var changes = await connection.QueryAsync<FieldChangeQueryResult>(
                sql,
                new { CompanyId = companyId.ToString(), TicketKey = ticketKey, FieldName = fieldName }
            );

            var results = new List<FieldChange>();
            foreach (var change in changes)
            {
                results.Add(new FieldChange
                {
                    Id = change.Id,
                    CompanyId = Guid.Parse(change.CompanyId),
                    TicketKey = change.TicketKey,
                    FieldName = change.FieldName,
                    FieldValue = change.FieldValue,
                    ObservedAt = ParseDateTime(change.ObservedAt),
                    SnapshotId = Guid.Parse(change.SnapshotId)
                });
            }

            return results;
        }
    }

    private static async Task<IDictionary<string, string?>> GetTicketFieldsAsOfDateAsync(
        Guid companyId,
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
                WHERE company_id = @CompanyId
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

        using (var connection = GetConnection())
        {
            var fields = await connection.QueryAsync<FieldQueryResult>(
                sql,
                new { CompanyId = companyId.ToString(), TicketKey = ticketKey, AsOfDate = asOfDate }
            );

            var result = new Dictionary<string, string?>();
            foreach (var field in fields)
            {
                result[field.FieldName] = field.FieldValue;
            }

            return result;
        }
    }
}
