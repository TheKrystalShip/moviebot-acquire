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
    /// It is passed on every add rather than left to the torrent client's own default, so this
    /// setting is the authority for anything this project starts and the two cannot drift into
    /// disagreeing about where a film is.
    ///
    /// Empty falls back to the account's downloads directory with <see cref="FolderName"/> under
    /// it. Set it explicitly for a service: a fallback that reads the environment agrees with
    /// another process only for as long as both are launched the same way, and a service is not
    /// launched like a session.
    /// </summary>
    public string Root { get; set; } = "";

    /// <summary>The directory under the downloads directory that films are kept in.</summary>
    public string FolderName { get; set; } = "Movies";

    /// <summary>
    /// The most the download directory may hold, in gibibytes.
    ///
    /// It is a real ceiling rather than a guideline. A completed download keeps seeding until
    /// the retention rule lets it go, so between one film being pruned and the next the directory
    /// only grows, and a run of requests inside one window has to fit under this.
    /// </summary>
    public double MaximumGiB { get; set; } = 300;

    /// <summary>
    /// The point at which the budget is reported as running out, in gibibytes. Crossing it
    /// changes nothing on its own; it is the last chance to remove something before a download
    /// is refused.
    /// </summary>
    public double WarningGiB { get; set; } = 250;

    /// <summary>The download directory, with the fallback resolved.</summary>
    public string ResolveRoot() =>
        string.IsNullOrWhiteSpace(Root)
            ? Path.Combine(DownloadsDirectory(), FolderName)
            : Path.GetFullPath(Root);

    /// <summary>
    /// The account's downloads directory, per the XDG user directories specification.
    ///
    /// The environment variable is preferred because a session that has one has already expanded
    /// it. The configuration file is read only when it is absent, which is the case for every
    /// service: a systemd unit inherits none of a desktop session's variables, so a service
    /// relying on the variable alone silently resolves somewhere else than the session that set
    /// the directory up.
    /// </summary>
    private static string DownloadsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var fromEnvironment = Environment.GetEnvironmentVariable("XDG_DOWNLOAD_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Path.IsPathRooted(fromEnvironment))
            return fromEnvironment;

        var fromFile = ReadUserDirectory(home, "XDG_DOWNLOAD_DIR");
        if (fromFile is not null) return fromFile;

        return Path.Combine(home, "Downloads");
    }

    /// <summary>
    /// Reads one entry out of the XDG user-directories file, which quotes its values and writes
    /// the home directory as an unexpanded <c>$HOME</c>.
    /// </summary>
    private static string? ReadUserDirectory(string home, string key)
    {
        var path = Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } configHome
                ? configHome
                : Path.Combine(home, ".config"),
            "user-dirs.dirs");

        try
        {
            if (!File.Exists(path)) return null;

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith(key + "=", StringComparison.Ordinal)) continue;

                var value = trimmed[(key.Length + 1)..].Trim().Trim('"');
                value = value.Replace("$HOME", home, StringComparison.Ordinal);

                return Path.IsPathRooted(value) ? value : null;
            }
        }
        catch (IOException)
        {
            // An unreadable file is the same as an absent one: the caller has a default.
        }

        return null;
    }
}
