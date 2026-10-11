using System.Globalization;
using System.Text.RegularExpressions;
using TheKrystalShip.MovieBot.Acquire.Search;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>A room waiting on a download, and the episode of it the room wants when it is a show's.</summary>
public readonly record struct WatchTag(string Room, EpisodeNumber? Episode);

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
public static partial class TorrentTags
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
    /// Who asked for the film, so they can be told when it arrives. The account follows the colon:
    /// the <c>sub</c> the identity provider names the person by, a string of any shape. A comma
    /// separates tags in the torrent client, so a comma and the percent sign that escapes it are
    /// percent-encoded, and nothing else is.
    /// </summary>
    public const string RequesterPrefix = "requester:";

    public static string Requester(string accountId) =>
        RequesterPrefix + accountId.Replace("%", "%25", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);

    /// <summary>Reads the account out of a requester tag, or null when it is not one.</summary>
    public static string? ReadRequester(string tag) =>
        tag.StartsWith(RequesterPrefix, StringComparison.Ordinal) && tag.Length > RequesterPrefix.Length
            ? tag[RequesterPrefix.Length..].Replace("%2C", ",", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal)
            : null;

    /// <summary>
    /// The Discord user the bot mentions when it announces the film in a channel. Written by the bot,
    /// which asked for the film on that person's behalf and is the one surface that knows them by a
    /// Discord id; nothing else reads it.
    /// </summary>
    public const string MentionPrefix = "mention:";

    public static string Mention(ulong discordUserId) => $"{MentionPrefix}{discordUserId}";

    /// <summary>Reads the Discord user out of a mention tag, or null when it is not one.</summary>
    public static ulong? ReadMention(string tag) =>
        tag.StartsWith(MentionPrefix, StringComparison.Ordinal)
        && ulong.TryParse(tag[MentionPrefix.Length..], out var userId)
            ? userId
            : null;

    /// <summary>
    /// The person who asked is owed a notification once the film can be watched, or once it could
    /// not be made watchable. Written when the download starts and removed once they have been
    /// told, so a download that never carried it is one nobody is owed news of.
    /// </summary>
    public const string Tell = "tell";

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
    /// The site room the film is to play in once it can be watched, by the room's own id.
    ///
    /// The site's counterpart of <see cref="RoomPrefix"/>: somebody pressed Watch now on a film that
    /// is not here, and a room is waiting on it. A site room has no Discord channel, so it is the
    /// API that loads the film into it rather than the bot. Room ids are short and drawn from
    /// letters, digits, <c>-</c> and <c>_</c>, which a tag carries as they are.
    /// </summary>
    public const string WatchPrefix = "watch:";

    /// <summary>
    /// The room, and for a show's torrent the episode the room waits on, <c>watch:a1b2c3d4:s05e03</c>:
    /// one torrent holds a season, an episode of it may be here already, and the room is waiting on
    /// the one somebody pressed.
    /// </summary>
    public static string Watch(string roomId, EpisodeNumber? episode = null) =>
        episode is { } e ? $"{WatchPrefix}{roomId}:{Code(e)}" : $"{WatchPrefix}{roomId}";

    /// <summary>Reads a watch tag, or null when it is not one or names no usable room.</summary>
    public static WatchTag? ReadWatch(string tag)
    {
        if (!tag.StartsWith(WatchPrefix, StringComparison.Ordinal)) return null;

        var parts = tag[WatchPrefix.Length..].Split(':');
        if (!IsRoomId(parts[0])) return null;
        if (parts.Length == 1) return new WatchTag(parts[0], null);

        return parts.Length == 2 && ReadEpisode(parts[1]) is { } episode
            ? new WatchTag(parts[0], episode)
            : null;
    }

    /// <summary>
    /// Why a file of a show's torrent is fetched when no room is waiting on it, <c>keep:s02e04</c>:
    /// somebody follows the show, a device wants the episode, it is the next one fetched ahead of a
    /// room, or it was asked for outright. A bare <c>keep</c> is a torrent fetched whole. A file
    /// only a room's <see cref="Watch"/> names is fetched for that room alone, and is let go of
    /// once the room moves past it (<see cref="Passed"/>).
    /// </summary>
    public const string KeepPrefix = "keep";

    public static string Keep(EpisodeNumber? episode = null) =>
        episode is { } e ? $"{KeepPrefix}:{Code(e)}" : KeepPrefix;

    /// <summary>Whether a tag keeps <paramref name="episode"/>: its own <c>keep:</c>, or a bare <c>keep</c>.</summary>
    public static bool Keeps(string tag, EpisodeNumber episode) =>
        tag == KeepPrefix || tag == Keep(episode);

    /// <summary>
    /// A room was waiting on this episode and moved past it before it could play,
    /// <c>passed:s02e04</c>. Left by whoever saw the room move; the hand-off, which knows whether
    /// the episode's transcode has begun, switches the file off when nothing else wants it and
    /// clears the tag either way.
    /// </summary>
    public const string PassedPrefix = "passed:";

    public static string Passed(EpisodeNumber episode) => $"{PassedPrefix}{Code(episode)}";

    /// <summary>Reads the episode out of a passed tag, or null when it is not one.</summary>
    public static EpisodeNumber? ReadPassed(string tag) =>
        tag.StartsWith(PassedPrefix, StringComparison.Ordinal) ? ReadEpisode(tag[PassedPrefix.Length..]) : null;

    /// <summary>
    /// Every file this torrent fetches was switched on by a pick that named why on the torrent, a
    /// <see cref="Watch"/> or a <see cref="Keep"/>. Only such a torrent has a file switched off
    /// when a room moves past it: in one without it, a file may be fetched for a reason nothing
    /// recorded.
    /// </summary>
    public const string PicksNamed = "picks-named";

    private static string Code(EpisodeNumber e) =>
        string.Create(CultureInfo.InvariantCulture, $"s{e.Season:00}e{e.Episode:00}");

    private static EpisodeNumber? ReadEpisode(string code) =>
        EpisodeTag().Match(code) is { Success: true } m
            ? new EpisodeNumber(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
            : null;

    [GeneratedRegex(@"^s(\d{1,3})e(\d{1,4})$")]
    private static partial Regex EpisodeTag();

    /// <summary>Whether a room id can ride on a tag: 1 to 64 letters, digits, <c>-</c> or <c>_</c>.</summary>
    public static bool IsRoomId(string roomId) =>
        roomId.Length is > 0 and <= 64
        && roomId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

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
