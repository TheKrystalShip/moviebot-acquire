namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// Where acquired films land, and how much of the disk they may hold.
/// </summary>
public sealed class DownloadOptions
{
    public const string Section = "Download";

    /// <summary>
    /// The directory the torrent client writes into. It is also the directory the ingest reads
    /// from, and the directory a completed film keeps being seeded out of.
    ///
    /// Empty means <c>~/Downloads/Movies</c>, resolved at startup rather than baked in, so the
    /// path is right whichever account the service runs as.
    /// </summary>
    public string Root { get; set; } = "";

    /// <summary>
    /// The most the download directory may hold, in gibibytes.
    ///
    /// It is a real ceiling rather than a guideline: a private tracker expects a completed
    /// download to keep seeding, so nothing here is deleted on a schedule and the directory only
    /// ever grows until somebody removes a film.
    /// </summary>
    public double MaximumGiB { get; set; } = 300;

    /// <summary>
    /// The point at which the budget is reported as running out, in gibibytes. Crossing it
    /// changes nothing on its own; it is the last chance to remove something before a download
    /// is refused.
    /// </summary>
    public double WarningGiB { get; set; } = 250;

    /// <summary>The download directory, with the default resolved.</summary>
    public string ResolveRoot() =>
        string.IsNullOrWhiteSpace(Root)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "Movies")
            : Path.GetFullPath(Root);
}
