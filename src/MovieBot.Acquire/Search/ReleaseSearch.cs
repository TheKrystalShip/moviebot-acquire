using Microsoft.Extensions.Logging;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Imdb;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// Finding a film, as the autocomplete layer needs it. Separate from the implementation so that
/// layer's pacing can be tested without a tracker to call.
/// </summary>
public interface IReleaseSearch
{
    Task<RankedReleases> ByTextAsync(string typed, CancellationToken ct);

    Task<RankedReleases> ByTitleAsync(string query, CancellationToken ct);

    Task<RankedReleases> ByImdbAsync(string imdbId, CancellationToken ct);
}

/// <summary>
/// The one entry point anything outside this project uses to find a film.
///
/// It is the seam that keeps the tracker's shapes and the account's passkey inside this project:
/// what comes back is <see cref="Release"/>, which holds no credential and can be logged, shown
/// and put in a menu without care.
/// </summary>
public sealed class ReleaseSearch(
    TrackerClient client,
    ImdbClient imdb,
    ReleaseRanker ranker,
    ILogger<ReleaseSearch> logger) : IReleaseSearch
{
    /// <summary>
    /// Finds candidates for whatever somebody typed, by working out which film they meant first.
    ///
    /// The tracker names a film the way the country that made it named it, so a search for the
    /// English name of a film shot elsewhere matches nothing at all: the releases are there, under
    /// a title nobody outside would think to type. The title index knows both names for the same
    /// film, so the film is identified there and the tracker is then asked for that film by id —
    /// which is a fact the two agree on however the film is spelled.
    ///
    /// Naming the film first is also what makes half a title work. The index answers a prefix,
    /// being what a search box reads, where matching a release's own words cannot: a title is only
    /// itself once it is finished being typed.
    ///
    /// Text carrying an id is a film already named, and takes the same path without a search.
    /// Falling back to the words is what keeps a film the index does not know findable.
    /// </summary>
    public async Task<RankedReleases> ByTextAsync(string typed, CancellationToken ct)
    {
        if (await IdentifyAsync(typed, ct) is not { ImdbId.Length: > 0 } film)
            return await ByTitleAsync(typed, ct);

        var rows = await client.SearchByImdbAsync(film.ImdbId, ct);

        logger.LogInformation(
            "Search for {Title} ({Imdb}) returned {Rows} rows.", film.Display, film.ImdbId, rows.Count);

        // The id names one film, so there is nothing left for a title or a year to disagree with.
        return Rank(rows, null, null) with { Film = film };
    }

    /// <summary>
    /// Which film somebody meant. A year decides between the films sharing a name, since that is
    /// the whole of what separates a remake from what it remade; without one the index's own
    /// order stands, which is the order a person searching would expect to see.
    /// </summary>
    private async Task<ImdbTitle?> IdentifyAsync(string typed, CancellationToken ct)
    {
        var found = await imdb.SuggestAsync(typed, ct);
        if (found.Count == 0) return null;

        return SearchQuery.Parse(typed).Year is { } wanted
            ? found.FirstOrDefault(f => f.Year == wanted) ?? found[0]
            : found[0];
    }

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
