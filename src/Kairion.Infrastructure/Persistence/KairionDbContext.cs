using Kairion.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairion.Infrastructure.Persistence;

public sealed class KairionDbContext : DbContext
{
    public KairionDbContext(DbContextOptions<KairionDbContext> options) : base(options) { }

    public DbSet<ResearchProject> ResearchProjects => Set<ResearchProject>();
    public DbSet<SourceItem> SourceItems => Set<SourceItem>();
    public DbSet<ScreeningResult> ScreeningResults => Set<ScreeningResult>();
    public DbSet<DeepAnalysis> DeepAnalyses => Set<DeepAnalysis>();
    public DbSet<PainCluster> PainClusters => Set<PainCluster>();
    public DbSet<ClusterAssignment> ClusterAssignments => Set<ClusterAssignment>();
    public DbSet<HumanRevision> HumanRevisions => Set<HumanRevision>();
    public DbSet<Observation> Observations => Set<Observation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ResearchProjectConfiguration());
        modelBuilder.ApplyConfiguration(new SourceItemConfiguration());
        modelBuilder.ApplyConfiguration(new ScreeningResultConfiguration());
        modelBuilder.ApplyConfiguration(new DeepAnalysisConfiguration());
        modelBuilder.ApplyConfiguration(new PainClusterConfiguration());
        modelBuilder.ApplyConfiguration(new ClusterAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new HumanRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new ObservationConfiguration());
    }
}

internal static class ListConverters
{
    /// <summary>
    /// Separator used to flatten a string list into a single column value. The unit
    /// separator (0x1F) is not a valid character in source ids or competitors, so it
    /// cannot collide with stored values.
    /// </summary>
    public const char Separator = '\u001f';

    public static string ToJoined(IEnumerable<string>? values) =>
        values is null ? string.Empty : string.Join(Separator, values);

    public static IReadOnlyList<string> FromJoined(string? joined) =>
        string.IsNullOrEmpty(joined)
            ? Array.Empty<string>()
            : joined.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    public static ValueComparer<IReadOnlyList<string>> Comparer { get; } = new(
        (a, b) => a!.SequenceEqual(b!),
        v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
        v => (IReadOnlyList<string>)v.ToArray());

    /// <summary>
    /// Pre-built <see cref="ValueConverter"/> for the topics / source-id /
    /// competitor list properties. Converts between the <c>string</c> database column
    /// (joined by the unit separator) and the in-memory <see cref="IReadOnlyList{T}"/>.
    /// </summary>
    public static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IReadOnlyList<string>, string> StringListConverter { get; } =
        new(
            v => ToJoined(v),
            v => FromJoined(v));
}

