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

    /// <summary>Null when nobody has voted: a rating with no votes behind it is a placeholder.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public IReadOnlyList<string> Creators { get; init; } = [];

    /// <summary>The cast across every season, billed in the database's order, with their main role.</summary>
    public IReadOnlyList<CastMember> Cast { get; init; } = [];

    public string? OriginalLanguage { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public IReadOnlyList<string> Countries { get; init; } = [];

    public IReadOnlyList<string> Networks { get; init; } = [];

    /// <summary>The seasons, in order; season 0 is the database's home for specials.</summary>
    public IReadOnlyList<ShowSeason> Seasons { get; init; } = [];

    public string? PosterPath { get; init; }

    public string? BackdropPath { get; init; }
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
