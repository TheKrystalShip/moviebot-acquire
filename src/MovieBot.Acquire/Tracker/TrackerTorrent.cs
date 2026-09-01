using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Tracker;

/// <summary>
/// One row exactly as the tracker's API returns it.
///
/// The wire names are snake_case and the shape is the tracker's, not ours: this type is the edge
/// of the system and nothing outside <see cref="TrackerClient"/> should hold it. What the rest
/// of the application works with is <see cref="Search.Release"/>, which is parsed from this and
/// deliberately drops <see cref="DownloadLink"/>.
/// </summary>
public sealed record TrackerTorrent
{
    [JsonPropertyName("id")] public long Id { get; init; }

    /// <summary>The release name, which is the only description of the encode that exists.</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>The IMDb id, as <c>tt0000000</c>, or empty when the uploader left it off.</summary>
    [JsonPropertyName("imdb")] public string? Imdb { get; init; }

    /// <summary>Whether downloading this counts against the account's ratio.</summary>
    [JsonPropertyName("freeleech")] public int Freeleech { get; init; }

    [JsonPropertyName("doubleup")] public int DoubleUp { get; init; }

    [JsonPropertyName("upload_date")] public string? UploadDate { get; init; }

    /// <summary>
    /// The tracker's download URL, with this account's passkey already in the query string.
    ///
    /// It is read off the wire and then discarded: <see cref="TrackerClient"/> builds its own
    /// URL at the moment of download, so the passkey exists in exactly one place in the process.
    /// </summary>
    [JsonPropertyName("download_link")] public string? DownloadLink { get; init; }

    /// <summary>Total size in bytes.</summary>
    [JsonPropertyName("size")] public long Size { get; init; }

    [JsonPropertyName("internal")] public int Internal { get; init; }

    [JsonPropertyName("moderated")] public int Moderated { get; init; }

    /// <summary>The tracker's own category name, such as <c>Movies 4K</c>.</summary>
    [JsonPropertyName("category")] public string Category { get; init; } = "";

    [JsonPropertyName("seeders")] public int Seeders { get; init; }

    [JsonPropertyName("leechers")] public int Leechers { get; init; }

    [JsonPropertyName("times_completed")] public int TimesCompleted { get; init; }

    [JsonPropertyName("comments")] public int Comments { get; init; }

    /// <summary>How many files the torrent holds. More than a handful means extras beside the film.</summary>
    [JsonPropertyName("files")] public int Files { get; init; }

    /// <summary>The genre line the tracker carries, when it has one.</summary>
    [JsonPropertyName("small_description")] public string? SmallDescription { get; init; }
}

/// <summary>The body the API returns instead of an array when it refuses a call.</summary>
public sealed record TrackerError
{
    [JsonPropertyName("error")] public string? Error { get; init; }
}
