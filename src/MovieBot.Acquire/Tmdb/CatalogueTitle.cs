namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A film or a show as a browse of the database names it, in a genre or among a person's credits:
/// what a poster card shows, and the IMDb id that leads to the rest of it.
/// </summary>
public sealed record CatalogueTitle
{
    public required int TmdbId { get; init; }

    /// <summary>The canonical <c>tt0458352</c> form, which is how everything else here names a title.</summary>
    public required string ImdbId { get; init; }

    /// <summary><c>film</c> or <c>show</c>, which says which of the database's two id spaces <see cref="TmdbId"/> is in.</summary>
    public required string Kind { get; init; }

    public required string Title { get; init; }

    /// <summary>The day a film was released or a show first aired, when the database knows it.</summary>
    public DateOnly? Released { get; init; }

    /// <summary>Out of ten, to one decimal; null when nobody has voted.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public string? Overview { get; init; }

    /// <summary>The database's image path, for <see cref="TmdbClient.ImageAsync"/>.</summary>
    public string? PosterPath { get; init; }

    /// <summary>
    /// What a person did on it, among their credits: the part they played, the job they did, or both
    /// joined by a middle dot. Null in a genre's list, and for a part the credits do not name.
    /// </summary>
    public string? Role { get; init; }
}

/// <summary>A person as the database describes them, with the films and shows they are credited on.</summary>
public sealed record PersonCredits
{
    public required int TmdbId { get; init; }

    public required string Name { get; init; }

    /// <summary>The department the database knows them for: <c>Acting</c>, <c>Directing</c>, <c>Writing</c>…</summary>
    public string? KnownFor { get; init; }

    /// <summary>Their portrait's path in the database's image store, like a poster's.</summary>
    public string? ProfilePath { get; init; }

    /// <summary>Their best-known credits first: the titles the most people have voted on.</summary>
    public IReadOnlyList<CatalogueTitle> Titles { get; init; } = [];
}

/// <summary>One of the database's genres for films or for shows, which are two separate lists.</summary>
public sealed record CatalogueGenre
{
    public required int Id { get; init; }

    public required string Name { get; init; }
}
