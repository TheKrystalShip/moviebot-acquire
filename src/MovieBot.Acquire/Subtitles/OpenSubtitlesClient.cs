using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;

namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>
/// The only thing in the process that holds the subtitle index's key and the only thing that
/// talks to it.
///
/// Three of its behaviours exist because the service fails quietly rather than loudly:
///
/// It builds every query string sorted and without default values, because the API answers a
/// query written any other way with a 301 to the canonical spelling. Following that redirect
/// works; not following it stores an HTML error page as if it were a result, and only for some
/// calls, which reads as an intermittent fault rather than a mistake.
///
/// It paces itself, because the download links are capped at a few requests a second and answer
/// past the cap with a line of plain text where a subtitle should be. A client that does not
/// check writes that line to disk as a valid, tiny subtitle file.
///
/// It separates searching from downloading, because only downloading spends the day's allowance.
/// Searching is free and can be done as often as a menu needs it.
/// </summary>
public sealed class OpenSubtitlesClient(
    HttpClient http,
    IOptions<OpenSubtitlesOptions> options,
    ILogger<OpenSubtitlesClient> logger)
{
    private readonly OpenSubtitlesOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> _cache = [];
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    private sealed record CacheEntry(IReadOnlyList<OpenSubtitlesItem> Items, DateTimeOffset Expires);

    /// <summary>What the last download said was left of today's allowance, or null before one.</summary>
    public int? RemainingDownloads { get; private set; }

    /// <summary>When the allowance resets, as the service last stated it.</summary>
    public DateTimeOffset? QuotaResetsAt { get; private set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>
    /// Subtitles uploaded against this exact file. Precise where everything else is a guess, and
    /// empty far more often than not: a hash is only indexed once somebody has watched that file
    /// in a player that reports it.
    /// </summary>
    public Task<IReadOnlyList<OpenSubtitlesItem>> SearchByHashAsync(
        string movieHash, string language, CancellationToken ct) =>
        SearchAsync(new SortedDictionary<string, string>
        {
            ["languages"] = language,
            ["moviehash"] = movieHash
        }, pages: 1, ct);

    /// <summary>
    /// Every subtitle for a film, by its IMDb id. Exact where a title search is not: a title
    /// query returns other films that merely share a word and a year.
    /// </summary>
    public Task<IReadOnlyList<OpenSubtitlesItem>> SearchByImdbAsync(
        string imdbId, string language, CancellationToken ct)
    {
        var number = ImdbId.ToNumber(imdbId)
            ?? throw new OpenSubtitlesException($"'{imdbId}' is not an IMDb id.");

        return SearchAsync(new SortedDictionary<string, string>
        {
            ["imdb_id"] = number.ToString(),
            ["languages"] = language
        }, _options.MaximumPages, ct);
    }

    /// <summary>
    /// The fallback for a film with no IMDb id recorded. It matches on text, so it returns other
    /// films as well as this one and whatever uses it has to say so.
    /// </summary>
    public Task<IReadOnlyList<OpenSubtitlesItem>> SearchByTitleAsync(
        string title, int? year, string language, CancellationToken ct)
    {
        var query = new SortedDictionary<string, string>
        {
            ["languages"] = language,
            ["query"] = title
        };

        if (year is { } y) query["year"] = y.ToString();

        return SearchAsync(query, _options.MaximumPages, ct);
    }

    private async Task<IReadOnlyList<OpenSubtitlesItem>> SearchAsync(
        SortedDictionary<string, string> query, int pages, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new OpenSubtitlesException("The subtitle index has no API key configured.");

        var key = string.Join('&', query.Select(p => $"{p.Key}={p.Value}")).ToLowerInvariant();

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
                return cached.Items;

            var items = new List<OpenSubtitlesItem>();

            for (var page = 1; page <= pages; page++)
            {
                // Page one is the default and naming it earns a redirect rather than a result.
                if (page > 1) query["page"] = page.ToString();

                var response = await GetAsync(query, ct);
                if (response is null) break;

                items.AddRange(response.Data);
                if (page >= response.TotalPages) break;
            }

            query.Remove("page");
            _cache[key] = new CacheEntry(
                items, DateTimeOffset.UtcNow.AddSeconds(_options.SearchCacheSeconds));

            return items;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OpenSubtitlesSearchResponse?> GetAsync(
        SortedDictionary<string, string> query, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/subtitles?"
                  + string.Join('&', query.Select(p =>
                      $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        await PaceAsync(ct);

        using var request = Build(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, ct);

        // The canonical form is what this already builds, so a redirect here means the rules
        // changed rather than that the request needs following.
        if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found)
            throw new OpenSubtitlesException(
                $"The index redirected a canonical query to {response.Headers.Location}, "
                + "so the way it wants queries written has changed.");

        if (!response.IsSuccessStatusCode)
            throw new OpenSubtitlesException(
                $"The index answered {(int)response.StatusCode} searching for subtitles.");

        try
        {
            return await response.Content.ReadFromJsonAsync(
                OpenSubtitlesJsonContext.Default.OpenSubtitlesSearchResponse, ct);
        }
        catch (JsonException ex)
        {
            throw new OpenSubtitlesException("The index answered a search with something that is not JSON.", ex);
        }
    }

    /// <summary>
    /// Asks for a link to one subtitle file, which is the call that spends the day's allowance.
    /// The link is temporary; <see cref="FetchAsync"/> is what turns it into bytes.
    /// </summary>
    public async Task<OpenSubtitlesDownloadResponse> RequestDownloadAsync(long fileId, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new OpenSubtitlesException("The subtitle index has no API key configured.");

        await _gate.WaitAsync(ct);
        try
        {
            await PaceAsync(ct);

            using var request = Build(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/download");
            request.Content = JsonContent.Create(
                new OpenSubtitlesDownloadRequest { FileId = fileId },
                OpenSubtitlesJsonContext.Default.OpenSubtitlesDownloadRequest);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)406)
                throw new OpenSubtitlesQuotaException(
                    "The day's subtitle download allowance is spent.", QuotaResetsAt);

            if (!response.IsSuccessStatusCode)
                throw new OpenSubtitlesException(
                    $"The index answered {(int)response.StatusCode} asking for file {fileId}.");

            var body = await response.Content.ReadFromJsonAsync(
                OpenSubtitlesJsonContext.Default.OpenSubtitlesDownloadResponse, ct)
                ?? throw new OpenSubtitlesException("The index answered a download with an empty body.");

            if (string.IsNullOrWhiteSpace(body.Link))
                throw new OpenSubtitlesException("The index answered a download without a link.");

            RemainingDownloads = body.Remaining;
            QuotaResetsAt = body.ResetsAt;

            logger.LogInformation(
                "Subtitle {FileName} allowed; {Remaining} downloads left today.",
                body.FileName, body.Remaining);

            return body;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Fetches the file a download link points at.
    ///
    /// Returns bytes rather than text on purpose: the file's encoding is the caller's problem and
    /// is frequently not UTF-8, so decoding it here would be guessing in the one place that has
    /// the least context to guess with.
    /// </summary>
    public async Task<byte[]> FetchAsync(string link, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await PaceAsync(ct);

            using var response = await http.GetAsync(link, ct);
            if (!response.IsSuccessStatusCode)
                throw new OpenSubtitlesException(
                    $"A subtitle link answered {(int)response.StatusCode}.");

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);

            // Being throttled looks like success and arrives as a short line of prose. Nothing
            // downstream would question it: it is valid text, and an empty subtitle track simply
            // shows no subtitles.
            if (bytes.Length < MinimumPlausibleSubtitleBytes)
                throw new OpenSubtitlesException(
                    $"A subtitle link answered with {bytes.Length} bytes, which is a message rather than a subtitle.");

            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Shorter than this is not a subtitle for a feature film. The throttle message is 37 bytes;
    /// the shortest real track measured here is over eighty thousand.
    /// </summary>
    private const int MinimumPlausibleSubtitleBytes = 512;

    private HttpRequestMessage Build(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Api-Key", _options.ApiKey);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task PaceAsync(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow - _lastRequest;
        var floor = TimeSpan.FromMilliseconds(_options.MinimumRequestIntervalMs);

        if (since < floor) await Task.Delay(floor - since, ct);

        _lastRequest = DateTimeOffset.UtcNow;
    }
}
