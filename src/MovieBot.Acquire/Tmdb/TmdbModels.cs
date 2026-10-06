using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

// What the database sends, read field by field as nullable: a film it covers thinly leaves fields
// null or out, and one unreadable field must not cost the whole description.

public sealed record TmdbFindResponse
{
    [JsonPropertyName("movie_results")] public IReadOnlyList<TmdbFound>? MovieResults { get; init; }
    [JsonPropertyName("tv_results")] public IReadOnlyList<TmdbFound>? TvResults { get; init; }
}

public sealed record TmdbTv
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("original_name")] public string? OriginalName { get; init; }
    [JsonPropertyName("original_language")] public string? OriginalLanguage { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("tagline")] public string? Tagline { get; init; }
    [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; init; }
    [JsonPropertyName("last_air_date")] public string? LastAirDate { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("in_production")] public bool? InProduction { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
    [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; init; }
    [JsonPropertyName("genres")] public IReadOnlyList<TmdbNamed>? Genres { get; init; }
    [JsonPropertyName("spoken_languages")] public IReadOnlyList<TmdbLanguage>? SpokenLanguages { get; init; }
    [JsonPropertyName("production_countries")] public IReadOnlyList<TmdbNamed>? ProductionCountries { get; init; }
    [JsonPropertyName("networks")] public IReadOnlyList<TmdbNamed>? Networks { get; init; }
    [JsonPropertyName("created_by")] public IReadOnlyList<TmdbNamed>? CreatedBy { get; init; }
    [JsonPropertyName("seasons")] public IReadOnlyList<TmdbTvSeason>? Seasons { get; init; }
    [JsonPropertyName("next_episode_to_air")] public TmdbEpisode? NextEpisodeToAir { get; init; }
    [JsonPropertyName("last_episode_to_air")] public TmdbEpisode? LastEpisodeToAir { get; init; }
    [JsonPropertyName("aggregate_credits")] public TmdbAggregateCredits? AggregateCredits { get; init; }
    [JsonPropertyName("content_ratings")] public TmdbContentRatings? ContentRatings { get; init; }
}

public sealed record TmdbTvSeason
{
    [JsonPropertyName("season_number")] public int SeasonNumber { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("air_date")] public string? AirDate { get; init; }
    [JsonPropertyName("episode_count")] public int? EpisodeCount { get; init; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
    [JsonPropertyName("episodes")] public IReadOnlyList<TmdbEpisode>? Episodes { get; init; }
}

public sealed record TmdbEpisode
{
    [JsonPropertyName("season_number")] public int SeasonNumber { get; init; }
    [JsonPropertyName("episode_number")] public int EpisodeNumber { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("air_date")] public string? AirDate { get; init; }
    [JsonPropertyName("runtime")] public int? Runtime { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
    [JsonPropertyName("still_path")] public string? StillPath { get; init; }
}

public sealed record TmdbAggregateCredits
{
    [JsonPropertyName("cast")] public IReadOnlyList<TmdbAggregateCast>? Cast { get; init; }
}

public sealed record TmdbAggregateCast
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("order")] public int? Order { get; init; }
    [JsonPropertyName("profile_path")] public string? ProfilePath { get; init; }
    [JsonPropertyName("roles")] public IReadOnlyList<TmdbRole>? Roles { get; init; }
}

public sealed record TmdbRole
{
    [JsonPropertyName("character")] public string? Character { get; init; }
    [JsonPropertyName("episode_count")] public int? EpisodeCount { get; init; }
}

public sealed record TmdbContentRatings
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbContentRating>? Results { get; init; }
}

public sealed record TmdbContentRating
{
    [JsonPropertyName("iso_3166_1")] public string? Country { get; init; }
    [JsonPropertyName("rating")] public string? Rating { get; init; }
}

public sealed record TmdbFound
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; init; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
}

/// <summary>One page of a list of titles: the week's trending, or a genre's catalogue.</summary>
public sealed record TmdbTitlePage
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbListedTitle>? Results { get; init; }
}

/// <summary>
/// A title as a list or a person's credits names it. A film carries <c>title</c> and
/// <c>release_date</c>, a show <c>name</c> and <c>first_air_date</c>; a credit also carries
/// <c>media_type</c> and the part played (<c>character</c>) or the job done (<c>job</c>).
/// </summary>
public sealed record TmdbListedTitle
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
    [JsonPropertyName("popularity")] public double? Popularity { get; init; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
    [JsonPropertyName("adult")] public bool? Adult { get; init; }
    [JsonPropertyName("genre_ids")] public IReadOnlyList<int>? GenreIds { get; init; }
    [JsonPropertyName("media_type")] public string? MediaType { get; init; }
    [JsonPropertyName("character")] public string? Character { get; init; }
    [JsonPropertyName("job")] public string? Job { get; init; }
}

public sealed record TmdbPerson
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("known_for_department")] public string? KnownForDepartment { get; init; }
    [JsonPropertyName("profile_path")] public string? ProfilePath { get; init; }
    [JsonPropertyName("combined_credits")] public TmdbCombinedCredits? CombinedCredits { get; init; }
}

public sealed record TmdbCombinedCredits
{
    [JsonPropertyName("cast")] public IReadOnlyList<TmdbListedTitle>? Cast { get; init; }
    [JsonPropertyName("crew")] public IReadOnlyList<TmdbListedTitle>? Crew { get; init; }
}

public sealed record TmdbGenreList
{
    [JsonPropertyName("genres")] public IReadOnlyList<TmdbNamed>? Genres { get; init; }
}

public sealed record TmdbExternalIds
{
    [JsonPropertyName("imdb_id")] public string? ImdbId { get; init; }
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

/// <summary>A genre, a network or a person by name; <c>id</c> is the database's where it gives one.</summary>
public sealed record TmdbNamed
{
    [JsonPropertyName("id")] public int Id { get; init; }
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
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("character")] public string? Character { get; init; }
    [JsonPropertyName("order")] public int? Order { get; init; }
    [JsonPropertyName("profile_path")] public string? ProfilePath { get; init; }
}

public sealed record TmdbCrew
{
    [JsonPropertyName("id")] public int Id { get; init; }
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
[JsonSerializable(typeof(TmdbTv))]
[JsonSerializable(typeof(TmdbTvSeason))]
[JsonSerializable(typeof(TmdbTitlePage))]
[JsonSerializable(typeof(TmdbExternalIds))]
[JsonSerializable(typeof(TmdbPerson))]
[JsonSerializable(typeof(TmdbGenreList))]
public partial class TmdbJsonContext : JsonSerializerContext;
