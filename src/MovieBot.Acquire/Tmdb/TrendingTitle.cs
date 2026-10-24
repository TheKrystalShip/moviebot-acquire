namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A film or a show the database counts among the most watched and talked about this week: what a
/// poster on a shelf shows, and the IMDb id that leads to the rest of it.
/// </summary>
public sealed record TrendingTitle
{
    public required int TmdbId { get; init; }

    /// <summary>The canonical <c>tt0458352</c> form, which is how everything else here names a title.</summary>
    public required string ImdbId { get; init; }

    public required string Title { get; init; }

    /// <summary>The year a film was released or a show first aired.</summary>
    public int? Year { get; init; }

    /// <summary>Out of ten, to one decimal; null when nobody has voted.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    /// <summary>The database's image path, for <see cref="TmdbClient.ImageAsync"/>.</summary>
    public string? PosterPath { get; init; }

    /// <summary>The database's genres it is filed under, by id in its kind's list (<see cref="TmdbClient.GenresAsync"/>).</summary>
    public IReadOnlyList<int> GenreIds { get; init; } = [];

    /// <summary>The language it was made in, an ISO 639-1 code, when the database says.</summary>
    public string? OriginalLanguage { get; init; }
}
