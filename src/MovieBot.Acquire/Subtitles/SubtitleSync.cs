using System.Text.RegularExpressions;

namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>
/// The times cues start, read out of either format a subtitle arrives in.
///
/// Only the start of each cue is taken. It is the part two subtitles for the same film agree on:
/// how long a line stays on screen is the author's choice, but when a person begins speaking is
/// the film's.
/// </summary>
public static partial class CueTimings
{
    /// <summary>
    /// Matches the start timestamp of a cue in both formats at once. SubRip separates milliseconds
    /// with a comma and always writes the hour; WebVTT uses a dot and may leave the hour off.
    /// </summary>
    [GeneratedRegex(@"(?:(\d+):)?(\d{1,2}):(\d{2})[.,](\d{1,3})\s*-->", RegexOptions.Compiled)]
    private static partial Regex CueStart();

    public static IReadOnlyList<double> Read(string text)
    {
        var times = new List<double>();

        foreach (Match match in CueStart().Matches(text))
        {
            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);

            // A two-digit fraction is hundredths, not thousandths.
            var fraction = match.Groups[4].Value;
            var milliseconds = int.Parse(fraction) * (int)Math.Pow(10, 3 - fraction.Length);

            times.Add(hours * 3600 + minutes * 60 + seconds + milliseconds / 1000.0);
        }

        times.Sort();
        return times;
    }
}

/// <summary>How well a subtitle lines up with one already known to fit the film.</summary>
public sealed record SyncMeasurement
{
    /// <summary>Seconds to add to the candidate to make it fit. Zero means it already does.</summary>
    public required double ShiftSeconds { get; init; }

    public required int Aligned { get; init; }

    public required int Total { get; init; }

    public double AlignedFraction => Total == 0 ? 0 : Aligned / (double)Total;
}

/// <summary>
/// Measures a subtitle against a reference instead of guessing from what an uploader typed.
///
/// This is the only check here that is a measurement rather than an inference, so where a
/// reference exists it outranks everything else. A film's own embedded track is the obvious one:
/// it shipped with the file, so it fits the file by construction.
/// </summary>
public static class SubtitleSync
{
    /// <summary>Two cues this close are the same moment, allowing for how authors round.</summary>
    private const double AlignmentTolerance = 0.25;

    /// <summary>The widest offset worth considering. Beyond this it is a different cut, not a shift.</summary>
    private const double SearchWindowSeconds = 20.0;

    /// <summary>Resolution of the shift search, which is finer than anyone can perceive.</summary>
    private const double Step = 0.05;

    /// <summary>Fewer cues than this and a coincidence outscores a real alignment.</summary>
    private const int MinimumCues = 20;

    /// <summary>
    /// The share of a subtitle's cues that must agree on one offset before it counts as found.
    ///
    /// This is what separates an answer from noise. Two subtitles for the same film agree on most
    /// of their cues even when the authors broke lines differently; two unrelated ones scatter
    /// their votes across every bucket and none of them reaches this.
    ///
    /// It also, deliberately, refuses a subtitle whose timing stretches rather than shifts. There
    /// is no single offset that fits one, so there is no honest answer to give, and a failure to
    /// align is the useful thing to report.
    ///
    /// The figure is measured rather than chosen. Against one film's English track: another
    /// English subtitle for the same film reaches 20 per cent, the Romanian track for that same
    /// film 13, and the commentary track — which shares the film but none of its dialogue timing —
    /// under 3. The line sits in the gap, low enough to accept a translation that broke its lines
    /// somewhere else entirely and high enough that nothing unrelated clears it.
    /// </summary>
    private const double MinimumConsensus = 0.08;

    public static SyncMeasurement? Against(
        IReadOnlyList<double> reference, IReadOnlyList<double> candidate)
    {
        if (reference.Count < MinimumCues || candidate.Count < MinimumCues) return null;

        var shift = BestShift(reference, candidate);
        if (shift is null) return null;

        return new SyncMeasurement
        {
            ShiftSeconds = shift.Value,
            Aligned = Aligned(reference, candidate, shift.Value),
            Total = candidate.Count
        };
    }

    /// <summary>
    /// The offset most cue pairs agree on.
    ///
    /// Found by voting rather than by trying every shift in turn: each candidate cue votes for the
    /// distance to every reference cue near it, and the winning bucket is the answer. That costs
    /// one pass instead of one pass per candidate shift, and it degrades honestly — when two
    /// subtitles have nothing to do with each other the votes scatter and no bucket wins.
    /// </summary>
    private static double? BestShift(IReadOnlyList<double> reference, IReadOnlyList<double> candidate)
    {
        if (candidate.Count == 0) return null;

        var votes = new Dictionary<int, int>();

        foreach (var time in candidate)
        {
            var from = LowerBound(reference, time - SearchWindowSeconds);

            for (var i = from; i < reference.Count; i++)
            {
                var difference = reference[i] - time;
                if (difference > SearchWindowSeconds) break;

                var bucket = (int)Math.Round(difference / Step);
                votes[bucket] = votes.GetValueOrDefault(bucket) + 1;
            }
        }

        if (votes.Count == 0) return null;

        var best = votes.OrderByDescending(v => v.Value).ThenBy(v => Math.Abs(v.Key)).First();

        if (best.Value < candidate.Count * MinimumConsensus) return null;

        return Math.Round(best.Key * Step, 2);
    }

    private static int Aligned(
        IReadOnlyList<double> reference, IReadOnlyList<double> candidate, double shift)
    {
        var count = 0;

        foreach (var time in candidate)
        {
            var shifted = time + shift;
            var i = LowerBound(reference, shifted - AlignmentTolerance);

            if (i < reference.Count && Math.Abs(reference[i] - shifted) <= AlignmentTolerance) count++;
        }

        return count;
    }

    private static int LowerBound(IReadOnlyList<double> sorted, double value)
    {
        int low = 0, high = sorted.Count;

        while (low < high)
        {
            var mid = (low + high) / 2;
            if (sorted[mid] < value) low = mid + 1;
            else high = mid;
        }

        return low;
    }
}
