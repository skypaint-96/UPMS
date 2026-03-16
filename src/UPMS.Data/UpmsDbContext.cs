namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using UPMS.Data.ReadModels;
using UPMS.Data.Jobs;
using UPMS.Data.Delivery;

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
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<DistributionList> DistributionLists => Set<DistributionList>();
    public DbSet<DistributionListRecipient> DistributionListRecipients => Set<DistributionListRecipient>();
    public DbSet<ReportDelivery> ReportDeliveries => Set<ReportDelivery>();
    public DbSet<FileSharePollingSource> FileSharePollingSources => Set<FileSharePollingSource>();

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

        // ── file_share_polling_source ─────────────────────────────────────────
        modelBuilder.Entity<FileSharePollingSource>(e =>
        {
            e.ToTable("file_share_polling_source");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(200)")
                .IsRequired();
            e.Property(x => x.Enabled)
                .HasColumnName("enabled")
                .HasColumnType("boolean")
                .HasDefaultValue(true);
            e.Property(x => x.WatchedPath)
                .HasColumnName("watched_path")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.FilePatternsJson)
                .HasColumnName("file_patterns_json")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.ArchivePath)
                .HasColumnName("archive_path")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.ErrorPath)
                .HasColumnName("error_path")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.ItsmSource)
                .HasColumnName("itsm_source")
                .HasColumnType("varchar(100)")
                .IsRequired();
            e.Property(x => x.PollIntervalSeconds)
                .HasColumnName("poll_interval_seconds")
                .HasColumnType("integer")
                .HasDefaultValue(300);
            e.Property(x => x.MaxFilesPerCycle)
                .HasColumnName("max_files_per_cycle")
                .HasColumnType("integer")
                .IsRequired(false);
            e.Property(x => x.StableFileAgeSeconds)
                .HasColumnName("stable_file_age_seconds")
                .HasColumnType("integer")
                .HasDefaultValue(30);
            e.Property(x => x.LastRunStartedAt)
                .HasColumnName("last_run_started_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.LastRunCompletedAt)
                .HasColumnName("last_run_completed_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.LastSucceededAt)
                .HasColumnName("last_succeeded_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.NextPollDueAt)
                .HasColumnName("next_poll_due_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.LastError)
                .HasColumnName("last_error")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.CurrentJobId)
                .HasColumnName("current_job_id")
                .HasColumnType("uuid")
                .IsRequired(false);
            e.Property(x => x.LastJobId)
                .HasColumnName("last_job_id")
                .HasColumnType("uuid")
                .IsRequired(false);
            e.Property(x => x.IsSystemManaged)
                .HasColumnName("is_system_managed")
                .HasColumnType("boolean")
                .HasDefaultValue(false);
            e.Property(x => x.CreatedBy)
                .HasColumnName("created_by")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);

            e.HasIndex(x => x.Name)
                .IsUnique()
                .HasDatabaseName("ix_file_share_polling_source_name");
            e.HasIndex(x => x.WatchedPath)
                .IsUnique()
                .HasDatabaseName("ix_file_share_polling_source_watched_path");
            e.HasIndex(x => new { x.Enabled, x.NextPollDueAt })
                .HasDatabaseName("ix_file_share_polling_source_enabled_due_at");
            e.HasIndex(x => x.CurrentJobId)
                .HasDatabaseName("ix_file_share_polling_source_current_job_id");
        });

        // ── background_job ───────────────────────────────────────────────────
        modelBuilder.Entity<BackgroundJob>(e =>
        {
            e.ToTable("background_job");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.JobType)
                .HasColumnName("job_type")
                .HasColumnType("varchar(100)")
                .IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasColumnType("varchar(50)")
                .IsRequired();
            e.Property(x => x.PayloadJson)
                .HasColumnName("payload_json")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.ResultJson)
                .HasColumnName("result_json")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.RequestedBy)
                .HasColumnName("requested_by")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.StartedAt)
                .HasColumnName("started_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.CompletedAt)
                .HasColumnName("completed_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.ErrorMessage)
                .HasColumnName("error_message")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.LeaseOwner)
                .HasColumnName("lease_owner")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.LeaseExpiresAt)
                .HasColumnName("lease_expires_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.OutputFilePath)
                .HasColumnName("output_file_path")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.OutputFileName)
                .HasColumnName("output_file_name")
                .HasColumnType("varchar(512)")
                .IsRequired(false);
            e.Property(x => x.OutputContentType)
                .HasColumnName("output_content_type")
                .HasColumnType("varchar(255)")
                .IsRequired(false);

            e.HasIndex(x => new { x.Status, x.CreatedAt })
                .HasDatabaseName("ix_background_job_status_created_at");
            e.HasIndex(x => new { x.JobType, x.Status, x.CreatedAt })
                .HasDatabaseName("ix_background_job_job_type_status_created_at");
        });


        // ── company_profile ───────────────────────────────────────────────
        modelBuilder.Entity<CompanyProfile>(e =>
        {
            e.ToTable("company_profile");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.CompanyKey)
                .HasColumnName("company_key")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.DisplayName)
                .HasColumnName("display_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            e.HasIndex(x => x.CompanyKey)
                .IsUnique()
                .HasDatabaseName("ix_company_profile_company_key");
            e.HasIndex(x => x.DisplayName)
                .HasDatabaseName("ix_company_profile_display_name");
        });

        // ── distribution_list ────────────────────────────────────────────
        modelBuilder.Entity<DistributionList>(e =>
        {
            e.ToTable("distribution_list");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.CompanyProfileId)
                .HasColumnName("company_profile_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasColumnType("boolean")
                .HasDefaultValue(true)
                .IsRequired();
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.CreatedBy)
                .HasColumnName("created_by")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by")
                .HasColumnType("varchar(255)")
                .IsRequired(false);

            e.HasIndex(x => new { x.CompanyProfileId, x.Name })
                .IsUnique()
                .HasDatabaseName("ix_distribution_list_company_profile_id_name");
            e.HasIndex(x => new { x.CompanyProfileId, x.IsActive, x.Name })
                .HasDatabaseName("ix_distribution_list_company_profile_id_is_active_name");

            e.HasOne(x => x.CompanyProfile)
                .WithMany(x => x.DistributionLists)
                .HasForeignKey(x => x.CompanyProfileId)
                .HasConstraintName("fk_distribution_list_company_profile_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── distribution_list_recipient ──────────────────────────────────
        modelBuilder.Entity<DistributionListRecipient>(e =>
        {
            e.ToTable("distribution_list_recipient");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.DistributionListId)
                .HasColumnName("distribution_list_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasColumnType("varchar(50)")
                .IsRequired();
            e.Property(x => x.Endpoint)
                .HasColumnName("endpoint")
                .HasColumnType("varchar(512)")
                .IsRequired();
            e.Property(x => x.DisplayName)
                .HasColumnName("display_name")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.MetadataJson)
                .HasColumnName("metadata_json")
                .HasColumnType("text")
                .IsRequired(false);
            e.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasColumnType("boolean")
                .HasDefaultValue(true)
                .IsRequired();
            e.Property(x => x.SortOrder)
                .HasColumnName("sort_order")
                .HasColumnType("integer")
                .HasDefaultValue(0)
                .IsRequired();
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            e.HasIndex(x => new { x.DistributionListId, x.Channel, x.Endpoint })
                .IsUnique()
                .HasDatabaseName("ix_distribution_list_recipient_distribution_list_id_channel_endpoint");
            e.HasIndex(x => new { x.DistributionListId, x.IsActive, x.SortOrder })
                .HasDatabaseName("ix_distribution_list_recipient_distribution_list_id_is_active_sort_order");

            e.HasOne(x => x.DistributionList)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.DistributionListId)
                .HasConstraintName("fk_distribution_list_recipient_distribution_list_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── report_delivery ───────────────────────────────────────────────
        modelBuilder.Entity<ReportDelivery>(e =>
        {
            e.ToTable("report_delivery");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();
            e.Property(x => x.ReportJobId)
                .HasColumnName("report_job_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.LastBackgroundJobId)
                .HasColumnName("last_background_job_id")
                .HasColumnType("uuid")
                .IsRequired(false);
            e.Property(x => x.CompanyProfileId)
                .HasColumnName("company_profile_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.CompanyKey)
                .HasColumnName("company_key")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.CompanyDisplayName)
                .HasColumnName("company_display_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.DistributionListId)
                .HasColumnName("distribution_list_id")
                .HasColumnType("uuid")
                .IsRequired();
            e.Property(x => x.DistributionListName)
                .HasColumnName("distribution_list_name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            e.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasColumnType("varchar(50)")
                .IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasColumnType("varchar(50)")
                .IsRequired();
            e.Property(x => x.ArtifactPath)
                .HasColumnName("artifact_path")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.ArtifactFileName)
                .HasColumnName("artifact_file_name")
                .HasColumnType("varchar(512)")
                .IsRequired(false);
            e.Property(x => x.ArtifactContentType)
                .HasColumnName("artifact_content_type")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.Subject)
                .HasColumnName("subject")
                .HasColumnType("varchar(512)")
                .IsRequired();
            e.Property(x => x.RecipientSnapshotJson)
                .HasColumnName("recipient_snapshot_json")
                .HasColumnType("text")
                .IsRequired();
            e.Property(x => x.RecipientCount)
                .HasColumnName("recipient_count")
                .HasColumnType("integer")
                .IsRequired();
            e.Property(x => x.AttemptCount)
                .HasColumnName("attempt_count")
                .HasColumnType("integer")
                .HasDefaultValue(0)
                .IsRequired();
            e.Property(x => x.RequestedBy)
                .HasColumnName("requested_by")
                .HasColumnType("varchar(255)")
                .IsRequired(false);
            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            e.Property(x => x.StartedAt)
                .HasColumnName("started_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.CompletedAt)
                .HasColumnName("completed_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.LastAttemptedAt)
                .HasColumnName("last_attempted_at")
                .HasColumnType("timestamptz")
                .IsRequired(false);
            e.Property(x => x.LastErrorMessage)
                .HasColumnName("last_error_message")
                .HasColumnType("text")
                .IsRequired(false);

            e.HasIndex(x => new { x.ReportJobId, x.CreatedAt })
                .HasDatabaseName("ix_report_delivery_report_job_id_created_at");
            e.HasIndex(x => new { x.CompanyKey, x.CreatedAt })
                .HasDatabaseName("ix_report_delivery_company_key_created_at");
            e.HasIndex(x => new { x.Status, x.CreatedAt })
                .HasDatabaseName("ix_report_delivery_status_created_at");
            e.HasIndex(x => new { x.DistributionListId, x.CreatedAt })
                .HasDatabaseName("ix_report_delivery_distribution_list_id_created_at");
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
