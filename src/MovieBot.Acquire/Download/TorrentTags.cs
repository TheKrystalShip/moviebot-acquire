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
    /// Who asked for the film, so they can be told when it arrives. The account id follows the
    /// colon.
    /// </summary>
    public const string RequesterPrefix = "requester:";

    public static string Requester(ulong accountId) => $"{RequesterPrefix}{accountId}";

    /// <summary>Reads the account out of a requester tag, or null when it is not one.</summary>
    public static ulong? ReadRequester(string tag) =>
        tag.StartsWith(RequesterPrefix, StringComparison.Ordinal)
        && ulong.TryParse(tag[RequesterPrefix.Length..], out var accountId)
            ? accountId
            : null;

    /// <summary>
    /// The message showing this download's progress, as a channel and a message within it.
    ///
    /// Kept on the torrent like everything else, so a bot restarted during a download picks the
    /// same message back up rather than leaving one stuck at whatever it last said.
    /// </summary>
    public const string ProgressPrefix = "progress:";

    public static string Progress(ulong channelId, ulong messageId) =>
        $"{ProgressPrefix}{channelId}:{messageId}";

    /// <summary>Reads the message a progress tag points at, or null when it is not one.</summary>
    public static (ulong ChannelId, ulong MessageId)? ReadProgress(string tag)
    {
        if (!tag.StartsWith(ProgressPrefix, StringComparison.Ordinal)) return null;

        var parts = tag[ProgressPrefix.Length..].Split(':');
        return parts.Length == 2
               && ulong.TryParse(parts[0], out var channelId)
               && ulong.TryParse(parts[1], out var messageId)
            ? (channelId, messageId)
            : null;
    }

    /// <summary>
    /// The room the film is to play in once it can be watched, as the voice channel's id.
    ///
    /// Somebody who picks a film that is not here yet has asked to watch it, not to file it: the
    /// download is the means. The room is written on the torrent so that whichever pass sees the
    /// film become watchable, in whichever process is running by then, loads it into that room and
    /// hands the room the way in.
    /// </summary>
    public const string RoomPrefix = "room:";

    public static string Room(ulong voiceChannelId) => $"{RoomPrefix}{voiceChannelId}";

    /// <summary>Reads the voice channel out of a room tag, or null when it is not one.</summary>
    public static ulong? ReadRoom(string tag) =>
        tag.StartsWith(RoomPrefix, StringComparison.Ordinal)
        && ulong.TryParse(tag[RoomPrefix.Length..], out var voiceChannelId)
            ? voiceChannelId
            : null;

    /// <summary>
    /// The id the film goes under in the library, written by the process that named it there.
    ///
    /// A surface that wants to open the film once it is watchable needs to know which library
    /// entry the download became, and deriving that a second time from the release name is a
    /// guess where this is a fact: the two parsers would not disagree loudly, they would simply
    /// open nothing.
    /// </summary>
    public const string LibraryPrefix = "library:";

    public static string Library(string id) => $"{LibraryPrefix}{id}";

    /// <summary>Reads the library id out of a library tag, or null when it is not one.</summary>
    public static string? ReadLibrary(string tag) =>
        tag.StartsWith(LibraryPrefix, StringComparison.Ordinal)
        && tag.Length > LibraryPrefix.Length
            ? tag[LibraryPrefix.Length..]
            : null;

    /// <summary>
    /// Which film this is, as the tracker identified it. Carried so the library can hold the id
    /// rather than re-deriving it from a release name, which is a guess where this is a fact.
    /// </summary>
    public const string ImdbPrefix = "imdb:";

    public static string Imdb(string imdbId) => $"{ImdbPrefix}{imdbId}";

    /// <summary>Reads the film out of an imdb tag, or null when it is not one.</summary>
    public static string? ReadImdb(string tag) =>
        tag.StartsWith(ImdbPrefix, StringComparison.Ordinal)
        && ImdbId.IsValid(tag[ImdbPrefix.Length..])
            ? tag[ImdbPrefix.Length..]
            : null;

    /// <summary>
    /// Somebody wants this film to stay. A download wearing it is never pruned, however long it
    /// has seeded, until somebody takes it off again.
    ///
    /// On the torrent rather than in a file of its own, because a keep is only meaningful while
    /// the torrent exists and every process that decides a download's fate already reads these.
    /// </summary>
    public const string Keep = "keep";

    /// <summary>
    /// Who asked for the film to stay, as an account id after the colon. Anyone may keep a film
    /// and anyone may let it go, so the name is there to be shown rather than to be checked.
    /// </summary>
    public const string KeeperPrefix = "keeper:";

    public static string Keeper(ulong accountId) => $"{KeeperPrefix}{accountId}";

    /// <summary>Reads the account out of a keeper tag, or null when it is not one.</summary>
    public static ulong? ReadKeeper(string tag) =>
        tag.StartsWith(KeeperPrefix, StringComparison.Ordinal)
        && ulong.TryParse(tag[KeeperPrefix.Length..], out var accountId)
            ? accountId
            : null;

    /// <summary>
    /// The download still has to be turned into something the player can open. Carried from the
    /// moment it is started and removed only once that work has actually finished, so a download
    /// still wearing it is one whose transcode is owed — whether it has not begun, is running, or
    /// was interrupted partway.
    ///
    /// It says nothing about whether the film can be watched yet; <see cref="Watchable"/> says
    /// that. One tag answering both questions is what made an interrupted transcode unrecoverable:
    /// cleared the moment a film became playable, it was no longer there to say that the rest of
    /// the work was still owed, and nothing ever picked the film up again.
    /// </summary>
    public const string NeedsIngest = "ingest";

    /// <summary>
    /// There is enough of the film written that the player can open it.
    ///
    /// Set within seconds of a transcode starting, long before it ends, because that is when a
    /// person can begin watching. It is what a surface waits for before telling anybody the film
    /// is ready.
    /// </summary>
    public const string Watchable = "watchable";

    /// <summary>
    /// The film arrived but could not be made watchable. Distinct from simply not being done:
    /// without it, a failure is indistinguishable from a transcode still running, and the person
    /// who asked is told nothing at all.
    /// </summary>
    public const string IngestFailed = "ingest-failed";
}
