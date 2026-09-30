using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kairion.Api.Errors;

/// <summary>
/// Shared JSON serialization options for the API. The global <c>DomainExceptionHandler</c>
/// and the MVC pipeline both use these settings so an <c>application/problem+json</c>
/// error response and a successful DTO response share the same camelCase property
/// names, never skip null fields, and serialize ProblemDetails <c>Extensions</c>
/// (<c>code</c>, <c>traceId</c>) consistently with the rest of the API.
/// </summary>
public static class KairionJsonOptions
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        // Keep null fields visible so the web app and external clients can rely
        // on the presence of every property — particularly the optional
        // `percentGrowth` / `previousCount` fields on trend and opportunity
        // signal responses and the `code` extension on ProblemDetails.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}
