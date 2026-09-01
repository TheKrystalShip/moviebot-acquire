using TheKrystalShip.MovieBot.Acquire.Search;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Stands in for the tracker, counting what it was asked. The point of the autocomplete tests is
/// the calls that are not made, so the count is the assertion.
/// </summary>
internal sealed class StubReleaseSearch(IReadOnlyList<Release> results, Action onCall) : IReleaseSearch
{
    public Task<RankedReleases> ByTitleAsync(string query, CancellationToken ct)
    {
        onCall();
        return Task.FromResult(new RankedReleases(results, []));
    }

    public Task<RankedReleases> ByImdbAsync(string imdbId, CancellationToken ct)
    {
        onCall();
        return Task.FromResult(new RankedReleases(results, []));
    }
}
