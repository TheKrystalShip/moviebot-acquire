namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>One video file in a show's torrent, and the episodes it holds.</summary>
/// <param name="Path">The file's path inside the torrent, as the torrent client names it.</param>
/// <param name="Episodes">Usually one; a double episode is a single file holding two.</param>
public sealed record EpisodeFile(string Path, long Size, IReadOnlyList<EpisodeNumber> Episodes)
{
    /// <summary>The episode the file is filed under: its first.</summary>
    public EpisodeNumber Episode => Episodes[0];
}

/// <summary>
/// Which file in a show's torrent is which episode.
///
/// Both the download, choosing which files to fetch, and the hand-off, choosing what to turn into
/// which title, read it here. Two readings of the same torrent would not disagree loudly: one
/// would fetch an episode the other never ingests.
/// </summary>
public static class EpisodeFiles
{
    /// <summary>The containers the ingest reads.</summary>
    public static readonly IReadOnlyList<string> VideoExtensions =
        [".mkv", ".mp4", ".avi", ".m4v", ".mov", ".ts", ".m2ts", ".wmv"];

    /// <summary>
    /// Words that name a file as something other than an episode. Matched against the file name
    /// only, because a directory named for the release can contain any of them.
    /// </summary>
    private static readonly string[] NotAnEpisode =
        ["sample", "trailer", "featurette", "extra", "behind.the.scenes", "deleted"];

    /// <summary>
    /// The episodes among a torrent's files, in episode order.
    ///
    /// A file is placed by the marker in its own name, which every pack measured names its files
    /// with. A file without one takes the torrent's season and its place among the video files in
    /// name order, which is how a pack that names its files only by number is laid out; a torrent
    /// that places nothing in a show places none of its files either.
    /// </summary>
    /// <param name="files">Every file in the torrent, by path and size.</param>
    /// <param name="torrent">The marker on the torrent's own name.</param>
    public static IReadOnlyList<EpisodeFile> Map(
        IEnumerable<(string Path, long Size)> files, EpisodeMarker? torrent)
    {
        var videos = files
            .Where(f => IsEpisode(f.Path))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        var mapped = new List<EpisodeFile>();
        var position = 0;
        foreach (var (path, size) in videos)
        {
            position++;
            var marker = EpisodeMarker.Read(System.IO.Path.GetFileName(path));

            if (marker is { IsSeasonPack: false })
                mapped.Add(new EpisodeFile(path, size, marker.EpisodeNumbers.ToList()));
            else if (torrent is { } whole && whole.LastSeason is null)
                mapped.Add(new EpisodeFile(path, size,
                    [whole.IsSeasonPack ? new EpisodeNumber(whole.Season, position)
                        : new EpisodeNumber(whole.Season, whole.Episodes[Math.Min(position, whole.Episodes.Count) - 1])]));
        }

        // Two files claiming one episode is a pack carrying a second cut of it; the larger is kept.
        return mapped
            .GroupBy(f => f.Episode)
            .Select(g => g.MaxBy(f => f.Size)!)
            .OrderBy(f => f.Episode)
            .ToList();
    }

    private static bool IsEpisode(string path)
    {
        if (!VideoExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return false;

        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        return !NotAnEpisode.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));
    }
}
