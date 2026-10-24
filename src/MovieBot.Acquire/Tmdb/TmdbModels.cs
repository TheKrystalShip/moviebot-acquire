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
    [JsonPropertyName("external_ids")] public TmdbExternalIds? ExternalIds { get; init; }
    [JsonPropertyName("keywords")] public TmdbKeywords? Keywords { get; init; }
    [JsonPropertyName("videos")] public TmdbVideos? Videos { get; init; }
    [JsonPropertyName("recommendations")] public TmdbTitlePage? Recommendations { get; init; }
    [JsonPropertyName("reviews")] public TmdbReviews? Reviews { get; init; }
    [JsonPropertyName("images")] public TmdbImages? Images { get; init; }
    [JsonPropertyName("watch/providers")] public TmdbWatchProviders? WatchProviders { get; init; }
}

/// <summary>A title's keywords: a film lists them under <c>keywords</c>, a show under <c>results</c>.</summary>
public sealed record TmdbKeywords
{
    [JsonPropertyName("keywords")] public IReadOnlyList<TmdbNamed>? Keywords { get; init; }
    [JsonPropertyName("results")] public IReadOnlyList<TmdbNamed>? Results { get; init; }
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
    [JsonPropertyName("total_pages")] public int TotalPages { get; init; }
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
    [JsonPropertyName("department")] public string? Department { get; init; }
    [JsonPropertyName("original_language")] public string? OriginalLanguage { get; init; }
}

/// <summary>A collection: the films of one series, as the database groups them.</summary>
public sealed record TmdbCollection
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("parts")] public IReadOnlyList<TmdbListedTitle>? Parts { get; init; }
}

/// <summary>A person a name search found, with the titles the database knows them for.</summary>
public sealed record TmdbPersonHit
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("known_for_department")] public string? KnownForDepartment { get; init; }
    [JsonPropertyName("profile_path")] public string? ProfilePath { get; init; }
    [JsonPropertyName("popularity")] public double? Popularity { get; init; }
    [JsonPropertyName("known_for")] public IReadOnlyList<TmdbListedTitle>? KnownFor { get; init; }
}

public sealed record TmdbPersonPage
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbPersonHit>? Results { get; init; }
}

/// <summary>A page of genres, keywords or the like: each an id and a name.</summary>
public sealed record TmdbNamedPage
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbNamed>? Results { get; init; }
}

public sealed record TmdbConfigLanguage
{
    [JsonPropertyName("iso_639_1")] public string? Code { get; init; }
    [JsonPropertyName("english_name")] public string? EnglishName { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
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
    [JsonPropertyName("keywords")] public TmdbKeywords? Keywords { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("budget")] public long? Budget { get; init; }
    [JsonPropertyName("revenue")] public long? Revenue { get; init; }
    [JsonPropertyName("videos")] public TmdbVideos? Videos { get; init; }
    [JsonPropertyName("recommendations")] public TmdbTitlePage? Recommendations { get; init; }
    [JsonPropertyName("reviews")] public TmdbReviews? Reviews { get; init; }
    [JsonPropertyName("images")] public TmdbImages? Images { get; init; }
    [JsonPropertyName("watch/providers")] public TmdbWatchProviders? WatchProviders { get; init; }
}

public sealed record TmdbVideos
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbVideo>? Results { get; init; }
}

public sealed record TmdbVideo
{
    [JsonPropertyName("key")] public string? Key { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("site")] public string? Site { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("official")] public bool? Official { get; init; }
    [JsonPropertyName("iso_639_1")] public string? Language { get; init; }
    [JsonPropertyName("published_at")] public string? PublishedAt { get; init; }
}

public sealed record TmdbReviews
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbReview>? Results { get; init; }
    [JsonPropertyName("total_results")] public int? TotalResults { get; init; }
}

public sealed record TmdbReview
{
    [JsonPropertyName("author")] public string? Author { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("author_details")] public TmdbReviewer? AuthorDetails { get; init; }
}

public sealed record TmdbReviewer
{
    [JsonPropertyName("rating")] public double? Rating { get; init; }
}

public sealed record TmdbImages
{
    [JsonPropertyName("logos")] public IReadOnlyList<TmdbImage>? Logos { get; init; }
}

public sealed record TmdbImage
{
    [JsonPropertyName("file_path")] public string? FilePath { get; init; }
    [JsonPropertyName("iso_639_1")] public string? Language { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("width")] public int? Width { get; init; }
}

/// <summary>Where a title streams, rents and sells, by region code.</summary>
public sealed record TmdbWatchProviders
{
    [JsonPropertyName("results")] public Dictionary<string, TmdbRegionProviders>? Results { get; init; }
}

public sealed record TmdbRegionProviders
{
    [JsonPropertyName("link")] public string? Link { get; init; }
    [JsonPropertyName("flatrate")] public IReadOnlyList<TmdbProvider>? Flatrate { get; init; }
    [JsonPropertyName("rent")] public IReadOnlyList<TmdbProvider>? Rent { get; init; }
    [JsonPropertyName("buy")] public IReadOnlyList<TmdbProvider>? Buy { get; init; }
    [JsonPropertyName("free")] public IReadOnlyList<TmdbProvider>? Free { get; init; }
    [JsonPropertyName("ads")] public IReadOnlyList<TmdbProvider>? Ads { get; init; }
}

public sealed record TmdbProvider
{
    [JsonPropertyName("provider_id")] public int Id { get; init; }
    [JsonPropertyName("provider_name")] public string? Name { get; init; }
    [JsonPropertyName("logo_path")] public string? LogoPath { get; init; }
    [JsonPropertyName("display_priority")] public int? Priority { get; init; }
}

/// <summary>The providers the database lists for a region, with how it orders them there.</summary>
public sealed record TmdbProviderList
{
    [JsonPropertyName("results")] public IReadOnlyList<TmdbListedProvider>? Results { get; init; }
}

public sealed record TmdbListedProvider
{
    [JsonPropertyName("provider_id")] public int Id { get; init; }
    [JsonPropertyName("provider_name")] public string? Name { get; init; }
    [JsonPropertyName("logo_path")] public string? LogoPath { get; init; }
    [JsonPropertyName("display_priorities")] public Dictionary<string, int>? Priorities { get; init; }
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
[JsonSerializable(typeof(TmdbCollection))]
[JsonSerializable(typeof(TmdbExternalIds))]
[JsonSerializable(typeof(TmdbPerson))]
[JsonSerializable(typeof(TmdbGenreList))]
[JsonSerializable(typeof(TmdbPersonPage))]
[JsonSerializable(typeof(TmdbNamedPage))]
[JsonSerializable(typeof(TmdbProviderList))]
[JsonSerializable(typeof(List<TmdbConfigLanguage>))]
public partial class TmdbJsonContext : JsonSerializerContext;
