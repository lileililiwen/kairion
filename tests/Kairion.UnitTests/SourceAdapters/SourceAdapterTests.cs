using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Domain;
using Kairion.Infrastructure.Providers;

namespace Kairion.UnitTests.SourceAdapters;

/// <summary>
/// B2/D1–D4 oracle: URL normalization, payload validation, size/page caps,
/// and secret redaction for the source-adapter package.
/// </summary>
public class SourceAdapterTests
{
    [Theory]
    [InlineData("HTTPS://Example.TEST/Post#frag", "https://example.test/Post")]
    [InlineData("https://example.test/Post/", "https://example.test/Post")]
    [InlineData("  https://example.test/a?b=1#x  ", "https://example.test/a?b=1")]
    public void CanonicalUrl_Normalizes_Deterministically(string input, string expected)
    {
        CanonicalUrl.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.test/x")]
    [InlineData("")]
    public void CanonicalUrl_Rejects_Invalid(string input)
    {
        FluentActions.Invoking(() => CanonicalUrl.Normalize(input))
            .Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ProviderSettings_Rejects_OutOfRange_Limits()
    {
        FluentActions.Invoking(() => new SourceProviderSettings("hn", true, maxQueries: 0))
            .Should().Throw<DomainValidationException>();
        FluentActions.Invoking(() => new SourceProviderSettings("hn", true, maxQueries: 6))
            .Should().Throw<DomainValidationException>();
        FluentActions.Invoking(() => new SourceProviderSettings("hn", true, maxResultsPerQuery: 0))
            .Should().Throw<DomainValidationException>();
        FluentActions.Invoking(() => new SourceProviderSettings("hn", true, maxResultsPerQuery: 51))
            .Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ProviderSettings_Rejects_NonHttps_Endpoint()
    {
        FluentActions.Invoking(() => new SourceProviderSettings("web-search", true, endpoint: "http://example.test/search"))
            .Should().Throw<DomainValidationException>();
        var ok = new SourceProviderSettings("web-search", true, endpoint: "https://example.test/search", credentialRef: "websearch-key");
        ok.Endpoint.Should().Be("https://example.test/search");
        ok.CredentialRef.Should().Be("websearch-key");
    }

    [Fact]
    public void Limits_Bound_Queries_And_Results()
    {
        SourceProviderLimits.BoundResultsPerQuery(500).Should().Be(50);
        SourceProviderLimits.BoundResultsPerQuery(0).Should().Be(25);
        SourceProviderLimits.BoundQueries(99).Should().Be(5);
    }

    [Fact]
    public void Normalizer_Rejects_NonHttps_And_Secrets()
    {
        var now = DateTime.UtcNow;
        var http = NewResult("https://example.test/a".Replace("https://", "http://"));
        CandidateNormalizer.TryNormalize(http, out _, out var code).Should().BeFalse();
        code.Should().Be("non_https_url");

        var secret = NewResult("https://example.test/b", provenance: "{\"api_key\":\"sk-live\"}");
        CandidateNormalizer.TryNormalize(secret, out _, out var secretCode).Should().BeFalse();
        secretCode.Should().Be("oversized_provenance");

        var oversized = NewResult("https://example.test/c", provenance: new string('x', 9000));
        CandidateNormalizer.TryNormalize(oversized, out _, out var sizeCode).Should().BeFalse();
        sizeCode.Should().Be("oversized_provenance");
    }

    [Fact]
    public void Normalizer_Truncates_Title_And_Excerpt()
    {
        var candidate = NewResult(
            "https://example.test/d",
            title: new string('t', 600),
            excerpt: new string('e', 9000));
        CandidateNormalizer.TryNormalize(candidate, out var normalized, out _).Should().BeTrue();
        normalized!.Title!.Length.Should().Be(CandidateNormalizer.MaxTitleLength);
        normalized.Excerpt!.Length.Should().Be(CandidateNormalizer.MaxExcerptLength);
    }

    [Fact]
    public void Hn_ParseHits_Maps_Url_Title_Author_Published()
    {
        var body = """
            {"hits": [
              {"objectID": "1", "title": "Pain point", "url": "https://example.test/thread/1", "author": "pg", "created_at": "2026-09-01T10:00:00Z"},
              {"objectID": "2", "title": null, "url": null, "author": null, "created_at": "bad-date"},
              {"objectID": "", "title": "skip", "url": "https://example.test/skip"}
            ]}
            """;
        var parsed = HackerNewsSourceProvider.ParseHits(body, DateTime.UtcNow);
        parsed.Should().NotBeNull();
        parsed!.Should().HaveCount(2);
        parsed[0].CanonicalUrl.Should().Be("https://example.test/thread/1");
        parsed[0].AuthorHandle.Should().Be("pg");
        parsed[0].PublishedUtc.Should().NotBeNull();
        parsed[1].CanonicalUrl.Should().Contain("news.ycombinator.com/item?id=2");
    }

    [Fact]
    public void Hn_ParseHits_Rejects_Malformed()
    {
        HackerNewsSourceProvider.ParseHits("not json", DateTime.UtcNow).Should().BeNull();
        HackerNewsSourceProvider.ParseHits("{\"hits\":{}}", DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void WebSearch_ParseResults_Requires_Https_And_Id()
    {
        var body = """
            {"results": [
              {"url": "https://example.test/r1", "title": "R1", "snippet": "excerpt", "id": "r1"},
              {"url": "http://example.test/r2", "title": "R2", "id": "r2"},
              {"title": "no-url"}
            ]}
            """;
        var parsed = ConfiguredWebSearchProvider.ParseResults(body, DateTime.UtcNow);
        parsed.Should().NotBeNull();
        parsed!.Should().HaveCount(1);
        parsed[0].CanonicalUrl.Should().Be("https://example.test/r1");
    }

    [Fact]
    public void WebSearch_ParseResults_Rejects_Malformed()
    {
        ConfiguredWebSearchProvider.ParseResults("nope", DateTime.UtcNow).Should().BeNull();
        ConfiguredWebSearchProvider.ParseResults("{\"other\":[]}", DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void Hn_BuildQueries_Bounded_To_Five()
    {
        var query = new SourceQuery(
            Guid.NewGuid(), "base",
            new[] { "t1", "t2", "t3", "t4", "t5", "t6" },
            Array.Empty<string>(), null, null, 10, 99);
        query.MaxQueries.Should().Be(5);
        HackerNewsSourceProvider.BuildQueries(query).Should().HaveCountLessThanOrEqualTo(5);
    }

    private static SourceFetchResult NewResult(
        string url, string? title = "T", string? excerpt = "E", string? provenance = null)
    {
        var now = DateTime.UtcNow;
        return new SourceFetchResult(
            "fake-source", Guid.NewGuid().ToString("N"), url, title, excerpt,
            now.AddDays(-1), now,
            provenance ?? "{\"source\":\"fake\"}",
            new ProviderObservation("fake-source", ProviderStatus.Available, null, null, now, 200));
    }
}
