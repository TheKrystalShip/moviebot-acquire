namespace TheKrystalShip.MovieBot.Acquire.Configuration;

/// <summary>
/// The film database that describes a film: its credits, its certificate, what it is about.
/// </summary>
public sealed class TmdbOptions
{
    public const string Section = "Tmdb";

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";

    /// <summary>Where artwork is served from; a size and a file path are appended.</summary>
    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p";

    /// <summary>
    /// The API Read Access Token, sent as the bearer. It is a credential and lives in the
    /// environment, never in a file under the repository. Empty means the database is not asked,
    /// and every film is described by its name, year and poster alone.
    /// </summary>
    public string ReadAccessToken { get; set; } = "";

    /// <summary>The language names, overviews and genres are asked for in.</summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// The country whose certificate a film is shown with. A film carries one per country and they
    /// disagree, so one country is chosen rather than whichever happens to come first.
    /// </summary>
    public string Region { get; set; } = "US";

    /// <summary>
    /// The country whose streaming services are asked about, and where a title streams; empty for
    /// <see cref="Region"/>. Certificates and streaming are separate because the shelves name
    /// certificates by one country's ratings while the people watching stream in another.
    /// </summary>
    public string WatchRegion { get; set; } = "";

    /// <summary>The country streaming is asked about: <see cref="WatchRegion"/>, else <see cref="Region"/>.</summary>
    public string StreamingRegion => string.IsNullOrWhiteSpace(WatchRegion) ? Region : WatchRegion.Trim();

    public int TimeoutSeconds { get; set; } = 15;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ReadAccessToken);
}
