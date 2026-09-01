using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// The torrent client, over its Web API.
///
/// Nothing here knows about the tracker and nothing here holds the passkey: it is handed the
/// bytes of a .torrent file and reports on what it was asked to fetch.
/// </summary>
public sealed class QBittorrentClient(
    HttpClient http,
    IOptions<QBittorrentOptions> options,
    ILogger<QBittorrentClient> logger)
{
    private readonly QBittorrentOptions _options = options.Value;
    private bool _signedIn;

    /// <summary>Whether the client is running and answering, checked before anything is promised.</summary>
    public async Task<bool> IsReachableAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync("api/v2/app/version", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Hands a .torrent file to the client and returns its info hash.
    ///
    /// The hash is computed from the file rather than taken from the response, because the add
    /// endpoint answers "Ok." and does not say what it added. Adding a torrent already present
    /// is not an error and returns the same hash, so a repeated request is harmless.
    /// </summary>
    public async Task<string> AddAsync(
        byte[] torrentFile, string savePath, IEnumerable<string>? tags, CancellationToken ct)
    {
        var hash = Bencode.InfoHash(torrentFile);

        await EnsureSignedInAsync(ct);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(torrentFile);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/x-bittorrent");
        form.Add(file, "torrents", $"{hash}.torrent");

        form.Add(new StringContent(savePath), "savepath");
        form.Add(new StringContent(_options.Category), "category");
        form.Add(new StringContent(Spell(_options.SequentialDownload)), "sequentialDownload");
        form.Add(new StringContent(Spell(_options.FirstLastPiecePriority)), "firstLastPiecePrio");

        // The client separates tags by comma, so a tag may not contain one.
        var tagList = (tags ?? []).Select(t => t.Replace(',', ' ')).ToList();
        if (tagList.Count > 0)
            form.Add(new StringContent(string.Join(',', tagList)), "tags");

        try
        {
            using var response = await http.PostAsync("api/v2/torrents/add", form, ct);
            var body = (await response.Content.ReadAsStringAsync(ct)).Trim();

            if (!response.IsSuccessStatusCode)
                throw new QBittorrentException(
                    $"The torrent client refused the torrent: {(int)response.StatusCode} {body}");

            // The endpoint reports failure in the body under a 200, so the status alone is not
            // enough to know it was taken.
            if (body.Equals("Fails.", StringComparison.OrdinalIgnoreCase))
                throw new QBittorrentException("The torrent client rejected the torrent file.");

            logger.LogInformation("Added {Hash} to the torrent client.", hash);
            return hash;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new QBittorrentException("The torrent client could not be reached.", ex);
        }
    }

    /// <summary>Adds one tag to a torrent, so a later pass can see what happened on this one.</summary>
    public Task AddTagAsync(string hash, string tag, CancellationToken ct) =>
        ChangeTagsAsync("api/v2/torrents/addTags", hash, tag, ct);

    /// <summary>
    /// Removes one tag from a torrent. Used to mark a note as acted on, so the same download is
    /// not announced twice.
    /// </summary>
    public Task RemoveTagAsync(string hash, string tag, CancellationToken ct) =>
        ChangeTagsAsync("api/v2/torrents/removeTags", hash, tag, ct);

    private async Task ChangeTagsAsync(string endpoint, string hash, string tag, CancellationToken ct)
    {
        await EnsureSignedInAsync(ct);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hashes"] = hash,
            ["tags"] = tag,
        });

        try
        {
            using var response = await http.PostAsync(endpoint, form, ct);
            if (!response.IsSuccessStatusCode)
                throw new QBittorrentException(
                    $"The torrent client answered {(int)response.StatusCode} changing a tag.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new QBittorrentException("The torrent client could not be reached.", ex);
        }
    }

    /// <summary>One download, or null when the client has never heard of it.</summary>
    public async Task<DownloadStatus?> GetAsync(string hash, CancellationToken ct)
    {
        var all = await ListAsync(hash, ct);
        return all.FirstOrDefault();
    }

    /// <summary>Everything this project started, or one torrent when a hash is given.</summary>
    public async Task<IReadOnlyList<DownloadStatus>> ListAsync(string? hash, CancellationToken ct)
    {
        await EnsureSignedInAsync(ct);

        var url = hash is null
            ? $"api/v2/torrents/info?category={Uri.EscapeDataString(_options.Category)}"
            : $"api/v2/torrents/info?hashes={Uri.EscapeDataString(hash)}";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new QBittorrentException(
                    $"The torrent client answered {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsStringAsync(ct);
            var torrents = JsonSerializer.Deserialize(
                body, QBittorrentJsonContext.Default.IReadOnlyListQBittorrentTorrent) ?? [];

            return torrents.Select(Describe).ToList();
        }
        catch (JsonException ex)
        {
            throw new QBittorrentException("The torrent client's answer could not be read.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new QBittorrentException("The torrent client could not be reached.", ex);
        }
    }

    /// <summary>
    /// How many bytes of one file inside a torrent are readable from its start, without a gap.
    ///
    /// Contiguous is the only useful measure here, because what reads the file reads it forwards:
    /// a piece that has arrived beyond a missing one is not reachable, and treating overall
    /// progress as a position would hand out an offset with a hole behind it.
    ///
    /// Torrents downloaded in order fill contiguously by construction. This asks rather than
    /// assumes, because a piece can arrive out of order regardless — an endgame duplicate, a
    /// prioritised first-and-last piece — and being wrong here means reading unwritten file.
    /// </summary>
    public async Task<long> ReadableBytesAsync(string hash, string filePath, CancellationToken ct)
    {
        await EnsureSignedInAsync(ct);

        var properties = await GetJsonAsync(
            $"api/v2/torrents/properties?hash={Uri.EscapeDataString(hash)}",
            QBittorrentJsonContext.Default.QBittorrentProperties, ct);

        var files = await GetJsonAsync(
            $"api/v2/torrents/files?hash={Uri.EscapeDataString(hash)}",
            QBittorrentJsonContext.Default.IReadOnlyListQBittorrentFile, ct);

        var states = await GetJsonAsync(
            $"api/v2/torrents/pieceStates?hash={Uri.EscapeDataString(hash)}",
            QBittorrentJsonContext.Default.Int32Array, ct);

        if (properties is null || files is null || states is null || properties.PieceSize <= 0)
            return 0;

        // The file's own offset into the torrent's byte stream is the sum of everything before
        // it. A single-file torrent starts at zero; a film in a folder of extras does not.
        var wanted = Path.GetFileName(filePath);
        long fileOffset = 0;
        long fileSize = 0;
        var found = false;

        foreach (var file in files.OrderBy(f => f.Index))
        {
            if (Path.GetFileName(file.Name).Equals(wanted, StringComparison.Ordinal))
            {
                fileSize = file.Size;
                found = true;
                break;
            }

            fileOffset += file.Size;
        }

        if (!found) return 0;

        // Two is the client's spelling of "downloaded". The first piece that is not is where
        // reading forwards has to stop.
        var missing = Array.IndexOf(states, 0) is var zero && zero >= 0 ? zero : states.Length;
        var partial = Array.IndexOf(states, 1);
        if (partial >= 0 && partial < missing) missing = partial;

        var contiguous = (long)missing * properties.PieceSize;
        var readable = contiguous - fileOffset;

        return Math.Clamp(readable, 0, fileSize);
    }

    private async Task<T?> GetJsonAsync<T>(
        string url, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new QBittorrentException(
                    $"The torrent client answered {(int)response.StatusCode}.");

            return JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), type);
        }
        catch (JsonException ex)
        {
            throw new QBittorrentException("The torrent client's answer could not be read.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new QBittorrentException("The torrent client could not be reached.", ex);
        }
    }

    private static DownloadStatus Describe(QBittorrentTorrent torrent) => new()
    {
        Hash = torrent.Hash,
        Name = torrent.Name,
        State = ReadState(torrent.State),
        Progress = torrent.Progress,
        SizeBytes = torrent.Size,
        BytesPerSecond = torrent.DownloadSpeed,
        Remaining = ReadEta(torrent.Eta),
        ContentPath = torrent.ContentPath,
        IsSequential = torrent.SequentialDownload,
        Tags = torrent.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    };

    /// <summary>
    /// Reduces the client's state vocabulary. Anything unrecognised is reported as starting
    /// rather than failed: a state this does not know is far more likely to be a transient the
    /// client has that this does not, and calling it a failure would be a lie about a download
    /// that is fine.
    /// </summary>
    private static DownloadState ReadState(string state) => state switch
    {
        "downloading" or "forcedDL" or "checkingDL" or "checkingResumeData" or "moving"
            => DownloadState.Downloading,
        "metaDL" or "allocating" or "queuedDL" => DownloadState.Starting,
        "stalledDL" => DownloadState.Stalled,
        "uploading" or "stalledUP" or "forcedUP" or "queuedUP" or "checkingUP" or "pausedUP"
            or "stoppedUP" => DownloadState.Complete,
        "pausedDL" or "stoppedDL" => DownloadState.Paused,
        "error" or "missingFiles" or "unknown" => DownloadState.Failed,
        _ => DownloadState.Starting,
    };

    /// <summary>
    /// The client reports a placeholder rather than nothing when it cannot estimate, and showing
    /// that as a duration produces a film arriving in a hundred years.
    /// </summary>
    private static TimeSpan? ReadEta(long seconds) =>
        seconds is <= 0 or >= 8640000 ? null : TimeSpan.FromSeconds(seconds);

    private static string Spell(bool value) =>
        value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();

    /// <summary>
    /// Signs in, when there is anything to sign in with. Over loopback the client is configured
    /// to let the request through unauthenticated, so this does nothing at all.
    /// </summary>
    private async Task EnsureSignedInAsync(CancellationToken ct)
    {
        if (_signedIn || string.IsNullOrEmpty(_options.Username)) return;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _options.Username,
            ["password"] = _options.Password,
        });

        using var response = await http.PostAsync("api/v2/auth/login", form, ct);
        var body = (await response.Content.ReadAsStringAsync(ct)).Trim();

        if (!response.IsSuccessStatusCode || body.Contains("Fail", StringComparison.OrdinalIgnoreCase))
            throw new QBittorrentException("The torrent client refused the credentials.");

        _signedIn = true;
    }
}
