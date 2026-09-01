namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// The notes processes leave on a torrent for each other.
///
/// A download outlives any one process working on it, and more than one process acts on the same
/// download: one starts it, another turns it into something watchable, a third announces it. The
/// torrent client already persists the torrent, so the state of that work is kept on the torrent
/// rather than in any of them — which is what makes a restart mid-download survivable.
///
/// The vocabulary lives here, in the library they all reference, because the failure of two
/// processes spelling the same idea differently is silent: nothing throws, the tag is simply
/// never seen and the work never happens.
/// </summary>
public static class TorrentTags
{
    /// <summary>
    /// Where to announce the film, once there is something to announce. The channel id follows
    /// the colon.
    /// </summary>
    public const string NotifyPrefix = "notify:";

    public static string Notify(ulong channelId) => $"{NotifyPrefix}{channelId}";

    /// <summary>Reads the channel out of a notify tag, or null when it is not one.</summary>
    public static ulong? ReadNotifyChannel(string tag) =>
        tag.StartsWith(NotifyPrefix, StringComparison.Ordinal)
        && ulong.TryParse(tag[NotifyPrefix.Length..], out var channelId)
            ? channelId
            : null;

    /// <summary>
    /// The download still has to be turned into something the player can open. Carried from the
    /// moment it is started and removed once that work is finished, so a complete download still
    /// wearing it is one nobody can watch yet.
    /// </summary>
    public const string NeedsIngest = "ingest";

    /// <summary>
    /// The film arrived but could not be made watchable. Distinct from simply not being done:
    /// without it, a failure is indistinguishable from a transcode still running, and the person
    /// who asked is told nothing at all.
    /// </summary>
    public const string IngestFailed = "ingest-failed";
}
