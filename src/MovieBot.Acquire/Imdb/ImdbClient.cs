using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.MovieBot.Acquire.Imdb;

/// <summary>
/// What a film is called and what it looks like, from the database that names it.
///
/// This reads the index the site's own search box reads. It is the one way in that answers a
/// program: the title pages themselves return an empty 202 to anything that is not a browser, and
/// every richer API over the same data is behind a key. What this index holds — the name, the
/// year, the top billing and the poster — is all of what an embed shows, so the key buys nothing
/// that is currently wanted.
///
/// Results are cached for the process's life. A film's name does not change, and the same title is
/// asked about again every time somebody opens it.
/// </summary>
public sealed class ImdbClient(HttpClient http, ILogger<ImdbClient> logger)
{
    private const string Root = "https://v3.sg.media-imdb.com/suggestion";

    private readonly Dictionary<string, ImdbTitle?> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The film an IMDb id names, or null when the id names nothing or the index cannot be
    /// reached. Never throws: a film without a poster is worse than one with it, and better than
    /// an ingest that failed over artwork.
    /// </summary>
    public async Task<ImdbTitle?> LookupAsync(string? imdbId, CancellationToken ct)
    {
        if (ImdbId.ToTag(ImdbId.ToNumber(imdbId)) is not { } tag) return null;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(tag, out var cached)) return cached;

            var found = (await FetchAsync($"t/{tag}", ct))
                .FirstOrDefault(s => string.Equals(s.Id, tag, StringComparison.OrdinalIgnoreCase));

            var title = found is null ? null : Read(found);
            _cache[tag] = title;

            if (title is null) logger.LogInformation("{Imdb} is not in the index.", tag);
            return title;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The best match for a name, for a film that arrived without an id. A year narrows it, which
    /// matters for the titles that have been made three times.
    /// </summary>
    public async Task<ImdbTitle?> SearchAsync(string title, int? year, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var results = (await FetchAsync($"x/{Slug(title)}", ct))
            .Select(Read)
            .Where(t => t.IsFeature)
            .ToList();

        if (results.Count == 0) return null;

        // The index is ordered by what people are searching for now, which puts a sequel above the
        // film it follows. An exact name beats that ordering.
        var named = results
            .Where(t => string.Equals(t.Title, title.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        // A year is the whole of what separates a remake from what it remade, so nothing without
        // it is offered as a guess when one was given.
        if (year is { } wanted)
            return named.FirstOrDefault(t => t.Year == wanted)
                   ?? results.FirstOrDefault(t => t.Year == wanted);

        return named.FirstOrDefault() ?? results[0];
    }

    /// <summary>
    /// The films the index offers for what somebody has typed so far, in the index's own order,
    /// for a surface that suggests as a person types.
    ///
    /// Text carrying an id — a pasted link, most often — answers with the one film the id names
    /// rather than with a search for the text of the link, which would find nothing. Only
    /// features are offered; a series or an episode is not something this pipeline can fetch.
    /// </summary>
    public async Task<IReadOnlyList<ImdbTitle>> SuggestAsync(string typed, CancellationToken ct)
    {
        if (ImdbId.FromText(typed) is { } pasted)
            return await LookupAsync(pasted, ct) is { IsFeature: true } named ? [named] : [];

        var slug = Slug(typed);
        if (slug.Length == 0) return [];

        return (await FetchAsync($"x/{slug}", ct))
            .Select(Read)
            .Where(t => t.IsFeature && t.ImdbId.Length > 0 && t.Title.Length > 0)
            .ToList();
    }

    private async Task<IReadOnlyList<ImdbSuggestion>> FetchAsync(string path, CancellationToken ct)
    {
        try
        {
            var response = await http.GetAsync($"{Root}/{path}.json", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return [];

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync(
                ImdbJsonContext.Default.ImdbSuggestResponse, ct);

            return body?.Results ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The title index could not be read for {Path}.", path);
            return [];
        }
    }

    private static ImdbTitle Read(ImdbSuggestion suggestion) => new()
    {
        ImdbId = suggestion.Id ?? "",
        Title = suggestion.Label ?? "",
        Year = suggestion.Year,
        Starring = string.IsNullOrWhiteSpace(suggestion.Subtext) ? null : suggestion.Subtext,
        Kind = suggestion.Kind,
        PosterUrl = Uri.TryCreate(suggestion.Image?.Url, UriKind.Absolute, out var url) ? url : null
    };

    /// <summary>
    /// The index is keyed by a squashed form of the query: lower case, and nothing but letters
    /// and digits. Sending anything else finds nothing rather than failing.
    /// </summary>
    private static string Slug(string query) =>
        Uri.EscapeDataString(new string(query.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim());
}
