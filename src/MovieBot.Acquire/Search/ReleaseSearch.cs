using Microsoft.Extensions.Logging;
using TheKrystalShip.MovieBot.Acquire.Tracker;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// The one entry point anything outside this project uses to find a film.
///
/// It is the seam that keeps the tracker's shapes and the account's passkey inside this project:
/// what comes back is <see cref="Release"/>, which holds no credential and can be logged, shown
/// and put in a menu without care.
/// </summary>
public sealed class ReleaseSearch(
    TrackerClient client,
    ReleaseRanker ranker,
    ILogger<ReleaseSearch> logger)
{
    /// <summary>
    /// Finds candidates for what somebody typed, which is normally a title and often a year.
    ///
    /// The whole phrase goes to the tracker, because it narrows on the year itself and a bare
    /// common word comes back against a server-side cap. The year is then applied again here,
    /// against each release's own year, since matching words is not the same as matching films.
    /// </summary>
    public async Task<RankedReleases> ByTitleAsync(string query, CancellationToken ct)
    {
        var parsed = SearchQuery.Parse(query);
        var rows = await client.SearchByNameAsync(parsed.Raw, ct);

        logger.LogInformation(
            "Search for {Title}{Year} returned {Rows} rows.",
            parsed.Title, parsed.Year is { } y ? $" ({y})" : "", rows.Count);

        return Rank(rows, parsed.Year, parsed.Title);
    }

    /// <summary>
    /// Finds candidates for an exact film. Preferred over a title where an id is known: it
    /// separates a remake from its original, and it survives a film being released under a
    /// different name in another country.
    /// </summary>
    public async Task<RankedReleases> ByImdbAsync(string imdbId, CancellationToken ct)
    {
        var rows = await client.SearchByImdbAsync(imdbId, ct);
        logger.LogInformation("Search for {Imdb} returned {Rows} rows.", imdbId, rows.Count);

        // The id already names one film, so there is nothing left to disagree with.
        return Rank(rows, null, null);
    }

    private RankedReleases Rank(
        IReadOnlyList<TrackerTorrent> rows, int? expectedYear, string? expectedTitle)
    {
        var ranked = ranker.Rank(rows.Select(ReleaseParser.Parse), expectedYear, expectedTitle);
        logger.LogInformation("Offering {Offered} of {Rows}.", ranked.Candidates.Count, rows.Count);
        return ranked;
    }
}
