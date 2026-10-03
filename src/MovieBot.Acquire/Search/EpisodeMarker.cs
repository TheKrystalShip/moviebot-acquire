using System.Globalization;
using System.Text.RegularExpressions;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>One episode of a show, by its place in it.</summary>
public readonly record struct EpisodeNumber(int Season, int Episode) : IComparable<EpisodeNumber>
{
    public int CompareTo(EpisodeNumber other) =>
        Season != other.Season ? Season.CompareTo(other.Season) : Episode.CompareTo(other.Episode);

    /// <summary>The scene spelling, <c>S02E04</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"S{Season:00}E{Episode:00}");
}

/// <summary>
/// The part of a release or file name that places it in a show: <c>S02</c> for a season,
/// <c>S02E04</c> for an episode, <c>S02E04E05</c> or <c>S02E04-E05</c> for a run of them,
/// <c>S01-S03</c> for several seasons.
///
/// It is the only thing that tells a show's release from a film's, since the tracker files both
/// under the same kind of name, and it is where a show's title ends: everything to its left is the
/// show, everything to its right is the episode's own name and the encode.
/// </summary>
/// <param name="Season">The season, or the first of several.</param>
/// <param name="LastSeason">The last season of a pack covering several, else null.</param>
/// <param name="Episodes">The episodes named, in order; empty for a whole season.</param>
/// <param name="Index">Where the marker starts in the name.</param>
public sealed partial record EpisodeMarker(int Season, int? LastSeason, IReadOnlyList<int> Episodes, int Index)
{
    // Whole-token only: a substring test reads a season out of a word that happens to hold an S
    // and digits. The episode part is tried before the season range, so "S01E01-02" is two
    // episodes and "S01-02" is two seasons.
    [GeneratedRegex(
        @"(?<![A-Za-z0-9])S(?<s>\d{1,2})"
        + @"(?:E(?<e>\d{1,3})(?:-?E(?<e>\d{1,3})|-(?<e>\d{1,3})(?![0-9pP]))*"
        + @"|-S?(?<s2>\d{1,2}))?(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Marker();

    /// <summary>Whether this names a whole season (or several) rather than particular episodes.</summary>
    public bool IsSeasonPack => Episodes.Count == 0;

    /// <summary>Every season the name covers.</summary>
    public IEnumerable<int> Seasons =>
        Enumerable.Range(Season, (LastSeason is { } last && last > Season ? last : Season) - Season + 1);

    /// <summary>The episodes named, with their season.</summary>
    public IEnumerable<EpisodeNumber> EpisodeNumbers => Episodes.Select(e => new EpisodeNumber(Season, e));

    /// <summary>As a person reads it: <c>S02</c>, <c>S02E04</c>, <c>S02E04–E05</c>, <c>S01–S03</c>.</summary>
    public string Label => Episodes.Count switch
    {
        0 when LastSeason is { } last && last > Season =>
            string.Create(CultureInfo.InvariantCulture, $"S{Season:00}–S{last:00}"),
        0 => string.Create(CultureInfo.InvariantCulture, $"S{Season:00}"),
        1 => new EpisodeNumber(Season, Episodes[0]).ToString(),
        _ => string.Create(CultureInfo.InvariantCulture,
            $"{new EpisodeNumber(Season, Episodes[0])}–E{Episodes[^1]:00}"),
    };

    /// <summary>The first marker in a name, or null when the name places nothing in a show.</summary>
    public static EpisodeMarker? Read(string name)
    {
        var match = Marker().Match(name);
        if (!match.Success) return null;

        var season = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        var episodes = match.Groups["e"].Captures
            .Select(c => int.Parse(c.Value, CultureInfo.InvariantCulture))
            .ToList();

        // A range written as its two ends, "E04-E06", names the episodes between them as well.
        if (episodes.Count == 2 && episodes[1] > episodes[0] + 1)
            episodes = Enumerable.Range(episodes[0], episodes[1] - episodes[0] + 1).ToList();

        int? lastSeason = null;
        if (episodes.Count == 0 && match.Groups["s2"].Success)
        {
            var last = int.Parse(match.Groups["s2"].Value, CultureInfo.InvariantCulture);
            if (last > season) lastSeason = last;
        }

        return new EpisodeMarker(season, lastSeason, episodes.Distinct().Order().ToList(), match.Index);
    }
}
