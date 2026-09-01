namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// What a download is doing, reduced to the states somebody waiting for a film can act on.
///
/// The torrent client distinguishes a dozen and a half states, most of which differ in ways that
/// change nothing for the person who asked for the film. What matters is whether it is coming,
/// whether it has arrived, and whether it has gone wrong in a way that needs somebody.
/// </summary>
public enum DownloadState
{
    /// <summary>Asking the tracker for peers. No data is moving yet and that is normal.</summary>
    Starting,

    /// <summary>Data is arriving.</summary>
    Downloading,

    /// <summary>Connected to nobody. It may recover on its own, and it may not.</summary>
    Stalled,

    /// <summary>On disk and complete. Still seeding, because a private tracker expects it.</summary>
    Complete,

    /// <summary>Deliberately stopped.</summary>
    Paused,

    /// <summary>Wrong in a way that will not fix itself.</summary>
    Failed,
}

/// <summary>One download, as a surface shows it.</summary>
public sealed record DownloadStatus
{
    public required string Hash { get; init; }

    /// <summary>The release name, which is what the torrent is called on disk.</summary>
    public required string Name { get; init; }

    public required DownloadState State { get; init; }

    /// <summary>Completion, from 0 to 1.</summary>
    public double Progress { get; init; }

    public long SizeBytes { get; init; }

    public long BytesPerSecond { get; init; }

    /// <summary>
    /// How long the client thinks is left, or null when it has no useful estimate. The client
    /// reports a placeholder rather than nothing, and showing that placeholder as a duration
    /// produces a film arriving in a hundred years.
    /// </summary>
    public TimeSpan? Remaining { get; init; }

    /// <summary>
    /// The film on disk. It is only meaningful once the download is complete: before that the
    /// path exists but the file behind it is full of holes.
    /// </summary>
    public string ContentPath { get; init; } = "";

    /// <summary>Whether the client is fetching pieces in order, which is what makes a partial file playable.</summary>
    public bool IsSequential { get; init; }

    /// <summary>
    /// Peers with the whole file that this is connected to.
    ///
    /// Carried because it is the answer to the only question a slow download raises. A download
    /// sitting at a third for ten minutes is either working or abandoned, and the seed count is
    /// what tells the two apart.
    /// </summary>
    public int Seeds { get; init; }

    /// <summary>
    /// Notes carried on the torrent itself rather than by whoever started it.
    ///
    /// A surface that wants to say something when a download finishes has to remember where to
    /// say it, and remembering it in the process that started the download loses it on a restart
    /// — which is exactly when a long download is still running. The torrent client already
    /// persists the torrent, so the note rides along with it.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsFinished => State == DownloadState.Complete;

    /// <summary>A short line for a chat surface.</summary>
    public string Summary => State switch
    {
        DownloadState.Complete => "Complete",
        DownloadState.Failed => "Failed",
        DownloadState.Paused => "Paused",
        DownloadState.Starting => "Looking for peers",
        DownloadState.Stalled => $"Stalled at {Progress:P0}",
        _ => Remaining is { } left
            ? $"{Progress:P0}, about {Describe(left)} left"
            : $"{Progress:P0}",
    };

    private static string Describe(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes} min"
            : "under a minute";
}
