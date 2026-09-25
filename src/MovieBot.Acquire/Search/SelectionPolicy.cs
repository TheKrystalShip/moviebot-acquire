namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// What is worth downloading, expressed as the pipeline's constraints rather than as taste.
/// </summary>
public sealed class SelectionPolicy
{
    public const string Section = "Selection";

    /// <summary>
    /// The best resolution worth taking. Anything above it is not offered at all.
    ///
    /// The ingest transcodes to H.264 at a fixed bitrate and tone-maps HDR to SDR, so a 2160p
    /// source reaches the player as the same rendition a 1080p source does, having cost several
    /// times the download, the disk it is then seeded from, and a slower transcode.
    /// </summary>
    public Resolution MaximumResolution { get; set; } = Resolution.Hd1080;

    /// <summary>The resolution to aim for when several are available.</summary>
    public Resolution PreferredResolution { get; set; } = Resolution.Hd1080;

    /// <summary>
    /// The largest release worth taking, in gibibytes. Seeding means the file stays on disk after
    /// the night it was watched, so the ceiling is a disk budget and not a bandwidth one.
    /// </summary>
    public double MaximumSizeGiB { get; set; } = 25;

    /// <summary>
    /// The fewest seeders a release may have and still be offered. A release with none cannot be
    /// downloaded at all, and showing it produces a request that never completes.
    /// </summary>
    public int MinimumSeeders { get; set; } = 1;

    /// <summary>
    /// The tracker categories a film may come from, spelled as the tracker spells them. A title
    /// search matches soundtracks and other non-film categories, so the filter is an allow-list
    /// rather than a set of exclusions; an empty list filters nothing.
    ///
    /// It has no default because the names are the tracker's own taxonomy, and which tracker this
    /// is belongs to the host's configuration. <see cref="AcquireServiceCollectionExtensions.AddAcquire"/>
    /// refuses to start without it, so a host that forgot it does not quietly offer soundtracks.
    /// </summary>
    public string[] AllowedCategories { get; set; } = [];

    /// <summary>
    /// Whether stereoscopic releases are offered. They are not, by default: the player has no
    /// stereoscopic mode, so a side-by-side encode arrives as two squashed half-width images.
    /// </summary>
    public bool AllowThreeDimensional { get; set; }

    /// <summary>How many candidates a search offers.</summary>
    public int MaximumResults { get; set; } = 10;

    /// <summary>
    /// Whether freeleech and internal releases are ordered ahead of everything else outright,
    /// rather than merely weighted.
    ///
    /// Turned on, the order is freeleech, then internal, then the rest, with the encode deciding
    /// only within each of those groups. It is what keeps the account's ratio healthy, and the
    /// case it costs is a freeleech 720p sitting above an internal 1080p. Turned off, every
    /// factor is weighed together and the better encode can come first.
    /// </summary>
    public bool TierByTrackerFlags { get; set; } = true;
}
