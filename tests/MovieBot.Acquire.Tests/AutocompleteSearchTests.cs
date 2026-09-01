using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The chat surface sends a request per keystroke and rejects a label over its length limit, so
/// what is asserted here is the shape of a row and the tracker calls that are not made.
/// </summary>
public class AutocompleteSearchTests
{
    /// <summary>Typing at a speed that keeps every keystroke inside the debounce window.</summary>
    private const int KeystrokeIntervalMs = 20;

    private static Release Release(string name, long id) =>
        ReleaseParser.Parse(new TrackerTorrent
        {
            Id = id,
            Name = name,
            Category = "Movies HD",
            Seeders = 40,
            Size = 8L << 30,
            Freeleech = 1,
        });

    [Fact]
    public async Task A_label_fits_the_surfaces_limit()
    {
        // The longest real release names run past the limit once a summary is added.
        var release = Release(
            "Blade.Runner.2049.2017.2160p.UHD.Blu-ray.REMUX.HEVC.HDR.Atmos.TrueHD.7.1-TERMiNAL", 1);

        var label = await LabelAsync(release);

        Assert.True(label.Length <= new AutocompleteOptions().MaximumLabelLength,
            $"label was {label.Length} characters: {label}");
    }

    [Fact]
    public async Task A_label_keeps_the_title_and_year_whole()
    {
        // Two rows differing only in a cut off description are two rows nobody can choose between.
        var release = Release(
            "Blade.Runner.2049.2017.2160p.UHD.Blu-ray.REMUX.HEVC.HDR.Atmos.TrueHD.7.1-TERMiNAL", 1);

        Assert.StartsWith("Blade Runner 2049 (2017)", await LabelAsync(release));
    }

    [Fact]
    public async Task A_short_query_asks_the_tracker_nothing()
    {
        var search = Build(out var calls, Immediate);

        Assert.Empty(await search.SuggestAsync("he", "user", CancellationToken.None));
        Assert.Equal(0, calls());
    }

    [Fact]
    public async Task Typing_a_whole_query_costs_one_tracker_call()
    {
        // The surface sends a request per character. Answered naively that is a call per
        // keystroke, which is both past what the tracker permits and slower than the typing.
        var search = Build(out var calls, Debounced,
        [
            Release("Heat.1995.1080p.BluRay.DD5.1.x264-EbP", 1),
            Release("Heat.1995.720p.BluRay.DD5.1.x264-EbP", 2),
        ]);

        await TypeAsync(search, "heat 1995", "user");

        Assert.Equal(1, calls());
    }

    [Fact]
    public async Task One_person_typing_does_not_suppress_another()
    {
        // The debounce is per person. Shared, a room where somebody is mid-word would answer
        // everybody else with nothing.
        var search = Build(out var calls, Debounced, [Release("Heat.1995.1080p.BluRay.x264-EbP", 1)]);

        await Task.WhenAll(
            TypeAsync(search, "heat 1995", "first"),
            TypeAsync(search, "dune 2021", "second"));

        Assert.Equal(2, calls());
    }

    [Fact]
    public async Task Narrowing_a_query_keeps_only_what_still_matches()
    {
        var search = Build(out _, Immediate,
        [
            Release("Heat.1995.1080p.BluRay.DD5.1.x264-EbP", 1),
            Release("Heat.2015.1080p.BluRay.DD5.1.x264-OTHER", 2),
        ]);

        await search.SuggestAsync("heat", "user", CancellationToken.None);
        var narrowed = await search.SuggestAsync("heat 1995", "user", CancellationToken.None);

        Assert.Equal(1, Assert.Single(narrowed).TorrentId);
    }

    [Fact]
    public async Task Narrowing_asks_the_tracker_nothing_even_while_typing()
    {
        // Narrowing costs no call, so it must not wait for typing to stop: an answer already
        // held is better shown at once.
        var search = Build(out var calls, Debounced,
        [
            Release("Heat.1995.1080p.BluRay.DD5.1.x264-EbP", 1),
        ]);

        await search.SuggestAsync("heat", "user", CancellationToken.None);
        var before = calls();

        var narrowed = await search.SuggestAsync("heat 1995", "user", CancellationToken.None);

        Assert.Single(narrowed);
        Assert.Equal(before, calls());
    }

    private static AutocompleteOptions Immediate => new() { DebounceMs = 0 };

    private static AutocompleteOptions Debounced => new() { DebounceMs = 500 };

    /// <summary>Types a query one character at a time without waiting for each to be answered.</summary>
    private static async Task TypeAsync(AutocompleteSearch search, string typed, string sessionKey)
    {
        var pending = new List<Task<IReadOnlyList<ReleaseChoice>>>();

        for (var length = 1; length <= typed.Length; length++)
        {
            pending.Add(search.SuggestAsync(typed[..length], sessionKey, CancellationToken.None));
            await Task.Delay(KeystrokeIntervalMs);
        }

        await Task.WhenAll(pending);
    }

    /// <summary>Reaches the label builder the way the surface does, through a suggestion.</summary>
    private static async Task<string> LabelAsync(Release release)
    {
        var search = Build(out _, Immediate, [release]);
        var suggestions = await search.SuggestAsync("blade runner", "user", CancellationToken.None);
        return Assert.Single(suggestions).Label;
    }

    private static AutocompleteSearch Build(
        out Func<int> calls, AutocompleteOptions options, IReadOnlyList<Release>? results = null)
    {
        var count = 0;
        calls = () => Volatile.Read(ref count);

        var stub = new StubReleaseSearch(results ?? [], () => Interlocked.Increment(ref count));
        return new AutocompleteSearch(stub, options, NullLogger<AutocompleteSearch>.Instance);
    }
}
