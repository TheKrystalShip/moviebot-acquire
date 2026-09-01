using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// One torrent as the client's API reports it. The wire names are the client's, not ours; what
/// the rest of the application works with is <see cref="DownloadStatus"/>.
/// </summary>
public sealed record QBittorrentTorrent
{
    [JsonPropertyName("hash")] public string Hash { get; init; } = "";

    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>
    /// The client's own state word. There are a dozen and a half of them and they distinguish
    /// things nobody watching a film cares about, so <see cref="DownloadStatus"/> reduces them.
    /// </summary>
    [JsonPropertyName("state")] public string State { get; init; } = "";

    /// <summary>Completion, from 0 to 1.</summary>
    [JsonPropertyName("progress")] public double Progress { get; init; }

    [JsonPropertyName("size")] public long Size { get; init; }

    [JsonPropertyName("amount_left")] public long AmountLeft { get; init; }

    [JsonPropertyName("dlspeed")] public long DownloadSpeed { get; init; }

    /// <summary>Seconds remaining. The client reports a placeholder when it does not know.</summary>
    [JsonPropertyName("eta")] public long Eta { get; init; }

    /// <summary>Seconds since the epoch, or a negative value while it is still downloading.</summary>
    [JsonPropertyName("completion_on")] public long CompletionOn { get; init; }

    /// <summary>Where the film itself is, which is what the ingest is handed.</summary>
    [JsonPropertyName("content_path")] public string ContentPath { get; init; } = "";

    [JsonPropertyName("save_path")] public string SavePath { get; init; } = "";

    [JsonPropertyName("seq_dl")] public bool SequentialDownload { get; init; }

    [JsonPropertyName("ratio")] public double Ratio { get; init; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IReadOnlyList<QBittorrentTorrent>))]
public partial class QBittorrentJsonContext : JsonSerializerContext;

/// <summary>The torrent client refused something, or could not be reached.</summary>
public sealed class QBittorrentException(string message, Exception? inner = null)
    : Exception(message, inner);
