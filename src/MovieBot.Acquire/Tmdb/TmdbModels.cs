using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

// What the database sends, read field by field as nullable: a film it covers thinly leaves fields
// null or out, and one unreadable field must not cost the whole description.

public sealed record TmdbFindResponse
{
    [JsonPropertyName("movie_results")] public IReadOnlyList<TmdbFound>? MovieResults { get; init; }
}

public sealed record TmdbFound
{
    [JsonPropertyName("id")] public int Id { get; init; }
}

public sealed record TmdbMovie
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("imdb_id")] public string? ImdbId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("original_title")] public string? OriginalTitle { get; init; }
    [JsonPropertyName("original_language")] public string? OriginalLanguage { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("tagline")] public string? Tagline { get; init; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("runtime")] public int? Runtime { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
    [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; init; }
    [JsonPropertyName("genres")] public IReadOnlyList<TmdbNamed>? Genres { get; init; }
    [JsonPropertyName("spoken_languages")] public IReadOnlyList<TmdbLanguage>? SpokenLanguages { get; init; }
    [JsonPropertyName("production_countries")] public IReadOnlyList<TmdbNamed>? ProductionCountries { get; init; }
    [JsonPropertyName("credits")] public TmdbCredits? Credits { get; init; }
    [JsonPropertyName("release_dates")] public TmdbReleaseDates? ReleaseDates { get; init; }
}

public sealed record TmdbNamed
{
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public sealed record TmdbLanguage
{
    [JsonPropertyName("english_name")] public string? EnglishName { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public sealed record TmdbCredits
{
    [JsonPropertyName("cast")] public IReadOnlyList<TmdbCast>? Cast { get; init; }
    [JsonPropertyName("crew")] public IReadOnlyList<TmdbCrew>? Crew { get; init; }
}

public sealed record TmdbCast
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("character")] public string? Character { get; init; }
    [JsonPropertyName("order")] public int? Order { get; init; }
    [JsonPropertyName("profile_path")] public string? ProfilePath { get; init; }
}

public sealed record TmdbCrew
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("job")] public string? Job { get; init; }
    [JsonPropertyName("department")] public string? Department { get; init; }
}

public sealed record TmdbReleaseDates
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbCountryReleases>? Results { get; init; }
}

public sealed record TmdbCountryReleases
{
    [JsonPropertyName("iso_3166_1")] public string? Country { get; init; }
    [JsonPropertyName("release_dates")] public IReadOnlyList<TmdbRelease>? Releases { get; init; }
}

public sealed record TmdbRelease
{
    [JsonPropertyName("certification")] public string? Certification { get; init; }

    /// <summary>1 premiere, 2 limited theatrical, 3 theatrical, 4 digital, 5 physical, 6 TV.</summary>
    [JsonPropertyName("type")] public int? Type { get; init; }
}

[JsonSerializable(typeof(TmdbFindResponse))]
[JsonSerializable(typeof(TmdbMovie))]
public partial class TmdbJsonContext : JsonSerializerContext;
