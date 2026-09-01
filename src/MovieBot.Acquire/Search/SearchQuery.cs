using System.Globalization;
using System.Text.RegularExpressions;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// What somebody typed, split into the film and the year they gave for it.
///
/// A year is the ordinary way people disambiguate a film, and it does two jobs here. The tracker
/// narrows on it — a bare title comes back against a server-side cap, so a common word returns a
/// hundred rows of which few are the film — and it is kept afterwards to drop a release that
/// matched the words but is a different film.
/// </summary>
/// <param name="Raw">Exactly what was typed, which is what the tracker is asked.</param>
/// <param name="Title">The film, with any trailing year removed.</param>
/// <param name="Year">The year, when one was given.</param>
public sealed partial record SearchQuery(string Raw, string Title, int? Year)
{
    /// <summary>
    /// The earliest year a film could carry. Anything below it in a title is part of the title.
    /// </summary>
    private const int EarliestFilmYear = 1888;

    [GeneratedRegex(@"^(?<title>.*?)[\s.\-_]+\(?(?<year>\d{4})\)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingYear();

    public static SearchQuery Parse(string input)
    {
        var raw = (input ?? "").Trim();
        if (raw.Length == 0) return new SearchQuery("", "", null);

        var match = TrailingYear().Match(raw);
        if (!match.Success) return new SearchQuery(raw, raw, null);

        var title = match.Groups["title"].Value.Trim();
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);

        // A four-digit number is only a year if a film could have been released in it. That is
        // what keeps "Blade Runner 2049" whole while reading the year out of "Heat 1995": the
        // first is set too far ahead to be a release year, the second is not.
        //
        // The title also has to survive it. "1917" and "2012" are films, and stripping their
        // only token would leave nothing to search for.
        if (title.Length == 0 || year < EarliestFilmYear || year > LatestPlausibleYear())
            return new SearchQuery(raw, raw, null);

        return new SearchQuery(raw, title, year);
    }

    /// <summary>
    /// Announced films are listed before they are released, so the ceiling sits a little ahead of
    /// now rather than on it.
    /// </summary>
    private static int LatestPlausibleYear() => DateTimeOffset.UtcNow.Year + 2;
}
