using Microsoft.Extensions.Options;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// When a downloaded torrent has done what the tracker demands of it and may be let go.
///
/// The tracker lets a torrent be closed once it has seeded for its minimum time or reached its
/// ratio, whichever comes first. A torrent removed here is gone with its files, never resumed, so
/// either is the end of what is owed and the source goes as soon as one is met: the disk is what
/// the next film needs, and nothing reads a source once its transcode is made.
///
/// Time is measured on the client's own seeding clock, which stops while the machine is off or the
/// torrent is stopped. That is the clock the tracker credits, so two days here are two days of
/// seeding and not two on the calendar.
/// </summary>
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    /// <summary>The seeding the tracker demands of a torrent that has not reached its ratio, in hours.</summary>
    public double TrackerMinimumHours { get; set; } = 48;

    /// <summary>
    /// Added to the tracker's minimum before anything is removed on time alone. The tracker counts
    /// by the announces it received and the client counts by the time it ran, and the two disagree
    /// by whatever announces did not arrive, always in the direction of the tracker crediting less.
    /// </summary>
    public double MarginHours { get; set; } = 12;

    /// <summary>The ratio, uploaded over downloaded, at which the tracker lets a torrent go whatever its seeding time.</summary>
    public double TrackerRatio { get; set; } = 1;

    /// <summary>
    /// Added to the tracker's ratio before anything is removed on ratio alone. The tracker hears of
    /// an upload at the next announce, so what the client has sent since the last one is not yet
    /// credited, and the margin covers it.
    /// </summary>
    public double RatioMargin { get; set; } = 0.1;

    /// <summary>
    /// The least seeding, in hours, before a torrent that has reached its ratio goes. The tracker
    /// asks this much of any torrent closed after its download, and it gives the upload a few
    /// announces to be credited by.
    /// </summary>
    public double RatioMinimumHours { get; set; } = 3;

    /// <summary>The seeding after which a download goes whatever its ratio.</summary>
    public TimeSpan Floor => TimeSpan.FromHours(TrackerMinimumHours + MarginHours);

    /// <summary>The ratio at which a download goes once it has seeded <see cref="RatioMinimum"/>.</summary>
    public double RatioFloor => TrackerRatio + RatioMargin;

    public TimeSpan RatioMinimum => TimeSpan.FromHours(RatioMinimumHours);

    /// <summary>
    /// None may be negative, and the tracker's ratio is above zero: a ratio of zero is met by every
    /// download the moment it completes.
    /// </summary>
    public bool IsCoherent => TrackerMinimumHours >= 0 && MarginHours >= 0 && TrackerRatio > 0
                              && RatioMargin >= 0 && RatioMinimumHours >= 0;
}

/// <summary>
/// The retention rule, asked one download at a time.
///
/// It decides nothing about running transcodes or occupied rooms: those are facts the caller holds
/// and checks. What this answers is only whether a download has done what the tracker demands.
/// </summary>
public sealed class Retention(IOptions<RetentionOptions> options)
{
    private readonly RetentionOptions _options = options.Value;

    public TimeSpan Floor => _options.Floor;

    public double RatioFloor => _options.RatioFloor;

    /// <summary>
    /// Whether a finished download has seeded past the floor, or reached the ratio floor after the
    /// least seeding. A download with nothing downloaded has no ratio and goes on time alone.
    /// </summary>
    public bool IsDue(DownloadStatus download) =>
        download.IsFinished
        && (download.Seeded >= _options.Floor
            || (download.Ratio is { } ratio && ratio >= _options.RatioFloor
                && download.Seeded >= _options.RatioMinimum));
}
