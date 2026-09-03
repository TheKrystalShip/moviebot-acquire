using Microsoft.Extensions.Logging;
using TheKrystalShip.MovieBot.Acquire.Imdb;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>One row of an autocomplete list: what is shown, and what comes back when it is picked.</summary>
/// <param name="Label">The text the chat surface displays, already within its length limit.</param>
/// <param name="TorrentId">
/// What the surface sends back on submit. An id rather than a release name: the name is long
/// enough to be truncated for display, and a truncated name cannot be resolved to a release.
/// </param>
public sealed record ReleaseChoice(string Label, long TorrentId);

/// <summary>
/// What to put in front of somebody: the rows, and — when there are none — the sentence saying
/// why.
///
/// A menu that simply comes back empty is indistinguishable from a broken tracker call, and the
/// two want opposite things from the person reading it. Every reason nothing is on offer is a
/// normal outcome worth showing: the tracker holds nothing under that name, or holds only
/// releases too large to take.
/// </summary>
/// <param name="Explanation">Null whenever there is something to show.</param>
public sealed record Suggestions(IReadOnlyList<ReleaseChoice> Choices, string? Explanation)
{
    public static readonly Suggestions None = new([], null);
}

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
    /// <summary>
    /// One answered query. The film and the reason nothing was offered are held with the releases
    /// because a narrowed or replayed query shows the same row text and the same explanation as
    /// the query it was taken from.
    /// </summary>
    private sealed record Answer(
        IReadOnlyList<Release> Releases, ImdbTitle? Film, string? Explanation);

    private readonly Dictionary<string, Answer> _answered = [];

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
    public async Task<Suggestions> SuggestAsync(
        string partial, string sessionKey, CancellationToken ct)
    {
        var query = (partial ?? "").Trim();
        if (query.Length < options.MinimumQueryLength) return Suggestions.None;

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
            var narrowed = Narrow(prefix.Answer.Releases, query);
            if (narrowed.Count > 0)
            {
                logger.LogDebug("Narrowed {Prefix} for {Query} without a tracker call.",
                    prefix.Key, key);
                return Present(prefix.Answer with { Releases = narrowed });
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
            var ranked = await search.ByTextAsync(query, deadline.Token);
            var answer = new Answer(ranked.Candidates, ranked.Film, ranked.EmptyExplanation);

            lock (_answered) _answered[key] = answer;
            return Present(answer);
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

    private (string Key, Answer Answer)? LongestAnsweredPrefix(string key)
    {
        lock (_answered)
        {
            return _answered
                .Where(e => key.StartsWith(e.Key, StringComparison.Ordinal))
                .OrderByDescending(e => e.Key.Length)
                .Select(e => ((string, Answer)?)(e.Key, e.Value))
                .FirstOrDefault();
        }
    }

    /// <summary>The closest answer already held, so a keystroke that cannot ask shows something.</summary>
    private Answer BestKnown(string key) =>
        LongestAnsweredPrefix(key) is { } prefix
            ? prefix.Answer with { Releases = Narrow(prefix.Answer.Releases, key) }
            : new Answer([], null, null);

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

    private Suggestions Present(Answer answer)
    {
        lock (_offered)
        {
            foreach (var release in answer.Releases)
                _offered[release.TorrentId] = release;
        }

        if (answer.Releases.Count == 0) return new Suggestions([], answer.Explanation);

        return new Suggestions(
            answer.Releases.Select(r => new ReleaseChoice(Label(r, answer.Film), r.TorrentId)).ToList(),
            null);
    }

    /// <summary>
    /// A row's text, built to fit rather than trimmed to fit: the surface rejects a label over
    /// its limit outright, and the release name is the part that is expendable.
    ///
    /// Where the film was named in the title index and the release calls it something else, both
    /// names are shown. A row reading only "Huo zhe yan" under a search for "The Furious" looks
    /// like the wrong film, and there is nothing else on the row to say otherwise.
    /// </summary>
    private string Label(Release release, ImdbTitle? film)
    {
        var head = film is null
            ? release.Display
            : SameName(film, release) ? film.Display : $"{film.Display} · {release.Title}";

        var tail = $" · {release.Summary}";

        // The identifying half is kept whole and the description gives way, because two rows
        // differing only in their cut off description are two rows nobody can choose between.
        if (head.Length + tail.Length <= options.MaximumLabelLength)
            return head + tail;

        var room = options.MaximumLabelLength - head.Length;
        if (room <= 3) return head[..Math.Min(head.Length, options.MaximumLabelLength)];

        return head + tail[..(room - 1)] + "…";
    }

    /// <summary>
    /// Whether the release is already carrying the film's own name, so a row is not made to say
    /// it twice. Compared on the words alone: punctuation and case are a release name's own,
    /// and a film released abroad differs by more than either.
    /// </summary>
    private static bool SameName(ImdbTitle film, Release release) =>
        string.Equals(
            Normalize(film.Title).Replace(" ", ""),
            Normalize(release.Title).Replace(" ", ""),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        string.Join(' ', value.ToLowerInvariant()
            .Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries));
}
