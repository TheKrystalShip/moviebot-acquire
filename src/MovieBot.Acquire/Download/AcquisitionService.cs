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

    /// <summary>Starts the whole of a release downloading.</summary>
    public Task<AcquisitionResult> StartAsync(
        Release release, IEnumerable<string>? tags, CancellationToken ct) =>
        StartAsync(release, null, tags, ct);

    /// <summary>
    /// Starts a release downloading, or only some of a show's episodes out of it.
    ///
    /// Picked episodes are the only files fetched: the torrent is added stopped, every other file
    /// is switched off, and it is started. The tracker accounts for the torrent whichever files
    /// are fetched, so the seeding rule is the same as for a whole one. Picking from a torrent the
    /// client already holds switches the newly picked files on in it.
    /// </summary>
    /// <param name="episodes">The episodes wanted; null or empty fetches the whole release.</param>
    /// <param name="tags">
    /// Notes to carry on the torrent itself, so a surface that wants to announce the download
    /// later does not have to remember it across its own restarts.
    /// </param>
    public async Task<AcquisitionResult> StartAsync(
        Release release, IReadOnlyCollection<EpisodeNumber>? episodes,
        IEnumerable<string>? tags, CancellationToken ct)
    {
        var picking = release.IsEpisodic && episodes is { Count: > 0 };

        // An estimate from the release's average episode until the torrent names each file's size.
        var estimate = picking
            ? Math.Min(release.SizeBytes, release.SizePerEpisode * episodes!.Count)
            : release.SizeBytes;

        var verdict = budget.CanAccept(estimate);
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

        if (!picking)
        {
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

        return await StartPickedAsync(release, torrentFile, episodes!, tags, ct);
    }

    /// <summary>Delay between asking a just-added torrent for its files, and how often to ask.</summary>
    private static readonly TimeSpan FilesPoll = TimeSpan.FromMilliseconds(250);
    private const int FilesPolls = 40;

    private async Task<AcquisitionResult> StartPickedAsync(
        Release release, byte[] torrentFile, IReadOnlyCollection<EpisodeNumber> episodes,
        IEnumerable<string>? tags, CancellationToken ct)
    {
        string hash;
        HashSet<string> wanted;
        try
        {
            hash = Bencode.InfoHash(torrentFile);
            var files = Bencode.Files(torrentFile);
            var picked = EpisodeFiles.Map(files.Select(f => (f.Path, f.Size)), release.Episodic)
                .Where(f => f.Episodes.Any(episodes.Contains))
                .ToList();
            if (picked.Count == 0)
                return AcquisitionResult.Refused("That release holds none of the episodes asked for.");

            // The real size, now the torrent names each file's.
            var verdict = budget.CanAccept(picked.Sum(f => f.Size));
            if (!verdict.Allowed)
            {
                logger.LogWarning("Refused {Release}: {Reason}", release.ReleaseName, verdict.Reason);
                return AcquisitionResult.Refused(verdict.Reason!);
            }

            wanted = picked.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "The torrent for {Release} could not be read.", release.ReleaseName);
            return AcquisitionResult.Refused("The torrent for that release could not be read.");
        }

        var held = await torrents.GetAsync(hash, ct) is not null;
        try
        {
            if (!held)
                await torrents.AddAsync(torrentFile, _download.ResolveRoot(), tags, ct, stopped: true);
            else
                foreach (var tag in tags ?? [])
                    await torrents.AddTagAsync(hash, tag, ct);

            // The client reads the torrent's metadata as it adds it, so the file list arrives a
            // moment after the add returns rather than with it.
            IReadOnlyList<QBittorrentFile> listed = [];
            for (var poll = 0; poll < FilesPolls && listed.Count == 0; poll++)
            {
                listed = await torrents.FilesAsync(hash, ct);
                if (listed.Count == 0) await Task.Delay(FilesPoll, ct);
            }
            if (listed.Count == 0)
                throw new QBittorrentException("The torrent client never listed the torrent's files.");

            var on = listed.Where(f => wanted.Contains(f.Name)).Select(f => f.Index).ToList();
            if (on.Count == 0)
                throw new QBittorrentException("The torrent client names the torrent's files differently.");

            await torrents.SetFilePriorityAsync(hash, on, 1, ct);
            if (!held)
            {
                await torrents.SetFilePriorityAsync(
                    hash, listed.Where(f => !wanted.Contains(f.Name)).Select(f => f.Index).ToList(), 0, ct);
                await torrents.StartAsync(hash, ct);
            }

            logger.LogInformation(
                "Started {Count} episodes of {Release} as {Hash}{Held}.",
                on.Count, release.ReleaseName, hash, held ? ", which the client already held" : "");

            return AcquisitionResult.Accepted(hash);
        }
        catch (QBittorrentException ex)
        {
            logger.LogWarning(ex, "The torrent client would not take {Release}.", release.ReleaseName);

            // A torrent added stopped and never started would sit in the client fetching nothing.
            if (!held)
            {
                try { await torrents.DeleteAsync(hash, ct); }
                catch (QBittorrentException) { }
            }

            return AcquisitionResult.Refused("The torrent client would not take it.");
        }
    }

    /// <summary>
    /// The files of one download, with what is being fetched of each and how much has arrived:
    /// for a show's torrent, which episodes are coming and how far along each is.
    /// </summary>
    public Task<IReadOnlyList<QBittorrentFile>> FilesAsync(string hash, CancellationToken ct) =>
        torrents.FilesAsync(hash, ct);

    /// <summary>Marks a note on a torrent as acted on, so it is not acted on twice.</summary>
    public Task ClearTagAsync(string hash, string tag, CancellationToken ct) =>
        torrents.RemoveTagAsync(hash, tag, ct);

    /// <summary>Leaves a note on a torrent for a later pass, or for another process entirely.</summary>
    public Task TagAsync(string hash, string tag, CancellationToken ct) =>
        torrents.AddTagAsync(hash, tag, ct);

    /// <summary>
    /// Takes a download out of the client and off the disk. The films it was turned into are
    /// whoever calls this to deal with; this only knows about the source.
    /// </summary>
    public Task RemoveAsync(string hash, CancellationToken ct) =>
        torrents.DeleteAsync(hash, ct);

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
