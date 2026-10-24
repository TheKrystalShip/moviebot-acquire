namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

// What a title's description carries beyond its facts: a trailer to watch, where it streams, what to
// watch next, what people wrote, the title set as a logo. All of it arrives with the description in
// one call.

/// <summary>A trailer or teaser on YouTube, embedded from <c>youtube-nocookie.com/embed/{key}</c>.</summary>
public sealed record Trailer
{
    /// <summary>The YouTube video id.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary><c>Trailer</c> or <c>Teaser</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Whether the rights holder published it.</summary>
    public bool Official { get; init; }

    /// <summary>The language it is in, an ISO 639-1 code; null for one with no speech.</summary>
    public string? Language { get; init; }
}

/// <summary>A streaming service, a rental shop or a store the database knows by an id of its own.</summary>
public sealed record WatchProvider
{
    public required int ProviderId { get; init; }

    public required string Name { get; init; }

    /// <summary>The logo's path in the database's image store, for <see cref="TmdbClient.ImageAsync"/>.</summary>
    public string? LogoPath { get; init; }
}

/// <summary>
/// Where a title can be watched in one region, by how: streamed with a subscription, rented, bought,
/// free, or free with advertising. The data is JustWatch's and is attributed to it where shown.
/// </summary>
public sealed record WatchProviders
{
    public required string Region { get; init; }

    /// <summary>The page on the database that lists these, which is JustWatch's.</summary>
    public string? Link { get; init; }

    public IReadOnlyList<WatchProvider> Flatrate { get; init; } = [];

    public IReadOnlyList<WatchProvider> Rent { get; init; } = [];

    public IReadOnlyList<WatchProvider> Buy { get; init; } = [];

    public IReadOnlyList<WatchProvider> Free { get; init; } = [];

    public IReadOnlyList<WatchProvider> Ads { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<WatchProvider> All => Flatrate.Concat(Rent).Concat(Buy).Concat(Free).Concat(Ads);
}

/// <summary>What a reviewer on the database wrote, cut to its beginning.</summary>
public sealed record Review
{
    public required string Author { get; init; }

    /// <summary>Out of ten, when the reviewer gave one.</summary>
    public double? Rating { get; init; }

    public required string Excerpt { get; init; }

    public string? Url { get; init; }
}

/// <summary>One of the database's keywords, by the id a discovery filters on.</summary>
public sealed record KeywordTag
{
    public required int Id { get; init; }

    public required string Name { get; init; }
}