internal sealed class ResearchProjectConfiguration : IEntityTypeConfiguration<ResearchProject>
{
    public void Configure(EntityTypeBuilder<ResearchProject> b)
    {
        b.ToTable("research_projects");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id");
        b.Property(p => p.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        b.Property(p => p.BriefKind).HasColumnName("brief_kind").HasConversion<string>().IsRequired().HasMaxLength(32);
        b.Property(p => p.BriefText).HasColumnName("brief_text").IsRequired().HasMaxLength(4_000);

        b.Property(p => p.Topics)
            .HasColumnName("topics")
            .HasConversion(ListConverters.StringListConverter)
            .HasMaxLength(8_000)
            .Metadata.SetValueComparer(ListConverters.Comparer);

        b.Property(p => p.EnabledSourceProviderIds)
            .HasColumnName("enabled_source_provider_ids")
            .HasConversion(ListConverters.StringListConverter)
            .HasMaxLength(8_000)
            .Metadata.SetValueComparer(ListConverters.Comparer);

        b.Property(p => p.IncludedCompetitors)
            .HasColumnName("included_competitors")
            .HasConversion(ListConverters.StringListConverter)
            .HasMaxLength(8_000)
            .Metadata.SetValueComparer(ListConverters.Comparer);

        b.Property(p => p.QueryStrategy)
            .HasColumnName("source_query_strategy")
            .HasMaxLength(2_000);
        b.Property(p => p.WindowStartUtc).HasColumnName("source_window_start_utc");
        b.Property(p => p.WindowEndUtc).HasColumnName("source_window_end_utc");

        b.Property(p => p.CreatedUtc).HasColumnName("created_utc");
        b.Property(p => p.UpdatedUtc).HasColumnName("updated_utc");
        b.Property(p => p.State).HasColumnName("state").HasConversion<string>().IsRequired().HasMaxLength(32);
        b.Property(p => p.ArchivedUtc).HasColumnName("archived_utc");

        b.Ignore(p => p.SourceConfiguration);
    }
}

internal sealed class SourceItemConfiguration : IEntityTypeConfiguration<SourceItem>
{
    public void Configure(EntityTypeBuilder<SourceItem> b)
    {
        b.ToTable("source_items");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id");
        b.Property(s => s.ProjectId).HasColumnName("project_id");
        b.Property(s => s.ProviderId).HasColumnName("provider_id").IsRequired().HasMaxLength(100);
        b.Property(s => s.ExternalId).HasColumnName("external_id").IsRequired().HasMaxLength(500);
        b.Property(s => s.CanonicalUrl).HasColumnName("canonical_url").IsRequired().HasMaxLength(2_000);
        b.Property(s => s.Title).HasColumnName("title").HasMaxLength(500);
        b.Property(s => s.Excerpt).HasColumnName("excerpt").HasMaxLength(8_000);
        b.Property(s => s.PublishedUtc).HasColumnName("published_utc");
        b.Property(s => s.ObservedUtc).HasColumnName("observed_utc");
        b.Property(s => s.ProvenanceJson).HasColumnName("provenance_json").HasColumnType("jsonb");
        b.Property(s => s.DuplicateOfId).HasColumnName("duplicate_of_id");
        b.Property(s => s.CreatedUtc).HasColumnName("created_utc");

        b.Property(s => s.LatestObservationProviderId).HasColumnName("latest_observation_provider_id").HasMaxLength(100);
        b.Property(s => s.LatestObservationStatus).HasColumnName("latest_observation_status").HasConversion<string>().HasMaxLength(32);
        b.Property(s => s.LatestObservationErrorCode).HasColumnName("latest_observation_error_code").HasMaxLength(100);
        b.Property(s => s.LatestObservationMessage).HasColumnName("latest_observation_message").HasMaxLength(2_000);
        b.Property(s => s.LatestObservationObservedUtc).HasColumnName("latest_observation_observed_utc");
        b.Property(s => s.LatestObservationHttpStatus).HasColumnName("latest_observation_http_status");

        b.Ignore(s => s.LatestObservation);

        b.HasIndex(s => new { s.ProjectId, s.ProviderId, s.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_source_items_identity");
        b.HasIndex(s => new { s.ProjectId, s.CanonicalUrl })
            .HasDatabaseName("ix_source_items_canonical_url");
        b.HasIndex(s => s.ObservedUtc).HasDatabaseName("ix_source_items_observed_utc");
    }
}

internal sealed class ScreeningResultConfiguration : IEntityTypeConfiguration<ScreeningResult>
{
    public void Configure(EntityTypeBuilder<ScreeningResult> b)
    {
        b.ToTable("screening_results");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id");
        b.Property(s => s.SourceItemId).HasColumnName("source_item_id");
        b.Property(s => s.AnalysisVersion).HasColumnName("analysis_version").IsRequired().HasMaxLength(50);
        b.Property(s => s.Relevance).HasColumnName("relevance").HasColumnType("numeric(6,4)");
        b.Property(s => s.Pain).HasColumnName("pain").HasColumnType("numeric(6,4)");
        b.Property(s => s.CommercialHint).HasColumnName("commercial_hint").HasColumnType("numeric(6,4)");
        b.Property(s => s.Spam).HasColumnName("spam");
        b.Property(s => s.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(32);
        b.Property(s => s.ProviderId).HasColumnName("provider_id").HasMaxLength(100);
        b.Property(s => s.Model).HasColumnName("model").HasMaxLength(100);
        b.Property(s => s.CreatedUtc).HasColumnName("created_utc");
        b.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);

        b.HasIndex(s => s.SourceItemId).HasDatabaseName("ix_screening_results_source_item");
        b.HasIndex(s => new { s.SourceItemId, s.AnalysisVersion })
            .HasDatabaseName("ix_screening_results_source_version");
    }
}

internal sealed class DeepAnalysisConfiguration : IEntityTypeConfiguration<DeepAnalysis>
{
    public void Configure(EntityTypeBuilder<DeepAnalysis> b)
    {
        b.ToTable("deep_analyses");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("id");
        b.Property(d => d.SourceItemId).HasColumnName("source_item_id");
        b.Property(d => d.SchemaVersion).HasColumnName("schema_version").IsRequired().HasMaxLength(50);
        b.Property(d => d.Problem).HasColumnName("problem").HasMaxLength(2_000);
        b.Property(d => d.Context).HasColumnName("context").HasMaxLength(4_000);
        b.Property(d => d.CurrentSolution).HasColumnName("current_solution").HasMaxLength(2_000);
        b.Property(d => d.Dissatisfaction).HasColumnName("dissatisfaction").HasMaxLength(2_000);
        b.Property(d => d.Workaround).HasColumnName("workaround").HasMaxLength(2_000);
        b.Property(d => d.DesiredOutcome).HasColumnName("desired_outcome").HasMaxLength(2_000);
        b.Property(d => d.Category).HasColumnName("category").HasMaxLength(200);
        b.Property(d => d.PriceSensitivity).HasColumnName("price_sensitivity").HasMaxLength(32);
        b.Property(d => d.PainStrength).HasColumnName("pain_strength").HasColumnType("numeric(6,4)");
        b.Property(d => d.Confidence).HasColumnName("confidence").HasColumnType("numeric(6,4)");
        b.Property(d => d.ProviderId).HasColumnName("provider_id").HasMaxLength(100);
        b.Property(d => d.Model).HasColumnName("model").HasMaxLength(100);
        b.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        b.Property(d => d.CreatedUtc).HasColumnName("created_utc");
        b.Property(d => d.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);

        b.HasIndex(d => d.SourceItemId).HasDatabaseName("ix_deep_analyses_source_item");
        b.HasIndex(d => new { d.SourceItemId, d.SchemaVersion })
            .HasDatabaseName("ix_deep_analyses_source_version");
    }
}

internal sealed class PainClusterConfiguration : IEntityTypeConfiguration<PainCluster>
{
    public void Configure(EntityTypeBuilder<PainCluster> b)
    {
        b.ToTable("pain_clusters");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id");
        b.Property(c => c.ProjectId).HasColumnName("project_id");
        b.Property(c => c.Label).HasColumnName("label").IsRequired().HasMaxLength(200);
        b.Property(c => c.Category).HasColumnName("category").IsRequired().HasMaxLength(200);
        b.Property(c => c.Summary).HasColumnName("summary").IsRequired().HasMaxLength(4_000);
        b.Property(c => c.Version).HasColumnName("version");
        b.Property(c => c.ReviewStateVersion).HasColumnName("review_state_version");
        b.Property(c => c.CreatedUtc).HasColumnName("created_utc");
        b.Property(c => c.UpdatedUtc).HasColumnName("updated_utc");

        b.HasIndex(c => c.ProjectId).HasDatabaseName("ix_pain_clusters_project");
    }
}

internal sealed class ClusterAssignmentConfiguration : IEntityTypeConfiguration<ClusterAssignment>
{
    public void Configure(EntityTypeBuilder<ClusterAssignment> b)
    {
        b.ToTable("cluster_assignments");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id");
        b.Property(a => a.ClusterId).HasColumnName("cluster_id");
        b.Property(a => a.SourceItemId).HasColumnName("source_item_id");
        b.Property(a => a.DeepAnalysisId).HasColumnName("deep_analysis_id");
        b.Property(a => a.Origin).HasColumnName("origin").HasConversion<string>().HasMaxLength(16);
        b.Property(a => a.CreatedUtc).HasColumnName("created_utc");
        b.Property(a => a.SupersedesId).HasColumnName("supersedes_id");

        b.HasIndex(a => a.ClusterId).HasDatabaseName("ix_cluster_assignments_cluster");
        b.HasIndex(a => a.SourceItemId).HasDatabaseName("ix_cluster_assignments_source");
    }
}

internal sealed class HumanRevisionConfiguration : IEntityTypeConfiguration<HumanRevision>
{
    public void Configure(EntityTypeBuilder<HumanRevision> b)
    {
        b.ToTable("human_revisions");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id");
        b.Property(r => r.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(32);
        b.Property(r => r.ClusterId).HasColumnName("cluster_id");
        b.Property(r => r.SourceItemId).HasColumnName("source_item_id");
        b.Property(r => r.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        b.Property(r => r.CreatedUtc).HasColumnName("created_utc");

        b.HasIndex(r => r.ClusterId).HasDatabaseName("ix_human_revisions_cluster");
    }
}

internal sealed class ObservationConfiguration : IEntityTypeConfiguration<Observation>
{
    public void Configure(EntityTypeBuilder<Observation> b)
    {
        b.ToTable("observations");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).HasColumnName("id");
        b.Property(o => o.ProjectId).HasColumnName("project_id");
        b.Property(o => o.SourceItemId).HasColumnName("source_item_id");
        b.Property(o => o.ClusterId).HasColumnName("cluster_id");
        b.Property(o => o.ObservedUtc).HasColumnName("observed_utc");
        b.Property(o => o.FirstObservedUtc).HasColumnName("first_observed_utc");

        b.HasIndex(o => new { o.ProjectId, o.ObservedUtc }).HasDatabaseName("ix_observations_project_observed");
        b.HasIndex(o => new { o.ProjectId, o.ClusterId, o.ObservedUtc }).HasDatabaseName("ix_observations_project_cluster_observed");
        b.HasIndex(o => o.SourceItemId).HasDatabaseName("ix_observations_source");
    }
}
