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

    /// <summary>The database's own rating out of ten, from its users' votes. Absent with no votes.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public IReadOnlyList<string> Directors { get; init; } = [];

    public IReadOnlyList<string> Writers { get; init; } = [];

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
}

public sealed record CastMember
{
    public required string Name { get; init; }

    /// <summary>Who they play, as the credits spell it.</summary>
    public string? Character { get; init; }

    /// <summary>Their portrait's path in the database's image store, like the poster's.</summary>
    public string? ProfilePath { get; init; }
}
