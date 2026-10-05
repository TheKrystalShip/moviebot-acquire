namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// A film or a show at a glance, as the database's lookup by IMDb id answers it in one call: what a
/// search result shows beside its poster. The rest of it is <see cref="FilmDetails"/> or
/// <see cref="ShowDetails"/>, asked for when the title is opened.
/// </summary>
public sealed record TitleGlance
{
    public required int TmdbId { get; init; }

    /// <summary><c>film</c> or <c>show</c>, which says which of the database's two id spaces <see cref="TmdbId"/> is in.</summary>
    public required string Kind { get; init; }

    /// <summary>Out of ten, to one decimal; null when nobody has voted.</summary>
    public double? Rating { get; init; }

    public int Votes { get; init; }

    public string? Overview { get; init; }

    /// <summary>The day a film was released or a show first aired, when the database knows it.</summary>
    public DateOnly? Released { get; init; }

    /// <summary>The database's image path, for <see cref="TmdbClient.ImageAsync"/>: the poster its full description carries.</summary>
    public string? PosterPath { get; init; }
}
