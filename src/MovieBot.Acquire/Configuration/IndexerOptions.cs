namespace TheKrystalShip.MovieBot.Acquire.Configuration;

/// <summary>
/// The tracker's member API and the identity used against it.
/// </summary>
public sealed class TrackerOptions
{
    public const string Section = "Tracker";

    /// <summary>The API endpoint. Every action is a query string against this one URL.</summary>
    public string BaseUrl { get; set; } = "https://tracker.invalid/api.php";

    /// <summary>The account name. Paired with the passkey; the account password is never used.</summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// The account's passkey. It is a credential and lives in the environment or in user-secrets,
    /// never in a file under the repository.
    ///
    /// It authenticates the API and it is also embedded in every download URL the tracker hands
    /// back, which is what makes a leak attributable: anything downloaded with it is charged to
    /// this account. <see cref="Search.Release"/> therefore carries a torrent id rather than a
    /// download link, so no value that reaches a chat surface or a log has ever held it.
    /// </summary>
    public string Passkey { get; set; } = "";

    /// <summary>
    /// The shortest gap between two calls to the API, in milliseconds. The tracker caps how often
    /// a member may call it and answers past the cap with an error rather than a result, so the
    /// client paces itself instead of discovering the ceiling in front of a room.
    /// </summary>
    public int MinimumRequestIntervalMs { get; set; } = 2000;

    /// <summary>
    /// How long a search result stays reusable, in seconds. Two people asking for the same film
    /// within the window is one call, and a select menu rebuilt after a click costs nothing.
    /// </summary>
    public int SearchCacheSeconds { get; set; } = 300;

    /// <summary>How long to wait on the API before giving up, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}
