using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Imdb;

/// <summary>
/// What the suggestion endpoint answers with. Every name is one letter long because it is the
/// index a search box reads while somebody types, and it was never meant to be read by a person.
/// </summary>
public sealed record ImdbSuggestResponse
{
    [JsonPropertyName("d")] public IReadOnlyList<ImdbSuggestion>? Results { get; init; }
}

public sealed record ImdbSuggestion
{
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>The label: the film's name.</summary>
    [JsonPropertyName("l")] public string? Label { get; init; }

    [JsonPropertyName("y")] public int? Year { get; init; }

    /// <summary>The subtitle line: top-billed cast for a film, the series for an episode.</summary>
    [JsonPropertyName("s")] public string? Subtext { get; init; }

    /// <summary>What kind of entry this is: <c>movie</c>, <c>tvSeries</c>, <c>tvEpisode</c>.</summary>
    [JsonPropertyName("qid")] public string? Kind { get; init; }

    [JsonPropertyName("i")] public ImdbSuggestionImage? Image { get; init; }
}

public sealed record ImdbSuggestionImage
{
    [JsonPropertyName("imageUrl")] public string? Url { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ImdbSuggestResponse))]
internal partial class ImdbJsonContext : JsonSerializerContext;
