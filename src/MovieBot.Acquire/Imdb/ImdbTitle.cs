namespace TheKrystalShip.MovieBot.Acquire.Imdb;

/// <summary>
/// What the database knows about a film, as far as anything worth showing goes.
///
/// A release name carries the same facts badly — a year buried between a resolution and a codec,
/// a title with the dots still in it — and carries nothing at all about who is in it or what the
/// poster looks like. This is the same film described by the people who catalogue films.
/// </summary>
public sealed record ImdbTitle
{
    /// <summary>The canonical <c>tt0133093</c> form.</summary>
    public required string ImdbId { get; init; }

    /// <summary>The film's name, with none of a release's decorations.</summary>
    public required string Title { get; init; }

    public int? Year { get; init; }

    /// <summary>
    /// What to call the film. The one spelling of a name and a year written together, so a film
    /// is not named one way in a menu row and another in the message that follows it.
    /// </summary>
    public string Display => Year is { } year ? $"{Title} ({year})" : Title;

    /// <summary>Top-billed cast, already written as one line.</summary>
    public string? Starring { get; init; }

    /// <summary>
    /// The poster, at whatever size the database holds it. Artwork is served from an image host
    /// that resizes on demand, which <see cref="PosterAt"/> is for.
    /// </summary>
    public Uri? PosterUrl { get; init; }

    /// <summary>What kind of thing this is — a feature, an episode, a series.</summary>
    public string? Kind { get; init; }

    /// <summary>Whether this is a film rather than a series or an episode of one.</summary>
    public bool IsFeature =>
        Kind is null || Kind.Contains("movie", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The poster no wider than a number of pixels.
    ///
    /// The image host takes the size in the file name, and the originals are several thousand
    /// pixels tall: asking for the size that will be looked at costs a fraction of the bytes and
    /// is the difference between a poster that renders and one that times out.
    /// </summary>
    public Uri? PosterAt(int width)
    {
        if (PosterUrl is null) return null;

        var url = PosterUrl.ToString();
        var marker = url.LastIndexOf("._V1_", StringComparison.Ordinal);
        if (marker < 0) return PosterUrl;

        var extension = Path.GetExtension(url);
        return new Uri($"{url[..marker]}._V1_QL75_UX{width}_{extension}");
    }
}
