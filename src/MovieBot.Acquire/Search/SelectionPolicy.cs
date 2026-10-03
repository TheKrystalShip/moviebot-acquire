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
    /// The tracker categories a show may come from, spelled as the tracker spells them, kept apart
    /// from the films' so a search for one never offers the other. Empty offers no show at all,
    /// since these are what say a release is a show's. Host configuration, like the films' list.
    /// </summary>
    public string[] AllowedShowCategories { get; set; } = [];

    /// <summary>
    /// The resolution to aim for in a show. Lower than a film's: a season is many hours, and 720p
    /// is a third of 1080p's size for a picture the ladder serves at 720p anyway on most screens.
    /// A person may still ask for 1080p.
    /// </summary>
    public Resolution PreferredShowResolution { get; set; } = Resolution.Hd720;

    /// <summary>
    /// The largest an episode may be, in gibibytes, averaged over a pack. A season is judged per
    /// episode because a pack's total says how long the season is, not how heavy the encode.
    /// </summary>
    public double MaximumEpisodeSizeGiB { get; set; } = 8;

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
