namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A show as the database describes it: what a page about the show shows above its seasons. Every
/// field but the ids and the name may be absent, as for a film.
/// </summary>
public sealed record ShowDetails
{
    public required int TmdbId { get; init; }

    /// <summary>The show's own IMDb id, which is what the tracker files every season under.</summary>
    public required string ImdbId { get; init; }

    public required string Title { get; init; }

    /// <summary>The name in its own language, when that differs.</summary>
    public string? OriginalTitle { get; init; }

    /// <summary>The year the first episode aired.</summary>
    public int? FirstYear { get; init; }

    /// <summary>The year the latest episode aired.</summary>
    public int? LastYear { get; init; }

    /// <summary>The database's word for where the show stands: <c>Returning Series</c>, <c>Ended</c>…</summary>
    public string? Status { get; init; }

    /// <summary>Whether more episodes are being made.</summary>
    public bool InProduction { get; init; }

    /// <summary>The rating in the configured region, such as <c>TV-MA</c>.</summary>
    public string? Certification { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public string? Overview { get; init; }

    public string? Tagline { get; init; }

    /// <summary>
    /// The database's keywords for the show. Null in a description kept before the database was
    /// asked for them; empty when it names none.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>Null when nobody has voted: a rating with no votes behind it is a placeholder.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public IReadOnlyList<string> Creators { get; init; } = [];

    /// <summary>Each creator's id in the database, by the name they are listed under.</summary>
    public IReadOnlyDictionary<string, int> PersonIds { get; init; } = new Dictionary<string, int>();

    /// <summary>The cast across every season, billed in the database's order, with their main role.</summary>
    public IReadOnlyList<CastMember> Cast { get; init; } = [];

    public string? OriginalLanguage { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public IReadOnlyList<string> Countries { get; init; } = [];

    public IReadOnlyList<string> Networks { get; init; } = [];

    /// <summary>The seasons, in order; season 0 is the database's home for specials.</summary>
    public IReadOnlyList<ShowSeason> Seasons { get; init; } = [];

    /// <summary>The episode the database expects to air next; null when it names none.</summary>
    public AiringEpisode? NextEpisodeToAir { get; init; }

    /// <summary>The latest episode the database says has aired; null when it names none.</summary>
    public AiringEpisode? LastEpisodeToAir { get; init; }

    public string? PosterPath { get; init; }

    public string? BackdropPath { get; init; }

    /// <summary>
    /// The keywords with the ids a discovery filters on. Null in a description kept before the
    /// database was asked for them.
    /// </summary>
    public IReadOnlyList<KeywordTag>? KeywordTags { get; init; }

    /// <summary>
    /// The trailers and teasers on YouTube, the best first, at most <see cref="TmdbClient.TrailersKept"/>.
    /// Null in a description kept before the database was asked for them; empty when it names none.
    /// </summary>
    public IReadOnlyList<Trailer>? Trailers { get; init; }

    /// <summary>The database's first page of recommendations, in its order.</summary>
    public IReadOnlyList<ListedTitle> Recommendations { get; init; } = [];

    /// <summary>The first reviews the database holds, at most <see cref="TmdbClient.ReviewsKept"/>.</summary>
    public IReadOnlyList<Review> Reviews { get; init; } = [];

    /// <summary>How many reviews the database holds.</summary>
    public int ReviewCount { get; init; }

    /// <summary>The title set as a logo, a transparent PNG's path in the database's image store; null when it has none.</summary>
    public string? LogoPath { get; init; }
}

/// <summary>An episode as the show's own description names it: where it falls and when it airs.</summary>
public sealed record AiringEpisode
{
    public required int Season { get; init; }

    public required int Number { get; init; }

    public string? Name { get; init; }

    /// <summary>The date it airs, <c>2025-01-16</c>; a date, with no time or zone.</summary>
    public string? AirDate { get; init; }
}

/// <summary>One season as the show lists it, before its episodes are asked for.</summary>
public sealed record ShowSeason
{
    public required int Number { get; init; }

    public string? Name { get; init; }

    public int EpisodeCount { get; init; }

    /// <summary>When its first episode aired, <c>2025-01-16</c>.</summary>
    public string? AirDate { get; init; }

    public string? PosterPath { get; init; }
}

/// <summary>One season with its episodes.</summary>
public sealed record SeasonDetails
{
    public required int Number { get; init; }

    public string? Name { get; init; }

    public string? Overview { get; init; }

    public string? AirDate { get; init; }

    public string? PosterPath { get; init; }

    public IReadOnlyList<EpisodeDetails> Episodes { get; init; } = [];
}

/// <summary>One episode: what an episode row shows.</summary>
public sealed record EpisodeDetails
{
    public required int Season { get; init; }

    public required int Number { get; init; }

    public string? Name { get; init; }

    public string? Overview { get; init; }

    public string? AirDate { get; init; }

    public int? RuntimeMinutes { get; init; }

    public double? Rating { get; init; }

    public int Votes { get; init; }

    /// <summary>A frame from the episode, which the episode list shows beside it.</summary>
    public string? StillPath { get; init; }
}
