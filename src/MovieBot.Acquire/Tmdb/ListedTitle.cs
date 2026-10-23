namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A film or a show as one of the database's lists names it, by the database's own id alone: a
/// search's hit, a discovery's result, a recommendation. What a poster card shows and what a filter
/// reads, before <see cref="TitleCard"/> says which IMDb id it is and who made it.
/// </summary>
public sealed record ListedTitle
{
    public required int TmdbId { get; init; }

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

    /// <summary>The database's ids for the genres it files the title under; a genre's name is <see cref="TmdbClient.GenresAsync"/>'s.</summary>
    public IReadOnlyList<int> GenreIds { get; init; } = [];

    /// <summary>The language it was made in, as an ISO 639-1 code.</summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>The database's measure of how much the title is looked at lately.</summary>
    public double Popularity { get; init; }
}

/// <summary>One page of a list the database answers in pages, and how many pages the list has at that page's size.</summary>
public sealed record ListedPage
{
    public IReadOnlyList<ListedTitle> Titles { get; init; } = [];

    public int Pages { get; init; }
}

/// <summary>
/// What names a title beyond a list's entry: its IMDb id, which everything else here leads by, the
/// genres it is filed under, who made it and who stars in it. One call to the database per title.
/// </summary>
public sealed record TitleCard
{
    public required int TmdbId { get; init; }

    public required string Kind { get; init; }

    /// <summary>The canonical <c>tt0458352</c> form; null for a title the database holds no IMDb id for, as an announcement often is.</summary>
    public string? ImdbId { get; init; }

    public required string Title { get; init; }

    /// <summary>The day a film was released or a show first aired, when the database knows it.</summary>
    public DateOnly? Released { get; init; }

    /// <summary>
    /// The database's word for where the title stands: <c>Released</c>, <c>Post Production</c>,
    /// <c>Planned</c>, <c>Rumored</c> for a film; <c>Returning Series</c>, <c>Ended</c>… for a show.
    /// </summary>
    public string? Status { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>A film's directors, a show's creators.</summary>
    public IReadOnlyList<string> MadeBy { get; init; } = [];

    /// <summary>The two top-billed actors.</summary>
    public IReadOnlyList<string> Starring { get; init; } = [];

    public string? OriginalLanguage { get; init; }
}
