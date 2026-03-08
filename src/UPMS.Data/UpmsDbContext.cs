namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using UPMS.Data.ReadModels;

/// <summary>
/// EF Core database context for UPMS. Covers all persisted entities plus
/// keyless result sets for stored-procedure reads.
/// </summary>
public class UpmsDbContext : DbContext
{
    public UpmsDbContext(DbContextOptions<UpmsDbContext> options) : base(options) { }

    // ── Writeable entity sets ──────────────────────────────────────────────

    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<SnapshotTicket> SnapshotTickets => Set<SnapshotTicket>();
    public DbSet<FieldChange> FieldChanges => Set<FieldChange>();
    public DbSet<ItsmSource> ItsmSources => Set<ItsmSource>();
    public DbSet<ItsmFieldMapping> ItsmFieldMappings => Set<ItsmFieldMapping>();
    public DbSet<CanonicalFieldDefinition> CanonicalFieldDefinitions => Set<CanonicalFieldDefinition>();

    // ── Keyless result sets (stored-procedure reads) ───────────────────────

    public DbSet<TicketFieldAtTimeDto> TicketFieldAtTimeResults => Set<TicketFieldAtTimeDto>();
    public DbSet<TicketFieldWithMetadataDto> TicketFieldWithMetadataResults => Set<TicketFieldWithMetadataDto>();
    public DbSet<SnapshotTicketKeyDto> SnapshotTicketKeyResults => Set<SnapshotTicketKeyDto>();
    public DbSet<SnapshotTicketPairDto> SnapshotTicketPairResults => Set<SnapshotTicketPairDto>();
    public DbSet<ReconstructedFieldDto> ReconstructedFieldResults => Set<ReconstructedFieldDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        // ── raw_snapshot ───────────────────────────────────────────────────
        modelBuilder.Entity<Snapshot>(e =>
        {
            e.ToTable("raw_snapshot");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedOnAdd();
            e.Property(x => x.ItsmSource)
                .HasColumnName("itsm_source")
                .HasColumnType("varchar(100)")
                .IsRequired();
            e.Property(x => x.SnapshotDate)
                .HasColumnName("snapshot_date")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UploadedBy)
                .HasColumnName("uploaded_by")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.UploadedAt)
                .HasColumnName("uploaded_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UploadMetadata)
                .HasColumnName("upload_metadata")
                .HasColumnType("text")
                .IsRequired(false);
        });

        // ── snapshot_ticket ────────────────────────────────────────────────
        modelBuilder.Entity<SnapshotTicket>(e =>
        {
            e.ToTable("snapshot_ticket");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedOnAdd();
            e.Property(x => x.SnapshotId)
                .HasColumnName("snapshot_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.CompanyName)
                .HasColumnName("company_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.TicketKey)
                .HasColumnName("ticket_key")
                .HasColumnType("varchar(255)")
                .IsRequired();

            e.HasIndex(x => new { x.SnapshotId, x.TicketKey }).IsUnique();

            e.HasOne(x => x.Snapshot)
                .WithMany()
                .HasForeignKey(x => x.SnapshotId)
                .HasConstraintName("fk_snapshot_ticket_snapshot_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── field_change ───────────────────────────────────────────────────
        modelBuilder.Entity<FieldChange>(e =>
        {
            e.ToTable("field_change");
            e.HasKey(x => x.Id);

            var idBuilder = e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("bigint")
                .ValueGeneratedOnAdd();
            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(idBuilder);

            e.Property(x => x.CompanyName)
                .HasColumnName("company_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.FieldName)
                .HasColumnName("field_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.CanonicalFieldName)
                .HasColumnName("canonical_field_name")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.FieldValue)
                .HasColumnName("field_value")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.ObservedAt)
                .HasColumnName("observed_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.SnapshotId)
                .HasColumnName("snapshot_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.TicketKey)
                .HasColumnName("ticket_key")
                .HasColumnType("varchar(255)")
                .IsRequired();

            e.HasIndex(x => x.SnapshotId);

            e.HasOne<Snapshot>()
                .WithMany()
                .HasForeignKey(x => x.SnapshotId)
                .HasConstraintName("fk_field_change_snapshot_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── itsm_source ────────────────────────────────────────────────────
        modelBuilder.Entity<ItsmSource>(e =>
        {
            e.ToTable("itsm_source");
            e.HasKey(x => x.Id);

            var idBuilder = e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("integer")
                .ValueGeneratedOnAdd();
            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(idBuilder);

            e.Property(x => x.DisplayLabel)
                .HasColumnName("display_label")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("text")
                .IsRequired();

            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── itsm_field_mapping ─────────────────────────────────────────────
        modelBuilder.Entity<ItsmFieldMapping>(e =>
        {
            e.ToTable("itsm_field_mapping");
            e.HasKey(x => new { x.ItsmSource, x.SourceFieldName });
            e.Property(x => x.ItsmSource)
                .HasColumnName("itsm_source")
                .HasColumnType("varchar(100)");
            e.Property(x => x.SourceFieldName)
                .HasColumnName("source_field_name")
                .HasColumnType("varchar(255)");
            e.Property(x => x.CanonicalFieldName)
                .HasColumnName("canonical_field_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.IsRequired)
                .ValueGeneratedOnAdd()
                .HasColumnName("is_required")
                .HasColumnType("boolean")
                .HasDefaultValue(false);
        });

        // ── canonical_field_definition ─────────────────────────────────────
        modelBuilder.Entity<CanonicalFieldDefinition>(e =>
        {
            e.ToTable("canonical_field_definition");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.DataType)
                .HasColumnName("data_type")
                .HasColumnType("varchar(50)")
                .HasConversion<string>()
                .IsRequired();
            e.Property(x => x.IsSystemRequired)
                .ValueGeneratedOnAdd()
                .HasColumnName("is_system_required")
                .HasColumnType("boolean")
                .HasDefaultValue(false);
        });

        // ── Keyless DTOs (stored-procedure result sets) ────────────────────

        modelBuilder.Entity<ReconstructedFieldDto>(e =>
        {
            e.HasNoKey();
            e.ToTable("ReconstructedFieldResults");
            e.Property(x => x.FieldName)
                .HasColumnType("text")
                .HasColumnName("field_name")
                .IsRequired();
            e.Property(x => x.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value")
                .IsRequired(false);
            e.Property(x => x.TicketKey)
                .HasColumnType("text")
                .HasColumnName("ticket_key")
                .IsRequired();
        });

        modelBuilder.Entity<SnapshotTicketKeyDto>(e =>
        {
            e.HasNoKey();
            e.ToTable("SnapshotTicketKeyResults");
            e.Property(x => x.TicketKey)
                .HasColumnType("text")
                .HasColumnName("ticket_key")
                .IsRequired();
        });

        modelBuilder.Entity<SnapshotTicketPairDto>(e =>
        {
            e.HasNoKey();
            e.ToTable("SnapshotTicketPairResults");
            e.Property(x => x.SnapshotId)
                .HasColumnType("uuid")
                .HasColumnName("snapshot_id");
            e.Property(x => x.TicketKey)
                .HasColumnType("text")
                .HasColumnName("ticket_key")
                .IsRequired();
        });

        modelBuilder.Entity<TicketFieldAtTimeDto>(e =>
        {
            e.HasNoKey();
            e.ToTable("TicketFieldAtTimeResults");
            e.Property(x => x.FieldName)
                .HasColumnType("text")
                .HasColumnName("field_name")
                .IsRequired();
            e.Property(x => x.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value")
                .IsRequired(false);
            e.Property(x => x.ObservedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("observed_at");
        });

        modelBuilder.Entity<TicketFieldWithMetadataDto>(e =>
        {
            e.HasNoKey();
            e.ToTable("TicketFieldWithMetadataResults");
            e.Property(x => x.FieldName)
                .HasColumnType("text")
                .HasColumnName("field_name")
                .IsRequired();
            e.Property(x => x.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value")
                .IsRequired(false);
            e.Property(x => x.ObservedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("observed_at");
            e.Property(x => x.SnapshotId)
                .HasColumnType("uuid")
                .HasColumnName("snapshot_id");
        });
    }
}

/// <summary>
/// Design-time factory so that <c>dotnet ef</c> can instantiate
/// <see cref="UpmsDbContext"/> without a running host.
/// Reads the connection string from the <c>ConnectionStrings__DefaultConnection</c>
/// environment variable, falling back to a local development default.
/// </summary>
public class UpmsDbContextFactory : IDesignTimeDbContextFactory<UpmsDbContext>
{
    public UpmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UpmsDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=upms;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        return new UpmsDbContext(optionsBuilder.Options);
    }
}
