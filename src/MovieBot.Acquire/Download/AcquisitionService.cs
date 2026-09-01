using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// What happened when a film was asked for.
/// </summary>
/// <param name="Started">Whether the torrent client took it.</param>
/// <param name="Hash">The download's identity, for asking after it later.</param>
/// <param name="Refusal">
/// Why it was not started, in a sentence meant for the person who asked rather than for a log.
/// </param>
public sealed record AcquisitionResult(bool Started, string? Hash, string? Refusal)
{
    public static AcquisitionResult Refused(string reason) => new(false, null, reason);

    public static AcquisitionResult Accepted(string hash) => new(true, hash, null);
}

/// <summary>
/// Starting a film downloading: the one place the tracker and the torrent client meet.
///
/// The order matters. Disk is checked before the tracker is asked for anything, because the
/// alternative is spending a tracker call and a torrent file to then refuse; and the torrent is
/// fetched before the client is asked to take it, because a client that cannot be reached should
/// not leave a half-started download behind.
/// </summary>
public sealed class AcquisitionService(
    TrackerClient tracker,
    QBittorrentClient torrents,
    DiskBudget budget,
    IOptions<DownloadOptions> download,
    ILogger<AcquisitionService> logger)
{
    private readonly DownloadOptions _download = download.Value;

    /// <param name="tags">
    /// Notes to carry on the torrent itself, so a surface that wants to announce the download
    /// later does not have to remember it across its own restarts.
    /// </param>
    public async Task<AcquisitionResult> StartAsync(
        Release release, IEnumerable<string>? tags, CancellationToken ct)
    {
        var verdict = budget.CanAccept(release.SizeBytes);
        if (!verdict.Allowed)
        {
            logger.LogWarning("Refused {Release}: {Reason}", release.ReleaseName, verdict.Reason);
            return AcquisitionResult.Refused(verdict.Reason!);
        }

        // Asked before the tracker is, so a client that is not running is reported as itself
        // rather than as a torrent that vanished.
        if (!await torrents.IsReachableAsync(ct))
            return AcquisitionResult.Refused("The torrent client is not running.");

        byte[] torrentFile;
        try
        {
            torrentFile = await tracker.DownloadTorrentFileAsync(release.TorrentId, ct);
        }
        catch (TrackerException ex)
        {
            logger.LogWarning(ex, "The tracker would not give up {Id}.", release.TorrentId);
            return AcquisitionResult.Refused("The tracker would not hand over that torrent.");
        }

        try
        {
            var hash = await torrents.AddAsync(torrentFile, _download.ResolveRoot(), tags, ct);

            logger.LogInformation(
                "Started {Release} ({Size}) as {Hash}. {Budget}",
                release.ReleaseName, release.SizeDisplay, hash, verdict.Reading.Summary);

            return AcquisitionResult.Accepted(hash);
        }
        catch (QBittorrentException ex)
        {
            logger.LogWarning(ex, "The torrent client would not take {Release}.", release.ReleaseName);
            return AcquisitionResult.Refused("The torrent client would not take it.");
        }
    }

    /// <summary>Marks a note on a torrent as acted on, so it is not acted on twice.</summary>
    public Task ClearTagAsync(string hash, string tag, CancellationToken ct) =>
        torrents.RemoveTagAsync(hash, tag, ct);

    /// <summary>Leaves a note on a torrent for a later pass, or for another process entirely.</summary>
    public Task TagAsync(string hash, string tag, CancellationToken ct) =>
        torrents.AddTagAsync(hash, tag, ct);

    /// <summary>
    /// How many bytes of one file in a download are readable from its start without a gap.
    ///
    /// For anything reading the file while it is still arriving: the file is its full length from
    /// the moment it is created, so its size says nothing about how much of it is real.
    /// </summary>
    public Task<long> ReadableBytesAsync(string hash, string filePath, CancellationToken ct) =>
        torrents.ReadableBytesAsync(hash, filePath, ct);

    /// <summary>One download, or null when this process never started it.</summary>
    public Task<DownloadStatus?> StatusAsync(string hash, CancellationToken ct) =>
        torrents.GetAsync(hash, ct);

    /// <summary>Everything started through this project.</summary>
    public Task<IReadOnlyList<DownloadStatus>> ListAsync(CancellationToken ct) =>
        torrents.ListAsync(null, ct);

    /// <summary>What the download directory holds, for a surface that wants to show it.</summary>
    public BudgetReading Budget() => budget.Read();
}
