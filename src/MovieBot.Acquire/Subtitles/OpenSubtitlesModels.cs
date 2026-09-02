using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>
/// The wire shapes the subtitle index returns, exactly as it returns them.
///
/// The names are the service's and the nesting is the service's; this is the edge of the system
/// and nothing outside <see cref="OpenSubtitlesClient"/> holds these types. What the rest of the
/// application works with is <see cref="SubtitleCandidate"/>.
/// </summary>
public sealed record OpenSubtitlesSearchResponse
{
    [JsonPropertyName("total_pages")] public int TotalPages { get; init; }
    [JsonPropertyName("total_count")] public int TotalCount { get; init; }
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("data")] public IReadOnlyList<OpenSubtitlesItem> Data { get; init; } = [];
}

public sealed record OpenSubtitlesItem
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("attributes")] public OpenSubtitlesAttributes? Attributes { get; init; }
}

public sealed record OpenSubtitlesAttributes
{
    [JsonPropertyName("language")] public string? Language { get; init; }
    [JsonPropertyName("release")] public string? Release { get; init; }
    [JsonPropertyName("download_count")] public int DownloadCount { get; init; }

    /// <summary>Zero when the uploader did not declare one, which is common and not a fault.</summary>
    [JsonPropertyName("fps")] public double Fps { get; init; }

    [JsonPropertyName("hearing_impaired")] public bool HearingImpaired { get; init; }
    [JsonPropertyName("foreign_parts_only")] public bool ForeignPartsOnly { get; init; }
    [JsonPropertyName("from_trusted")] public bool FromTrusted { get; init; }
    [JsonPropertyName("ai_translated")] public bool AiTranslated { get; init; }
    [JsonPropertyName("machine_translated")] public bool MachineTranslated { get; init; }

    /// <summary>
    /// True only on a search made by hash, where it means this subtitle was uploaded against the
    /// exact file rather than merely against the same film.
    /// </summary>
    [JsonPropertyName("moviehash_match")] public bool? MovieHashMatch { get; init; }

    /// <summary>More than one means the subtitle is split for a release that came on two discs.</summary>
    [JsonPropertyName("nb_cd")] public int CdCount { get; init; }

    [JsonPropertyName("upload_date")] public DateTimeOffset? UploadDate { get; init; }
    [JsonPropertyName("files")] public IReadOnlyList<OpenSubtitlesFile> Files { get; init; } = [];
    [JsonPropertyName("feature_details")] public OpenSubtitlesFeature? Feature { get; init; }
}

public sealed record OpenSubtitlesFile
{
    /// <summary>What a download is asked for by. The subtitle id is a different number.</summary>
    [JsonPropertyName("file_id")] public long FileId { get; init; }

    [JsonPropertyName("file_name")] public string? FileName { get; init; }
}

public sealed record OpenSubtitlesFeature
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>A bare number here, where every other system writes it as <c>tt0458352</c>.</summary>
    [JsonPropertyName("imdb_id")] public long? ImdbId { get; init; }

    [JsonPropertyName("tmdb_id")] public long? TmdbId { get; init; }
}

/// <summary>
/// What the service answers a download request with. The link is temporary and single-purpose;
/// the quota fields are the only place the remaining allowance is ever stated.
/// </summary>
public sealed record OpenSubtitlesDownloadResponse
{
    [JsonPropertyName("link")] public string? Link { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }

    /// <summary>Downloads spent today, counting this one.</summary>
    [JsonPropertyName("requests")] public int Requests { get; init; }

    /// <summary>Downloads left today. Searching does not consume these; only this call does.</summary>
    [JsonPropertyName("remaining")] public int Remaining { get; init; }

    [JsonPropertyName("reset_time_utc")] public DateTimeOffset? ResetsAt { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}
