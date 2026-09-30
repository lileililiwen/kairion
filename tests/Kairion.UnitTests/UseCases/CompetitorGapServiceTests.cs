using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Application.Trends;
using Kairion.Application.UseCases;
using Kairion.Domain;
using NSubstitute;
using Xunit;

namespace Kairion.UnitTests.UseCases;

/// <summary>
/// Deterministic matrix tests over a fixed UTC fixture. Every assertion uses
/// known competitor/cluster assignments and fixed timestamps so the exact
/// counts, windows, deltas, low-sample state and unmapped bucket are verified.
/// </summary>
public class CompetitorGapServiceTests
{
    private static readonly DateTime AsOf = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ComputeAsync_WithAssignedItems_ReturnsExactCountsAndEvidenceLinks()
    {
        var projectId = Guid.NewGuid();
        var clusterA = new PainCluster(Guid.NewGuid(), projectId, "Onboarding pain", "onboarding", "summary", 1, AsOf.AddDays(-60));
        var clusterB = new PainCluster(Guid.NewGuid(), projectId, "Pricing pain", "pricing", "summary", 1, AsOf.AddDays(-60));
        var compAcme = new Competitor(Guid.NewGuid(), projectId, "Acme", AsOf.AddDays(-60));
        var compBeta = new Competitor(Guid.NewGuid(), projectId, "Beta", AsOf.AddDays(-60));

        // 3 current-window items for Acme x clusterA (above the limited threshold),
        // 1 current item for Beta x clusterB, plus previous-window items for deltas.
        var acmeCurrent = MakeItems(projectId, 3, AsOf.AddDays(-5));
        var betaCurrent = MakeItems(projectId, 1, AsOf.AddDays(-4));
        var acmePrevious = MakeItems(projectId, 1, AsOf.AddDays(-40));
        var all = acmeCurrent.Concat(betaCurrent).Concat(acmePrevious).ToList();

        var harness = BuildHarness(projectId, new[] { "Acme", "Beta" },
            new[] { clusterA, clusterB }, new[] { compAcme, compBeta }, all);

        harness.ClusterLinks[clusterA.Id] = acmeCurrent.Concat(acmePrevious).Select(s => s.Id).ToList();
        harness.ClusterLinks[clusterB.Id] = betaCurrent.Select(s => s.Id).ToList();
        foreach (var s in acmeCurrent.Concat(acmePrevious)) harness.CompetitorLinks[s.Id] = new HashSet<Guid> { compAcme.Id };
        foreach (var s in betaCurrent) harness.CompetitorLinks[s.Id] = new HashSet<Guid> { compBeta.Id };
        foreach (var s in all) harness.Confidences[s.Id] = 0.8m;

        var response = (await harness.Service.ComputeAsync(projectId, TrendWindow.Days30, null, CancellationToken.None))!;

        response.Window.Should().Be("30d");
        response.WindowStartUtc.Should().Be(AsOf.AddDays(-30));
        response.PreviousWindowStartUtc.Should().Be(AsOf.AddDays(-60));
        response.Coverage.TotalEvidenceInWindow.Should().Be(4);
        response.Coverage.MappedEvidenceInWindow.Should().Be(4);
        response.Coverage.UnmappedEvidenceInWindow.Should().Be(0);

        var acmeCell = response.Cells.Single(c => c.CompetitorId == compAcme.Id && c.ClusterId == clusterA.Id);
        acmeCell.EvidenceCount.Should().Be(3);
        acmeCell.SourceCount.Should().Be(3);
        acmeCell.PreviousCount.Should().Be(1);
        acmeCell.Delta.Should().Be(2);
        acmeCell.PercentChange.Should().Be(2.0m);
        acmeCell.LimitedEvidence.Should().BeFalse();
        acmeCell.Classification.Should().Be("Emerging");
        acmeCell.ConfidenceMean.Should().Be(0.8m);
        acmeCell.ConfidenceSampleCount.Should().Be(3);
        acmeCell.RepresentativeEvidence.Should().HaveCount(3);
        acmeCell.RepresentativeEvidence.All(r => !string.IsNullOrWhiteSpace(r.CanonicalUrl)).Should().BeTrue();
        acmeCell.FirstObservedUtc.Should().NotBeNull();
        acmeCell.LastObservedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ComputeAsync_UnassignedItems_AppearAsUnmappedAndNeverAttributed()
    {
        var projectId = Guid.NewGuid();
        var cluster = new PainCluster(Guid.NewGuid(), projectId, "Label", "cat", "summary", 1, AsOf.AddDays(-60));
        var comp = new Competitor(Guid.NewGuid(), projectId, "Acme", AsOf.AddDays(-60));
        var mapped = MakeItems(projectId, 1, AsOf.AddDays(-5));
        var unmapped = MakeItems(projectId, 2, AsOf.AddDays(-6));
        var all = mapped.Concat(unmapped).ToList();

        var harness = BuildHarness(projectId, new[] { "Acme" }, new[] { cluster }, new[] { comp }, all);
        harness.ClusterLinks[cluster.Id] = all.Select(s => s.Id).ToList();
        foreach (var s in mapped) harness.CompetitorLinks[s.Id] = new HashSet<Guid> { comp.Id };

        var response = (await harness.Service.ComputeAsync(projectId, TrendWindow.Days30, null, CancellationToken.None))!;

        var mappedCell = response.Cells.Single(c => c.CompetitorId == comp.Id && c.ClusterId == cluster.Id);
        mappedCell.EvidenceCount.Should().Be(1);
        mappedCell.LimitedEvidence.Should().BeTrue();
        mappedCell.Classification.Should().Be("InsufficientData");

        var unmappedCell = response.Cells.Single(c => c.CompetitorId == null && c.ClusterId == cluster.Id);
        unmappedCell.CompetitorName.Should().Be("Unmapped");
        unmappedCell.EvidenceCount.Should().Be(2);
        response.Coverage.UnmappedEvidenceInWindow.Should().Be(2);
        response.Coverage.MappedEvidenceInWindow.Should().Be(1);
    }

    [Fact]
    public async Task ComputeAsync_AbsentPreviousWindow_ReturnsNullDeltaAndInsufficientData()
    {
        var projectId = Guid.NewGuid();
        var cluster = new PainCluster(Guid.NewGuid(), projectId, "Label", "cat", "summary", 1, AsOf.AddDays(-60));
        var comp = new Competitor(Guid.NewGuid(), projectId, "Acme", AsOf.AddDays(-60));
        var items = MakeItems(projectId, 4, AsOf.AddDays(-3));

        var harness = BuildHarness(projectId, new[] { "Acme" }, new[] { cluster }, new[] { comp }, items);
        harness.ClusterLinks[cluster.Id] = items.Select(s => s.Id).ToList();
        foreach (var s in items) harness.CompetitorLinks[s.Id] = new HashSet<Guid> { comp.Id };

        var response = (await harness.Service.ComputeAsync(projectId, TrendWindow.Days30, null, CancellationToken.None))!;
        var cell = response.Cells.Single(c => c.CompetitorId == comp.Id && c.ClusterId == cluster.Id);
        cell.EvidenceCount.Should().Be(4);
        cell.PreviousCount.Should().Be(0);
        cell.Delta.Should().BeNull();
        cell.PercentChange.Should().BeNull();
        cell.Classification.Should().Be("InsufficientData");
        cell.LimitedEvidence.Should().BeFalse();
    }

    [Fact]
    public async Task ComputeAsync_ForeignCompetitorFilter_ThrowsValidation()
    {
        var projectId = Guid.NewGuid();
        var cluster = new PainCluster(Guid.NewGuid(), projectId, "Label", "cat", "summary", 1, AsOf.AddDays(-60));
        var comp = new Competitor(Guid.NewGuid(), projectId, "Acme", AsOf.AddDays(-60));
        var harness = BuildHarness(projectId, new[] { "Acme" }, new[] { cluster }, new[] { comp }, new List<SourceItem>());

        var act = () => harness.Service.ComputeAsync(projectId, TrendWindow.Days30, new[] { Guid.NewGuid() }, CancellationToken.None);
        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ComputeAsync_ArchivedProject_ReturnsNull()
    {
        var projectId = Guid.NewGuid();
        var projects = Substitute.For<IResearchProjectRepository>();
        var project = new ResearchProject(projectId, "t", BriefKind.Market, "brief",
            Array.Empty<string>(), new SourceConfiguration(Array.Empty<string>(), new[] { "Acme" }, null, null, null), AsOf.AddDays(-60));
        project.Archive(AsOf.AddDays(-1));
        projects.FindAsync(projectId, Arg.Any<CancellationToken>()).Returns(project);

        var harness = BuildHarness(projectId, new[] { "Acme" }, Array.Empty<PainCluster>(), Array.Empty<Competitor>(), new List<SourceItem>(), projects);
        (await harness.Service.ComputeAsync(projectId, TrendWindow.Days30, null, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task AssignAsync_IsIdempotent_AndUnassign_Removes()
    {
        var projectId = Guid.NewGuid();
        var comp = new Competitor(Guid.NewGuid(), projectId, "Acme", AsOf);
        var item = MakeItems(projectId, 1, AsOf.AddDays(-2)).Single();
        var harness = BuildHarness(projectId, new[] { "Acme" }, Array.Empty<PainCluster>(), new[] { comp }, new[] { item });

        var first = await harness.Service.AssignAsync(projectId, item.Id, comp.Id, AssignmentOrigin.Human, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        var second = await harness.Service.AssignAsync(projectId, item.Id, comp.Id, AssignmentOrigin.Human, CancellationToken.None);
        second.IsSuccess.Should().BeTrue();
        harness.AddedLinks.Count.Should().Be(1);

        var unassign = await harness.Service.UnassignAsync(projectId, item.Id, comp.Id, CancellationToken.None);
        unassign.IsSuccess.Should().BeTrue();
        unassign.Value.Should().BeTrue();
    }

    private static List<SourceItem> MakeItems(Guid projectId, int count, DateTime observedUtc)
    {
        var list = new List<SourceItem>();
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            list.Add(new SourceItem(
                id, projectId, "manual", $"ext-{id:N}", $"https://example.test/{id:N}",
                $"title {i}", "excerpt", publishedUtc: null, observedUtc.AddHours(i),
                "{}", new ProviderObservation("manual", ProviderStatus.Available, null, null, observedUtc.AddHours(i), null)));
        }
        return list;
    }

    private sealed class Harness
    {
        public CompetitorGapService Service { get; set; } = null!;
        public Dictionary<Guid, List<Guid>> ClusterLinks { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> CompetitorLinks { get; } = new();
        public Dictionary<Guid, decimal> Confidences { get; } = new();
        public List<SourceItemCompetitorAssignment> AddedLinks { get; } = new();
    }

    private static Harness BuildHarness(
        Guid projectId,
        string[] competitorNames,
        IReadOnlyList<PainCluster> clusters,
        IReadOnlyList<Competitor> competitors,
        IReadOnlyList<SourceItem> sources,
        IResearchProjectRepository? projectsOverride = null)
    {
        var harness = new Harness();
        var projects = projectsOverride ?? Substitute.For<IResearchProjectRepository>();
        if (projectsOverride is null)
        {
            var project = new ResearchProject(projectId, "t", BriefKind.Market, "brief",
                Array.Empty<string>(), new SourceConfiguration(Array.Empty<string>(), competitorNames, null, null, null), AsOf.AddDays(-60));
            projects.FindAsync(projectId, Arg.Any<CancellationToken>()).Returns(project);
        }
        var clusterRepo = Substitute.For<IPainClusterRepository>();
        clusterRepo.ListForProjectAsync(projectId, Arg.Any<CancellationToken>()).Returns(clusters.ToList());
        foreach (var c in clusters)
        {
            clusterRepo.FindAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);
        }
        var assignmentRepo = Substitute.For<IClusterAssignmentRepository>();
        assignmentRepo.ListForClusterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var clusterId = call.Arg<Guid>();
                if (!harness.ClusterLinks.TryGetValue(clusterId, out var ids)) return new List<ClusterAssignment>();
                return ids.Select(sid => new ClusterAssignment(Guid.NewGuid(), clusterId, sid, null, AssignmentOrigin.Human, AsOf.AddDays(-1))).ToList();
            });
        var sourceRepo = Substitute.For<ISourceItemRepository>();
        sourceRepo.ListForProjectAsync(projectId, Arg.Any<CancellationToken>()).Returns(sources.ToList());
        foreach (var s in sources)
        {
            var captured = s;
            sourceRepo.FindAsync(captured.Id, Arg.Any<CancellationToken>()).Returns(captured);
        }
        var analysisRepo = Substitute.For<IDeepAnalysisRepository>();
        analysisRepo.LatestForSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var sid = call.Arg<Guid>();
                if (!harness.Confidences.TryGetValue(sid, out var confidence)) return null;
                return new DeepAnalysis(Guid.NewGuid(), sid, "v1", "problem", "context", "current",
                    "dissat", "work", "outcome", "cat", null, 0.5m, confidence,
                    "demo", "demo-v1", AnalysisStatus.Completed, AsOf);
            });
        var competitorRepo = Substitute.For<ICompetitorRepository>();
        competitorRepo.ListForProjectAsync(projectId, Arg.Any<CancellationToken>()).Returns(_ => competitors.ToList());
        foreach (var c in competitors)
        {
            var captured = c;
            competitorRepo.FindAsync(captured.Id, Arg.Any<CancellationToken>()).Returns(captured);
        }
        competitorRepo.AddAsync(Arg.Any<Competitor>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var linkRepo = Substitute.For<ISourceItemCompetitorRepository>();
        linkRepo.ListForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(_ => harness.CompetitorLinks
                .SelectMany(kv => kv.Value.Select(cid => new SourceItemCompetitorAssignment(Guid.NewGuid(), projectId, kv.Key, cid, AssignmentOrigin.Human, AsOf)))
                .ToList());
        linkRepo.ListForSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var sid = call.Arg<Guid>();
                var existing = harness.CompetitorLinks.TryGetValue(sid, out var set)
                    ? set.Select(cid => new SourceItemCompetitorAssignment(Guid.NewGuid(), projectId, sid, cid, AssignmentOrigin.Human, AsOf)).ToList()
                    : new List<SourceItemCompetitorAssignment>();
                return existing.Concat(harness.AddedLinks.Where(a => a.SourceItemId == sid)).ToList();
            });
        linkRepo.AddAsync(Arg.Any<SourceItemCompetitorAssignment>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var link = call.Arg<SourceItemCompetitorAssignment>();
                harness.AddedLinks.Add(link);
                if (!harness.CompetitorLinks.TryGetValue(link.SourceItemId, out var set))
                {
                    set = new HashSet<Guid>();
                    harness.CompetitorLinks[link.SourceItemId] = set;
                }
                set.Add(link.CompetitorId);
                return Task.CompletedTask;
            });
        linkRepo.When(r => r.Remove(Arg.Any<SourceItemCompetitorAssignment>()))
            .Do(call =>
            {
                var link = call.Arg<SourceItemCompetitorAssignment>();
                harness.AddedLinks.RemoveAll(a => a.SourceItemId == link.SourceItemId && a.CompetitorId == link.CompetitorId);
                if (harness.CompetitorLinks.TryGetValue(link.SourceItemId, out var set)) set.Remove(link.CompetitorId);
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(AsOf);
        var observations = Substitute.For<IObservationReadService>();
        var trends = new TrendService(observations, clock);
        harness.Service = new CompetitorGapService(projects, clusterRepo, assignmentRepo, sourceRepo,
            analysisRepo, competitorRepo, linkRepo, unitOfWork, clock, trends);
        return harness;
    }
}
