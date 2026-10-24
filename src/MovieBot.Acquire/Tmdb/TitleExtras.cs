namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

// What a title's description carries beyond its facts: a trailer to watch, what to watch next, what
// people wrote, the title set as a logo. All of it arrives with the description in one call.

/// <summary>An official trailer or teaser on YouTube, embedded from <c>youtube-nocookie.com/embed/{key}</c>.</summary>
public sealed record Trailer
{
    /// <summary>The YouTube video id.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary><c>Trailer</c> or <c>Teaser</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The language it is in, an ISO 639-1 code; null for one with no speech.</summary>
    public string? Language { get; init; }
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
