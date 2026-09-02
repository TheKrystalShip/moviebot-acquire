namespace TheKrystalShip.MovieBot.Acquire.Configuration;

/// <summary>
/// The subtitle index and the key used against it.
/// </summary>
public sealed class OpenSubtitlesOptions
{
    public const string Section = "OpenSubtitles";

    public string BaseUrl { get; set; } = "https://api.opensubtitles.com/api/v1";

    /// <summary>
    /// The consumer key. It is a credential and lives in the environment, never in a file under
    /// the repository. A key marked for anonymous use needs no account login behind it, so this is
    /// the only secret the client holds.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The application this identifies itself as. The API requires one and refuses a request that
    /// arrives without it.
    /// </summary>
    public string UserAgent { get; set; } = "MovieBot v1.8.0";

    /// <summary>
    /// The shortest gap between two calls, in milliseconds. The service caps requests per second
    /// and answers past the cap with a line of plain text where a file should be, which a client
    /// that is not looking for it stores as a 37-byte subtitle.
    /// </summary>
    public int MinimumRequestIntervalMs { get; set; } = 250;

    /// <summary>
    /// How long a search stays reusable, in seconds. Searching costs no download, but a menu
    /// rebuilt after every click still makes the room wait for the network.
    /// </summary>
    public int SearchCacheSeconds { get; set; } = 600;

    public int TimeoutSeconds { get; set; } = 25;

    /// <summary>
    /// How many pages of results to read. Fifty candidates per page is already more than anyone
    /// will look at, and the tail of a title search is other films entirely.
    /// </summary>
    public int MaximumPages { get; set; } = 2;
}
