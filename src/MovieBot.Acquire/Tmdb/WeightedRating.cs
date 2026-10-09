namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// Titles ordered by a rating that weighs how many people gave it. A plain average puts a film rated
/// 8.4 by 268 people above one rated 7.6 by 19,000, which says less about the film than about who
/// bothered to vote. The weighted rating is <c>v / (v + m) * R + m / (v + m) * C</c>: a title's own
/// average <c>R</c> counts in proportion to its votes <c>v</c>, and the rest of the weight goes to
/// <c>C</c>, the average the whole set was voted. Both are read off the set being ordered: <c>C</c> is
/// its vote-weighted mean and <c>m</c> the votes of its quarter-way title, so a title with fewer
/// votes than three quarters of the set is pulled more than half way to the mean, and a set of
/// obscure titles is judged by its own standards rather than by a fixed number of votes.
/// </summary>
public static class WeightedRating
{
    /// <summary>The titles best first; one nobody has rated comes last.</summary>
    public static IReadOnlyList<ListedTitle> Order(IReadOnlyList<ListedTitle> titles)
    {
        var rated = titles.Where(t => t.Rating is > 0 && t.Votes > 0).ToList();
        if (rated.Count == 0) return titles;

        var mean = rated.Sum(t => t.Rating!.Value * t.Votes) / rated.Sum(t => (double)t.Votes);
        var votes = rated.Select(t => t.Votes).Order().ToList();
        var prior = Math.Max(1, votes[(votes.Count - 1) / 4]);

        double Score(ListedTitle t) =>
            t.Votes / (t.Votes + (double)prior) * t.Rating!.Value + prior / (t.Votes + (double)prior) * mean;

        return [.. rated.OrderByDescending(Score).ThenByDescending(t => t.Votes), .. titles.Except(rated)];
    }
}
