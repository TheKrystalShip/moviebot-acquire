using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;

namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>How a single check came out.</summary>
public enum CheckResult
{
    /// <summary>Nothing to compare. Not a fault, and not a reason to avoid the subtitle.</summary>
    Unknown = 0,

    Match = 1,

    Mismatch = 2
}

/// <summary>
/// One thing that was compared, in the words it should be shown in. The detail is what makes a
/// mismatch actionable: "25 fps against 23.976" says why a subtitle will drift, where a red cross
/// only says that something is wrong.
/// </summary>
public sealed record SubtitleCheck(string Name, CheckResult Result, string Detail);

/// <summary>A candidate with everything that is known about how well it fits.</summary>
public sealed record CheckedSubtitle
{
    public required SubtitleCandidate Candidate { get; init; }
    public required IReadOnlyList<SubtitleCheck> Checks { get; init; }
    public required int Score { get; init; }

    public bool HasMismatch => Checks.Any(c => c.Result == CheckResult.Mismatch);
}

/// <summary>
/// What can be judged about a subtitle before spending a download on it.
///
/// Everything here is a comparison against the film as it actually sits on this disc, and every
/// answer is one of three rather than two. The third matters: most uploads declare no frame rate
/// and almost none are indexed against a particular file, so treating absent evidence as a
/// failure would condemn nearly every subtitle that exists.
///
/// What is deliberately not checked is the file's character encoding. It is not a property of how
/// well a subtitle fits the film, and it is repaired on the way in, so showing it as a warning
/// would steer people away from subtitles that are perfectly good.
/// </summary>
public static class SubtitleChecks
{
    /// <summary>Frame rates are declared to three decimals at best, so this is loose on purpose.</summary>
    private const double FrameRateTolerance = 0.05;

    public static CheckedSubtitle For(SubtitleCandidate candidate, SubtitleTarget target)
    {
        var checks = new List<SubtitleCheck>();
        var score = 0;

        // Timed for this exact file. Precise when it fires and silent otherwise: a hash is only
        // indexed once somebody has watched that file in a player that reports it, so its absence
        // says nothing at all about the subtitle.
        if (candidate.HashMatch)
        {
            checks.Add(new SubtitleCheck("This file", CheckResult.Match, "timed against this exact file"));
            score += 1000;
        }
        else
        {
            checks.Add(new SubtitleCheck("This file", CheckResult.Unknown, "not indexed against this file"));
        }

        score += FrameRate(candidate, target, checks);
        score += Release(candidate, target, checks);

        if (candidate.CdCount > 1)
        {
            checks.Add(new SubtitleCheck("Whole film", CheckResult.Mismatch,
                $"split across {candidate.CdCount} discs"));
            score -= 400;
        }

        if (candidate.ForeignPartsOnly)
        {
            checks.Add(new SubtitleCheck("Whole film", CheckResult.Mismatch,
                "foreign-language lines only"));
            score -= 500;
        }

        if (candidate.MachineTranslated || candidate.AiTranslated)
        {
            checks.Add(new SubtitleCheck("Written by a person", CheckResult.Mismatch,
                candidate.AiTranslated ? "AI translated" : "machine translated"));
            score -= 300;
        }

        if (candidate.FromTrusted) score += 25;

        // Popularity is the weakest signal here and is weighted to match: it breaks ties between
        // subtitles that are otherwise equal, and it never outranks a measurement.
        score += Math.Min(60, (int)(Math.Log10(1 + candidate.DownloadCount) * 12));

        return new CheckedSubtitle { Candidate = candidate, Checks = checks, Score = score };
    }

    private static int FrameRate(
        SubtitleCandidate candidate, SubtitleTarget target, List<SubtitleCheck> checks)
    {
        if (candidate.Fps <= 0 || target.FrameRate <= 0)
        {
            checks.Add(new SubtitleCheck("Frame rate", CheckResult.Unknown, "not declared"));
            return 0;
        }

        if (Math.Abs(candidate.Fps - target.FrameRate) <= FrameRateTolerance)
        {
            checks.Add(new SubtitleCheck("Frame rate", CheckResult.Match, $"{candidate.Fps:0.###}"));
            return 80;
        }

        // A rate mismatch is the one fault that gets worse as the film runs, so it is penalised
        // harder than anything else that can still be watched.
        checks.Add(new SubtitleCheck("Frame rate", CheckResult.Mismatch,
            $"{candidate.Fps:0.###} against {target.FrameRate:0.###} — drifts"));
        return -150;
    }

    private static int Release(
        SubtitleCandidate candidate, SubtitleTarget target, List<SubtitleCheck> checks)
    {
        if (string.IsNullOrWhiteSpace(candidate.Release) || string.IsNullOrWhiteSpace(target.Release))
        {
            checks.Add(new SubtitleCheck("Release", CheckResult.Unknown, "not stated"));
            return 0;
        }

        // Parsed by the same parser the torrent search uses. A second one written here would not
        // disagree loudly; it would just read one of the two names differently and nobody would
        // know which.
        var theirs = ReleaseParser.Parse(new TrackerTorrent { Name = candidate.Release });
        var ours = ReleaseParser.Parse(new TrackerTorrent { Name = target.Release });

        if (theirs.Group is { Length: > 0 } group
            && string.Equals(group, ours.Group, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new SubtitleCheck("Release", CheckResult.Match, $"same release, {group}"));
            return 300;
        }

        if (theirs.Source == Source.Unknown || ours.Source == Source.Unknown)
        {
            checks.Add(new SubtitleCheck("Release", CheckResult.Unknown, candidate.Release));
            return 0;
        }

        // Two encodes off the same kind of source share a timeline even when the groups differ,
        // which is measurably true: a Blu-ray subtitle from another group needed no shift at all.
        if (SameTimeline(theirs.Source, ours.Source))
        {
            checks.Add(new SubtitleCheck("Release", CheckResult.Match,
                $"{Describe(theirs.Source)}, like ours"));
            return 100;
        }

        checks.Add(new SubtitleCheck("Release", CheckResult.Mismatch,
            $"{Describe(theirs.Source)} against our {Describe(ours.Source)}"));
        return -100;
    }

    /// <summary>
    /// A remux and a Blu-ray encode come off the same disc, so they run to the same clock. A DVD
    /// or a broadcast capture does not.
    /// </summary>
    private static bool SameTimeline(Source a, Source b) =>
        a == b || (Disc(a) && Disc(b));

    private static bool Disc(Source source) => source is Source.BluRay or Source.Remux;

    private static string Describe(Source source) => source switch
    {
        Source.Remux => "remux",
        Source.BluRay => "Blu-ray",
        Source.Web => "WEB",
        Source.Dvd => "DVD",
        Source.Cam => "cam",
        _ => "unknown"
    };
}
