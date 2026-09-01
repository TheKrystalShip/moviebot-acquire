namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>Where the torrent client is, and how torrents are handed to it.</summary>
public sealed class QBittorrentOptions
{
    public const string Section = "QBittorrent";

    /// <summary>
    /// The client's Web API. Loopback, because the client is configured to let a request from
    /// loopback through without credentials: nothing here holds a password for it.
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    /// <summary>
    /// Set only if the client is reached across a network, where its local bypass does not
    /// apply. Empty leaves the session unauthenticated, which is correct over loopback.
    /// </summary>
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>
    /// The category every torrent is added under, so what this started is distinguishable from
    /// anything else the client is running.
    /// </summary>
    public string Category { get; set; } = "moviebot";

    /// <summary>
    /// Whether pieces are fetched in order rather than rarest-first.
    ///
    /// It costs some download speed, since the client can no longer take whatever a peer happens
    /// to have, and it buys a file that is watchable from the beginning before it is finished.
    /// </summary>
    public bool SequentialDownload { get; set; } = true;

    /// <summary>
    /// Whether the first and last pieces are fetched ahead of the rest.
    ///
    /// Separate from sequential order and necessary alongside it: a container keeps the index a
    /// player needs before anything else at one end or the other, and without it a file that is
    /// sequentially complete for twenty minutes still will not open.
    /// </summary>
    public bool FirstLastPiecePriority { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;
}
