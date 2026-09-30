using System;
using FluentAssertions;
using Kairion.Domain;
using Xunit;

namespace Kairion.UnitTests.Domain;

/// <summary>
/// Unit tests for domain entity invariants. These do not touch EF; they only verify that
/// the validating constructors reject bad input and that the mutating operations enforce
/// their contracts.
/// </summary>
public class DomainEntityTests
{
    [Fact]
    public void ResearchProject_Constructor_RequiresTitle()
    {
        var act = () => new ResearchProject(
            id: Guid.NewGuid(),
            title: "  ",
            briefKind: BriefKind.Market,
            briefText: "ok",
            topics: Array.Empty<string>(),
            sourceConfiguration: new SourceConfiguration(
                enabledSourceProviderIds: Array.Empty<string>(),
                includedCompetitors: Array.Empty<string>(),
                queryStrategy: null,
                windowStartUtc: null,
                windowEndUtc: null),
            createdUtc: DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ResearchProject_Constructor_RequiresBrief()
    {
        var act = () => new ResearchProject(
            id: Guid.NewGuid(),
            title: "ok",
            briefKind: BriefKind.Market,
            briefText: " \t",
            topics: Array.Empty<string>(),
            sourceConfiguration: new SourceConfiguration(
                enabledSourceProviderIds: Array.Empty<string>(),
                includedCompetitors: Array.Empty<string>(),
                queryStrategy: null,
                windowStartUtc: null,
                windowEndUtc: null),
            createdUtc: DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ResearchProject_Archive_ThenRestore_TogglesState()
    {
        var project = NewProject();
        var now = DateTime.UtcNow;
        project.Archive(now);
        project.State.Should().Be(ResearchProjectState.Archived);
        project.ArchivedUtc.Should().Be(now);
        project.Restore(now);
        project.State.Should().Be(ResearchProjectState.Active);
        project.ArchivedUtc.Should().BeNull();
    }

    [Fact]
    public void SourceItem_Constructor_RequiresCanonicalUrl()
    {
        var act = () => new SourceItem(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            providerId: "manual",
            externalId: "x",
            canonicalUrl: "",
            title: null,
            excerpt: null,
            publishedUtc: null,
            observedUtc: DateTime.UtcNow,
            provenanceJson: "{}",
            latestObservation: new ProviderObservation("manual", ProviderStatus.Available, null, null, DateTime.UtcNow));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void SourceItem_EffectiveDate_PrefersPublishedOverObserved()
    {
        var observed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var published = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var item = new SourceItem(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            providerId: "manual",
            externalId: "x",
            canonicalUrl: "https://example.test/x",
            title: null,
            excerpt: null,
            publishedUtc: published,
            observedUtc: observed,
            provenanceJson: "{}",
            latestObservation: new ProviderObservation("manual", ProviderStatus.Available, null, null, observed));
        item.EffectiveDateUtc.Should().Be(published);
    }

    [Fact]
    public void SourceItem_EffectiveDate_FallsBackToObserved()
    {
        var observed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var item = new SourceItem(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            providerId: "manual",
            externalId: "x",
            canonicalUrl: "https://example.test/x",
            title: null,
            excerpt: null,
            publishedUtc: null,
            observedUtc: observed,
            provenanceJson: "{}",
            latestObservation: new ProviderObservation("manual", ProviderStatus.Available, null, null, observed));
        item.EffectiveDateUtc.Should().Be(observed);
    }

    [Fact]
    public void MarkDuplicateOf_RejectsSelf()
    {
        var id = Guid.NewGuid();
        var item = new SourceItem(
            id: id,
            projectId: Guid.NewGuid(),
            providerId: "manual",
            externalId: "x",
            canonicalUrl: "https://example.test/x",
            title: null,
            excerpt: null,
            publishedUtc: null,
            observedUtc: DateTime.UtcNow,
            provenanceJson: "{}",
            latestObservation: new ProviderObservation("manual", ProviderStatus.Available, null, null, DateTime.UtcNow));
        var act = () => item.MarkDuplicateOf(id, DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ClusterAssignment_Constructor_RejectsEmptyCluster()
    {
        var act = () => new ClusterAssignment(
            id: Guid.NewGuid(),
            clusterId: Guid.Empty,
            sourceItemId: Guid.NewGuid(),
            deepAnalysisId: null,
            origin: AssignmentOrigin.Ai,
            createdUtc: DateTime.UtcNow,
            supersedesId: null);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void PainCluster_Constructor_RequiresLabel()
    {
        var act = () => new PainCluster(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            label: "",
            category: "x",
            summary: "x",
            version: 1,
            createdUtc: DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    private static ResearchProject NewProject() => new(
        id: Guid.NewGuid(),
        title: "ok",
        briefKind: BriefKind.Market,
        briefText: "ok",
        topics: Array.Empty<string>(),
        sourceConfiguration: new SourceConfiguration(
            enabledSourceProviderIds: Array.Empty<string>(),
            includedCompetitors: Array.Empty<string>(),
            queryStrategy: null,
            windowStartUtc: null,
            windowEndUtc: null),
        createdUtc: DateTime.UtcNow);
}
