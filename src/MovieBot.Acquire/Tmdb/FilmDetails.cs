namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A film as the database describes it: everything a page about it shows beyond the name, the year
/// and the poster. Every field but the ids and the name may be absent, because the database's
/// coverage of an obscure film is thin and an empty field is the honest answer.
/// </summary>
public sealed record FilmDetails
{
    public required int TmdbId { get; init; }

    /// <summary>The canonical <c>tt0458352</c> form, which is how everything else here names a film.</summary>
    public required string ImdbId { get; init; }

    public required string Title { get; init; }

    /// <summary>The title in the language it was made in, when that differs.</summary>
    public string? OriginalTitle { get; init; }

    public int? Year { get; init; }

    /// <summary>The first release, as <c>yyyy-MM-dd</c>.</summary>
    public string? ReleaseDate { get; init; }

    public int? RuntimeMinutes { get; init; }

    /// <summary>The certificate in the configured country: <c>R</c>, <c>PG-13</c>, <c>15</c>.</summary>
    public string? Certification { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public string? Overview { get; init; }

    public string? Tagline { get; init; }

    /// <summary>
    /// The database's keywords for the film: <c>time loop</c>, <c>heist</c>. Null in a
    /// description kept before the database was asked for them; empty when it names none.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>The database's own rating out of ten, from its users' votes. Absent with no votes.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public IReadOnlyList<string> Directors { get; init; } = [];

    public IReadOnlyList<string> Writers { get; init; } = [];

    /// <summary>Each director's and writer's id in the database, by the name they are listed under.</summary>
    public IReadOnlyDictionary<string, int> PersonIds { get; init; } = new Dictionary<string, int>();

    /// <summary>Top-billed first, as the credits order them.</summary>
    public IReadOnlyList<CastMember> Cast { get; init; } = [];

    /// <summary>The language the film was made in, as an ISO 639-1 code.</summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>The languages spoken in it, by their English names.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>The countries it was produced in, by their English names.</summary>
    public IReadOnlyList<string> Countries { get; init; } = [];

    /// <summary>
    /// The poster's path in the database's image store, for <see cref="TmdbClient.ImageAsync"/>.
    /// A path rather than a URL: whoever shows the film fetches the image once and serves its own
    /// copy, so the database is asked once rather than by every viewer.
    /// </summary>
    public string? PosterPath { get; init; }

    /// <summary>A still from the film, wide, for behind a heading; a path like the poster's.</summary>
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

    /// <summary>What the film cost to make, in US dollars; null when the database does not know.</summary>
    public long? Budget { get; init; }

    /// <summary>What the film took, in US dollars; null when the database does not know.</summary>
    public long? Revenue { get; init; }
}

public sealed record CastMember
{
    /// <summary>The person's id in the database, which their credits are asked for by; null in a description kept without one.</summary>
    public int? TmdbId { get; init; }

    public required string Name { get; init; }

    /// <summary>Who they play, as the credits spell it.</summary>
    public string? Character { get; init; }

    /// <summary>Their portrait's path in the database's image store, like the poster's.</summary>
    public string? ProfilePath { get; init; }
}
