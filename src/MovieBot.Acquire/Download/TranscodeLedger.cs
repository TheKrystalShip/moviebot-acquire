using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// A transcode's claim on the disk it writes to, for as long as it runs.
/// </summary>
public sealed record TranscodeReservation
{
    /// <summary>What is being made: the download's hash, with the file's path for an episode.</summary>
    public required string Key { get; init; }

    /// <summary>The media root the transcode writes under.</summary>
    public required string Root { get; init; }

    /// <summary>The title's own directory under that root, which is what the claim is measured against.</summary>
    public required string TitleDirectory { get; init; }

    /// <summary>What the finished transcode is expected to take.</summary>
    public required long Bytes { get; init; }

    /// <summary>
    /// What the transcode has still to write. The part already on disk is in the volume's free
    /// space, so counting it again would hold back room that is already gone.
    /// </summary>
    public long OutstandingBytes() => Outstanding(Bytes, WrittenBytes());

    public static long Outstanding(long reservedBytes, long writtenBytes) =>
        Math.Max(0, reservedBytes - writtenBytes);

    private long WrittenBytes()
    {
        long written = 0;

        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(
                         TitleDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    written += new FileInfo(file).Length;
                }
                catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
                {
                    // A segment being replaced while the directory is walked is normal here.
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing written yet.
        }

        return written;
    }
}

[JsonSerializable(typeof(TranscodeReservation))]
internal partial class TranscodeLedgerJsonContext : JsonSerializerContext;

/// <summary>Where the ledger is kept.</summary>
public sealed class TranscodeLedgerOptions
{
    /// <summary>
    /// The media root, from <c>Media:Root</c> and, where that is blank, <c>Handoff:MediaRoot</c>:
    /// the two name one directory, and the ledger is a dotted directory under it. Blank means no
    /// ledger can be kept.
    /// </summary>
    public string MediaRoot { get; set; } = "";
}

/// <summary>
/// The transcodes now running and the room each has still to take, kept where every process that
/// spends the same disk can read it.
///
/// Downloads are started by the API and the bot, and transcodes by the hand-off, so a claim held
/// in one process's memory would be invisible to the others and each would count the free space as
/// its own. The claims are files instead, one per transcode, in a dotted directory under the media
/// root: the hand-off, the ledger's only writer, can always write the media root, and every process
/// that reads it already knows where the media root is. The dot is what keeps every listing of
/// titles from taking the directory for one.
///
/// A claim lives only while its transcode does. The hand-off holds none when it starts, so it
/// clears the ledger then, which is what removes the claim of a process that died mid-transcode.
///
/// A ledger that cannot be kept or read never blocks work. A missing directory is an empty ledger,
/// which is what a host with nothing running looks like. A directory that exists but cannot be read,
/// a media root that is not configured, and a claim that cannot be written are logged as warnings
/// and count as no claims: refusing downloads or transcodes over a bookkeeping failure is worse
/// than the free space being counted without it.
/// </summary>
public sealed class TranscodeLedger(IOptions<TranscodeLedgerOptions> options, ILogger<TranscodeLedger> logger)
{
    /// <summary>The directory the claims are in, under the media root.</summary>
    public const string FolderName = ".transcode-reservations";

    /// <summary>Whether the last read failed, so a ledger that stays unreadable is one line.</summary>
    private bool _reportedUnreadable;

    private string? Folder =>
        string.IsNullOrWhiteSpace(options.Value.MediaRoot)
            ? null
            : Path.Combine(Path.GetFullPath(options.Value.MediaRoot), FolderName);

    /// <summary>
    /// Records a transcode's claim, replacing any it already holds. Returns whether it was
    /// recorded; the caller carries on either way.
    /// </summary>
    public bool Reserve(TranscodeReservation reservation)
    {
        if (Folder is not { } folder)
        {
            logger.LogWarning("No media root is configured, so the transcode of {Key} holds no claim.", reservation.Key);
            return false;
        }

        try
        {
            System.IO.Directory.CreateDirectory(folder);

            var path = PathOf(folder, reservation.Key);
            var temporary = path + ".tmp";

            File.WriteAllText(temporary, JsonSerializer.Serialize(
                reservation, TranscodeLedgerJsonContext.Default.TranscodeReservation));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not record the claim of {Key} under {Folder}; it is made without one.",
                reservation.Key, folder);
            return false;
        }
    }

    /// <summary>Ends a transcode's claim, because it finished, failed or stopped.</summary>
    public void Release(string key)
    {
        if (Folder is not { } folder) return;

        try
        {
            File.Delete(PathOf(folder, key));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A claim that stays holds back room until the hand-off next starts, which is the
            // safe direction to be wrong in.
        }
    }

    /// <summary>Ends every claim.</summary>
    public void Clear()
    {
        if (Folder is not { } folder || !System.IO.Directory.Exists(folder)) return;

        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(folder))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Read again next time; an entry that cannot be deleted is still a claim.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not clear the claims under {Folder}.", folder);
        }
    }

    /// <summary>Every transcode holding a claim now.</summary>
    public IReadOnlyList<TranscodeReservation> List()
    {
        if (Folder is not { } folder)
        {
            ReportUnreadable("No media root is configured");
            return [];
        }

        if (!System.IO.Directory.Exists(folder)) return [];

        var found = new List<TranscodeReservation>();

        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(folder, "*.json"))
            {
                try
                {
                    var reservation = JsonSerializer.Deserialize(
                        File.ReadAllText(file), TranscodeLedgerJsonContext.Default.TranscodeReservation);
                    if (reservation is not null) found.Add(reservation);
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // Released between listing and reading, or being replaced: next read has it.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportUnreadable($"{folder} could not be read: {ex.Message}");
            return [];
        }

        _reportedUnreadable = false;
        return found;
    }

    /// <summary>Whether any transcode is writing under this media root.</summary>
    public bool IsWritingUnder(string root) =>
        List().Any(r => string.Equals(r.Root, root, StringComparison.Ordinal));

    /// <summary>Whether a transcode is making this title, under whichever root.</summary>
    public bool IsMaking(string titleId) =>
        List().Any(r => string.Equals(Path.GetFileName(r.TitleDirectory), titleId, StringComparison.Ordinal));

    private void ReportUnreadable(string why)
    {
        if (_reportedUnreadable) return;

        logger.LogWarning("{Why}; the transcodes running are counted as holding no room.", why);
        _reportedUnreadable = true;
    }

    private static string PathOf(string folder, string key) =>
        Path.Combine(folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32] + ".json");
}
