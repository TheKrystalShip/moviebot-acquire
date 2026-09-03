using Microsoft.Extensions.Options;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// How long a downloaded film stays before it is let go.
///
/// Both figures are measured on the client's own seeding clock, which stops while the machine is
/// off or the torrent is stopped. That is the clock the tracker credits, so a week here is a week
/// of seeding and not a week on the calendar.
/// </summary>
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    /// <summary>
    /// How many days of seeding a film gets before it is pruned, unless somebody keeps it. It has
    /// to sit well above the tracker's minimum: the client's clock can run ahead of the tracker's
    /// whenever an announce fails to land, and the gap between the two is what absorbs that.
    /// </summary>
    public double SeedDays { get; set; } = 7;

    /// <summary>
    /// The seeding the tracker demands of every download, in hours. Falling short of it is
    /// penalised, so nothing is ever removed below it, whatever else says it may be.
    /// </summary>
    public double TrackerMinimumHours { get; set; } = 48;

    /// <summary>
    /// Added to the tracker's minimum before anything is removed. The tracker counts by the
    /// announces it received and the client counts by the time it ran, and the two disagree by
    /// whatever announces did not arrive — always in the direction of the tracker crediting less.
    /// </summary>
    public double MarginHours { get; set; } = 12;

    /// <summary>The seeding a download must have before it is pruned.</summary>
    public TimeSpan Window => TimeSpan.FromDays(SeedDays);

    /// <summary>The seeding below which nothing is removed, whatever the window says.</summary>
    public TimeSpan Floor => TimeSpan.FromHours(TrackerMinimumHours + MarginHours);

    /// <summary>
    /// The window may not sit under the floor: a window that short would either be ignored, which
    /// makes it a lie, or honoured, which earns the penalty the floor exists to avoid.
    /// </summary>
    public bool IsCoherent => SeedDays >= 0 && TrackerMinimumHours >= 0 && MarginHours >= 0
                              && Window >= Floor;
}

/// <summary>
/// The retention rule, asked one download at a time.
///
/// It decides nothing about kept films, running transcodes or occupied rooms: those are facts the
/// caller holds and checks. What this answers is only how the seeding a download has done stands
/// against the window.
/// </summary>
public sealed class Retention(IOptions<RetentionOptions> options)
{
    private readonly RetentionOptions _options = options.Value;

    public TimeSpan Window => _options.Window;

    public TimeSpan Floor => _options.Floor;

    /// <summary>
    /// Whether a download has seeded for the whole window. Never true below the floor, so a
    /// window misconfigured under the tracker's minimum still removes nothing early.
    /// </summary>
    public bool IsDue(DownloadStatus download) =>
        download.IsFinished && download.Seeded >= Window && download.Seeded >= Floor;

    /// <summary>
    /// How much more seeding a download has ahead of it before it is due. Zero once it is due, so
    /// a surface never shows a negative wait.
    /// </summary>
    public TimeSpan Remaining(DownloadStatus download)
    {
        var until = Window > Floor ? Window : Floor;
        var left = until - download.Seeded;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    /// <summary>
    /// The remaining seeding as a person reads it: whole days while there are days, hours
    /// under a day, and "less than an hour" under that.
    /// </summary>
    public static string Describe(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
        {
            var days = (int)Math.Ceiling(remaining.TotalDays);
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (remaining.TotalHours >= 1)
        {
            var hours = (int)Math.Ceiling(remaining.TotalHours);
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        return "less than an hour";
    }
}
