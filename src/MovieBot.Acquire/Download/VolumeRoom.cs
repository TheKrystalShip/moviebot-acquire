namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// What is left of a volume once everything already promised a share of it has taken that share.
///
/// Free space is what the filesystem says now. Three things will still take some of it: the
/// transcodes that have not finished writing, the downloads that have not finished arriving, and
/// nothing else is counted. A volume read this way is the one figure both the download budget and
/// a transcode's placement are judged against, so neither believes the free space is its own.
/// </summary>
/// <param name="FreeBytes">What the filesystem reports as available.</param>
/// <param name="TranscodeOutstandingBytes">What the running transcodes writing here have still to write.</param>
/// <param name="PendingDownloadBytes">
/// What the downloads still arriving here have still to fetch; zero for a volume the downloads are
/// not on.
/// </param>
public sealed record VolumeRoom(long FreeBytes, long TranscodeOutstandingBytes, long PendingDownloadBytes)
{
    /// <summary>
    /// What is kept free past any claim. A volume filling exactly to its last block is how a
    /// filesystem stops being able to record that it is full.
    /// </summary>
    public const long MarginBytes = 1L << 30;

    public long AvailableBytes =>
        Math.Max(0, FreeBytes - TranscodeOutstandingBytes - PendingDownloadBytes);

    /// <summary>Whether a transcode of this size can be written here without eating into a claim or the margin.</summary>
    public bool Fits(long bytes) => bytes + MarginBytes <= AvailableBytes;
}

/// <summary>Which volume a path is on.</summary>
public static class Volumes
{
    /// <summary>
    /// The mount point a path is under: the longest mount point that is a parent of it. A path on
    /// a volume of its own and a path on the system disk share the root "/", and only the longer
    /// mount point tells them apart.
    /// </summary>
    public static string MountOf(string path, IEnumerable<string> mountPoints)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var best = "/";

        foreach (var mount in mountPoints)
        {
            var candidate = Path.TrimEndingDirectorySeparator(mount);
            if (candidate.Length == 0 || candidate.Length <= best.Length) continue;

            if (full.Equals(candidate, StringComparison.Ordinal)
                || full.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                best = candidate;
            }
        }

        return best;
    }

    public static string MountOf(string path) =>
        MountOf(path, DriveInfo.GetDrives().Select(d => d.Name));

    public static bool Same(string a, string b, IEnumerable<string> mountPoints) =>
        MountOf(a, mountPoints) == MountOf(b, mountPoints);
}
