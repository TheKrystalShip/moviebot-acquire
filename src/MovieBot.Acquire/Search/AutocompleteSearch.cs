using Microsoft.Extensions.Logging;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>One row of an autocomplete list: what is shown, and what comes back when it is picked.</summary>
/// <param name="Label">The text the chat surface displays, already within its length limit.</param>
/// <param name="TorrentId">
/// What the surface sends back on submit. An id rather than a release name: the name is long
/// enough to be truncated for display, and a truncated name cannot be resolved to a release.
/// </param>
public sealed record ReleaseChoice(string Label, long TorrentId);

/// <summary>How autocomplete paces itself against the tracker.</summary>
public sealed class AutocompleteOptions
{
    public const string Section = "Autocomplete";

    /// <summary>
    /// The shortest query that reaches the tracker. Below it the list stays empty: one or two
    /// letters match a large fraction of the tracker and describe no film anybody is after.
    /// </summary>
    public int MinimumQueryLength { get; set; } = 3;

    /// <summary>
    /// How long typing must stop before the tracker is asked, in milliseconds.
    ///
    /// A keystroke arriving inside the window supersedes the one before it, so a query typed at
    /// speed reaches the tracker once, when it is finished, rather than once per character. It
    /// costs the pause before the first results appear and it sits well inside the surface's own
    /// deadline.
    ///
    /// Nothing that can be answered from what is already held waits on this: a repeated query
    /// and a query that merely narrows one already answered are both served immediately.
    /// </summary>
    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// How long a suggestion may take before an empty list is better, in milliseconds.
    ///
    /// The chat surface discards a late answer and shows the user nothing either way, so the
    /// deadline exists to stop a slow call holding the pacing gate against the keystroke behind
    /// it. It sits below the surface's own limit rather than at it.
    /// </summary>
    public int DeadlineMs { get; set; } = 2000;

    /// <summary>
    /// The longest label a row may carry. It is the chat surface's limit, and a label over it is
    /// rejected outright rather than truncated by the surface.
    /// </summary>
    public int MaximumLabelLength { get; set; } = 100;
}

