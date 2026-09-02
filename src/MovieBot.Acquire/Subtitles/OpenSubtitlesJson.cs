using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>
/// The serializer for the subtitle index's wire shapes, shipped beside them so the snake_case
/// names cannot drift apart from the types. Every root is registered here; a type reached only by
/// reflection would throw at runtime.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenSubtitlesSearchResponse))]
[JsonSerializable(typeof(OpenSubtitlesDownloadResponse))]
[JsonSerializable(typeof(OpenSubtitlesDownloadRequest))]
public partial class OpenSubtitlesJsonContext : JsonSerializerContext;

/// <summary>The body a download is asked for with.</summary>
public sealed record OpenSubtitlesDownloadRequest
{
    [JsonPropertyName("file_id")] public long FileId { get; init; }
}

/// <summary>The index refused a call, or answered with something that is not a result.</summary>
public sealed class OpenSubtitlesException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// The day's download allowance is spent. Distinct from any other failure because it is the one
/// that resolves by itself, at a stated time, and telling somebody to try again tomorrow is a
/// useful answer where "the subtitle service failed" is not.
/// </summary>
public sealed class OpenSubtitlesQuotaException(string message, DateTimeOffset? resetsAt)
    : Exception(message)
{
    public DateTimeOffset? ResetsAt { get; } = resetsAt;
}
