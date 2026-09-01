using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;

namespace TheKrystalShip.MovieBot.Acquire.Tracker;

/// <summary>
/// The only thing in the process that holds the passkey and the only thing that talks to the
/// tracker.
///
/// The tracker publishes a member API, so there is no HTML in this client and no session to keep
/// alive: no login, no cookie jar, no CSRF token, and nothing that breaks when the site is
/// restyled. Every call is one GET against one URL.
///
/// Two things it does beyond the request itself, both because the alternative fails in front of
/// a room rather than on a log: it paces calls to the interval the options name, since the
/// tracker answers past its cap with an error rather than a result; and it holds a result for a
/// short window, since rebuilding a select menu after a click would otherwise cost a call.
/// </summary>
public sealed class TrackerClient(
    HttpClient http,
    IOptions<TrackerOptions> options,
    ILogger<TrackerClient> logger)
{
    private readonly TrackerOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> _cache = [];
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    private sealed record CacheEntry(IReadOnlyList<TrackerTorrent> Rows, DateTimeOffset Expires);

    /// <summary>Whether the client has been given an identity to call with.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Passkey);

    /// <summary>Searches by title text, the way somebody types a film's name.</summary>
    public Task<IReadOnlyList<TrackerTorrent>> SearchByNameAsync(string query, CancellationToken ct) =>
        SearchAsync("name", query, ct);

    /// <summary>
    /// Searches by IMDb id, which is exact where a title is not: it separates a remake from its
    /// original and it survives a film being known by a different name in another country.
    /// </summary>
    public Task<IReadOnlyList<TrackerTorrent>> SearchByImdbAsync(string imdbId, CancellationToken ct) =>
        SearchAsync("imdb", imdbId, ct);

    private async Task<IReadOnlyList<TrackerTorrent>> SearchAsync(
        string type, string query, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new TrackerException("The tracker has no username and passkey configured.");

        if (string.IsNullOrWhiteSpace(query))
            return [];

        var key = $"{type}:{query.Trim().ToLowerInvariant()}";

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
                return cached.Rows;

            await PaceAsync(ct);

            var url = BuildUrl(new()
            {
                ["action"] = "search-torrents",
                ["type"] = type,
                ["query"] = query.Trim(),
            });

            var rows = await GetRowsAsync(url, ct);

            _cache[key] = new CacheEntry(
                rows, DateTimeOffset.UtcNow.AddSeconds(_options.SearchCacheSeconds));

            // The cache is per-process and small by construction, but a long-lived bot would
            // otherwise keep every search anybody ever ran.
            PruneExpired();

            return rows;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Fetches the .torrent file for one result.
    ///
    /// The URL is built here rather than taken from the search row, so the passkey the tracker
    /// echoes back in every row is read off the wire and immediately dropped. The bytes go
    /// straight to the torrent client; they are never written beside the repository.
    /// </summary>
    public async Task<byte[]> DownloadTorrentFileAsync(long torrentId, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new TrackerException("The tracker has no username and passkey configured.");

        await _gate.WaitAsync(ct);
        try
        {
            await PaceAsync(ct);

            var url = "https://tracker.invalid/download.php"
                      + $"?id={torrentId}&passkey={Uri.EscapeDataString(_options.Passkey)}";

            using var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);

            // The tracker answers a refused download with an HTML page under a 200, so the only
            // reliable check is the payload itself. A bencoded torrent starts with a dictionary.
            if (bytes.Length == 0 || bytes[0] != (byte)'d')
                throw new TrackerException(
                    $"The tracker did not return a torrent file for {torrentId}.");

            return bytes;
        }
        catch (HttpRequestException ex)
        {
            throw new TrackerException($"The torrent file for {torrentId} could not be fetched.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<TrackerTorrent>> GetRowsAsync(string url, CancellationToken ct)
    {
        string body;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);

            // A refusal arrives as a 401 carrying a JSON reason, which is more useful than the
            // status, so the body is read before the status is judged.
            if (!response.IsSuccessStatusCode)
                throw new TrackerException(DescribeFailure(response.StatusCode, body));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new TrackerException("The tracker could not be reached.", ex);
        }

        var first = body.AsSpan().TrimStart();

        // Success is an array and every failure is an object, so the first character decides
        // which shape to parse rather than a failed deserialization deciding it.
        if (first.Length == 0)
            return [];

        if (first[0] == '{')
        {
            var error = Deserialize(body, TrackerJsonContext.Default.TrackerError);
            throw new TrackerException(
                $"The tracker refused the search: {error?.Error ?? "no reason given"}.");
        }

        if (first[0] != '[')
            throw new TrackerException("The tracker answered with something that is not JSON.");

        return Deserialize(body, TrackerJsonContext.Default.IReadOnlyListTrackerTorrent) ?? [];
    }

    private static T? Deserialize<T>(string body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        try
        {
            return JsonSerializer.Deserialize(body, type);
        }
        catch (JsonException ex)
        {
            throw new TrackerException("The tracker's answer could not be read.", ex);
        }
    }

    private static string DescribeFailure(System.Net.HttpStatusCode status, string body)
    {
        var trimmed = body.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '{')
        {
            try
            {
                var error = JsonSerializer.Deserialize(body, TrackerJsonContext.Default.TrackerError);
                if (!string.IsNullOrWhiteSpace(error?.Error))
                    return $"The tracker refused the call: {error.Error}";
            }
            catch (JsonException)
            {
                // Fall through to the status, which is all that is left to report.
            }
        }

        return $"The tracker answered {(int)status}.";
    }

    private string BuildUrl(Dictionary<string, string> parameters)
    {
        var query = new List<string>
        {
            $"username={Uri.EscapeDataString(_options.Username)}",
            $"passkey={Uri.EscapeDataString(_options.Passkey)}",
        };

        foreach (var (name, value) in parameters)
            query.Add($"{name}={Uri.EscapeDataString(value)}");

        return $"{_options.BaseUrl}?{string.Join('&', query)}";
    }

    /// <summary>
    /// Holds calls apart by the configured interval. The gate is already held by the caller, so
    /// this serializes every call the process makes rather than every call on one path.
    /// </summary>
    private async Task PaceAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(_options.MinimumRequestIntervalMs);
        var since = DateTimeOffset.UtcNow - _lastRequest;

        if (since < interval)
        {
            var wait = interval - since;
            logger.LogDebug("Pacing the tracker call by {Milliseconds}ms.", wait.TotalMilliseconds);
            await Task.Delay(wait, ct);
        }

        _lastRequest = DateTimeOffset.UtcNow;
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _cache.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToList())
            _cache.Remove(key);
    }
}