/// <summary>
/// Serves an autocomplete list as somebody types, without spending a tracker call per keystroke.
///
/// A chat surface sends an autocomplete request on every character typed and expects an answer
/// within a few seconds. Answered naively that is a tracker call per keystroke, which is both
/// far past what the tracker permits and slower than the typing it is trying to keep up with.
///
/// Four things keep it to roughly one call per search:
///
/// - Nothing shorter than the minimum query length is looked up at all.
/// - A query that extends one already answered is filtered from that answer rather than asked
///   again. The tracker narrows on the words it is given, so a longer query's results are a
///   subset of a shorter one's, and the subset can be taken locally. This costs nothing and so
///   happens immediately, without waiting for typing to stop.
/// - Anything left goes to the tracker only once typing has stopped for the debounce window. A
///   keystroke arriving inside it supersedes the one before, which returns what is known rather
///   than asking.
/// - Only one call is in flight at a time. A second person searching queues behind the first for
///   as long as the deadline allows rather than being answered with nothing.
/// </summary>
public sealed class AutocompleteSearch(
    IReleaseSearch search,
    AutocompleteOptions options,
    ILogger<AutocompleteSearch> logger)
{
    private readonly Dictionary<string, IReadOnlyList<Release>> _answered = [];

    // Every release ever offered, by torrent id. The surface sends back only the value behind a
    // row, so this is what turns that value into the release it stood for. It is kept separately
    // from the query cache because a person picking a row is answering a query that may already
    // have expired out of it.
    private readonly Dictionary<long, Release> _offered = [];
    private readonly Dictionary<string, long> _newestPerSession = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _sequence;

    /// <param name="partial">What has been typed so far.</param>
    /// <param name="sessionKey">
    /// Who is typing. Debouncing is per person: one person mid-word must not suppress another
    /// person's finished query.
    /// </param>
    public async Task<IReadOnlyList<ReleaseChoice>> SuggestAsync(
        string partial, string sessionKey, CancellationToken ct)
    {
        var query = (partial ?? "").Trim();
        if (query.Length < options.MinimumQueryLength) return [];

        var key = Normalize(query);

        lock (_answered)
        {
            if (_answered.TryGetValue(key, out var exact))
                return Present(exact);
        }

        // A query that extends one already answered is narrowed locally. The longest such answer
        // is the closest and the cheapest to filter. It asks the tracker nothing, so it does not
        // wait for typing to stop.
        if (LongestAnsweredPrefix(key) is { } prefix)
        {
            var narrowed = Narrow(prefix.Releases, query);
            if (narrowed.Count > 0)
            {
                logger.LogDebug("Narrowed {Prefix} for {Query} without a tracker call.",
                    prefix.Key, key);
                return Present(narrowed);
            }
        }

        if (!await IsStillTypingSettledAsync(sessionKey, ct))
        {
            logger.LogDebug("{Query} was superseded before the tracker was asked.", key);
            return Present(BestKnown(key));
        }

        // One call at a time, and the deadline covers the queueing as well as the call itself.
        // Refusing to queue at all would answer a second person searching with nothing at the
        // moment somebody else happens to be mid-search.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(options.DeadlineMs);

        try
        {
            await _gate.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug("{Query} did not reach the front of the queue in time.", key);
            return Present(BestKnown(key));
        }

        try
        {
            var ranked = await search.ByTitleAsync(query, deadline.Token);

            lock (_answered) _answered[key] = ranked.Candidates;
            return Present(ranked.Candidates);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug("The tracker did not answer {Query} inside the deadline.", key);
            return Present(BestKnown(key));
        }
        catch (Exception ex)
        {
            // A failing tracker must not surface as a broken command. An empty list reads as
            // "nothing yet", which is what the next keystroke will correct.
            logger.LogWarning(ex, "Autocomplete for {Query} failed.", key);
            return Present(BestKnown(key));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Waits out the debounce window and reports whether this request is still the newest for
    /// its session. A later keystroke replaces the sequence number, which is what tells the
    /// earlier one to stop rather than ask.
    /// </summary>
    private async Task<bool> IsStillTypingSettledAsync(string sessionKey, CancellationToken ct)
    {
        var mine = Interlocked.Increment(ref _sequence);
        lock (_newestPerSession) _newestPerSession[sessionKey] = mine;

        if (options.DebounceMs > 0)
            await Task.Delay(options.DebounceMs, ct);

        lock (_newestPerSession)
            return _newestPerSession.TryGetValue(sessionKey, out var newest) && newest == mine;
    }

    private (string Key, IReadOnlyList<Release> Releases)? LongestAnsweredPrefix(string key)
    {
        lock (_answered)
        {
            return _answered
                .Where(e => key.StartsWith(e.Key, StringComparison.Ordinal))
                .OrderByDescending(e => e.Key.Length)
                .Select(e => ((string, IReadOnlyList<Release>)?)(e.Key, e.Value))
                .FirstOrDefault();
        }
    }

    /// <summary>The closest answer already held, so a keystroke that cannot ask shows something.</summary>
    private IReadOnlyList<Release> BestKnown(string key) =>
        LongestAnsweredPrefix(key) is { } prefix ? Narrow(prefix.Releases, key) : [];

    /// <summary>
    /// Takes the subset of an answer that still matches a longer query, on the words alone: the
    /// tracker is not being asked, so this only removes rows it would have removed.
    /// </summary>
    private static IReadOnlyList<Release> Narrow(IReadOnlyList<Release> releases, string query)
    {
        var tokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return releases
            .Where(r =>
            {
                var haystack = Normalize($"{r.Title} {r.Year} {r.ReleaseName}");
                return tokens.All(t => haystack.Contains(t, StringComparison.Ordinal));
            })
            .ToList();
    }

    /// <summary>
    /// Turns a value the surface sent back into the release it stood for, or null when it was
    /// never offered by this process.
    /// </summary>
    public Release? Resolve(long torrentId)
    {
        lock (_offered) return _offered.GetValueOrDefault(torrentId);
    }

    private IReadOnlyList<ReleaseChoice> Present(IReadOnlyList<Release> releases)
    {
        lock (_offered)
        {
            foreach (var release in releases)
                _offered[release.TorrentId] = release;
        }

        return releases.Select(r => new ReleaseChoice(Label(r), r.TorrentId)).ToList();
    }

    /// <summary>
    /// A row's text, built to fit rather than trimmed to fit: the surface rejects a label over
    /// its limit outright, and the release name is the part that is expendable.
    /// </summary>
    private string Label(Release release)
    {
        var head = release.Year is { } year ? $"{release.Title} ({year})" : release.Title;
        var tail = $" · {release.Summary}";

        // The identifying half is kept whole and the description gives way, because two rows
        // differing only in their cut off description are two rows nobody can choose between.
        if (head.Length + tail.Length <= options.MaximumLabelLength)
            return head + tail;

        var room = options.MaximumLabelLength - head.Length;
        if (room <= 3) return head[..Math.Min(head.Length, options.MaximumLabelLength)];

        return head + tail[..(room - 1)] + "…";
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.ToLowerInvariant()
            .Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries));
}
