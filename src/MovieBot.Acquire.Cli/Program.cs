using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheKrystalShip.MovieBot.Acquire;
using TheKrystalShip.MovieBot.Acquire.Download;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Download;
using TheKrystalShip.MovieBot.Acquire.Search;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddAcquire(builder.Configuration);

// An interactive command writes to stderr so the tables on stdout stay pipeable.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

using var host = builder.Build();
var services = host.Services;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

try
{
    return command switch
    {
        "search" => await SearchAsync(string.Join(' ', args.Skip(1))),
        "imdb" => await ImdbAsync(args.Skip(1).FirstOrDefault()),
        "budget" => Budget(),
        "get" => await GetAsync(args.Skip(1).ToArray()),
        "downloads" => await DownloadsAsync(),
        _ => Help(),
    };
}
catch (QBittorrentException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
catch (TrackerException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    return 130;
}

async Task<int> SearchAsync(string query)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        Console.Error.WriteLine("error: search needs a title.");
        return 2;
    }

    var search = services.GetRequiredService<ReleaseSearch>();
    return Report(await search.ByTitleAsync(query, cancellation.Token));
}

async Task<int> ImdbAsync(string? imdbId)
{
    if (string.IsNullOrWhiteSpace(imdbId))
    {
        Console.Error.WriteLine("error: imdb needs an id, as tt0000000.");
        return 2;
    }

    var search = services.GetRequiredService<ReleaseSearch>();
    return Report(await search.ByImdbAsync(imdbId, cancellation.Token));
}

int Report(RankedReleases ranked)
{
    if (ranked.Candidates.Count == 0)
    {
        Console.Error.WriteLine(ranked.EmptyExplanation);
        return 1;
    }

    var index = 1;
    foreach (var release in ranked.Candidates)
    {
        var flags = release.IsFreeleech ? "FL" : release.IsInternal ? "IN" : "  ";
        Console.WriteLine($"{index,2}. [{flags}] {release.Title}"
                          + (release.Year is { } year ? $" ({year})" : ""));
        Console.WriteLine($"      {release.Summary}");
        Console.WriteLine($"      {release.ReleaseName}");
        Console.WriteLine($"      id={release.TorrentId} category={release.Category}");
        index++;
    }

    // The rejections are the difference between a short list and a broken search, so they are
    // always accounted for rather than only when nothing survives.
    if (ranked.Rejected.Count > 0)
    {
        var counts = ranked.Rejected
            .GroupBy(r => r.Reason)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} {g.Key}");
        Console.Error.WriteLine($"filtered out: {string.Join(", ", counts)}");
    }

    return 0;
}

async Task<int> GetAsync(string[] rest)
{
    // A rank may be given to take something other than the top row, which is how the CLI stands
    // in for somebody picking off a menu.
    var pick = 1;
    var terms = new List<string>();
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] == "--pick" && i + 1 < rest.Length && int.TryParse(rest[i + 1], out var chosen))
        {
            pick = chosen;
            i++;
            continue;
        }

        terms.Add(rest[i]);
    }

    var query = string.Join(' ', terms);
    if (string.IsNullOrWhiteSpace(query))
    {
        Console.Error.WriteLine("error: get needs a title.");
        return 2;
    }

    var ranked = await services.GetRequiredService<ReleaseSearch>()
        .ByTitleAsync(query, cancellation.Token);

    if (ranked.Candidates.Count == 0)
    {
        Console.Error.WriteLine(ranked.EmptyExplanation);
        return 1;
    }

    if (pick < 1 || pick > ranked.Candidates.Count)
    {
        Console.Error.WriteLine($"error: pick must be between 1 and {ranked.Candidates.Count}.");
        return 2;
    }

    var release = ranked.Candidates[pick - 1];
    Console.WriteLine($"{release.ReleaseName}");
    Console.WriteLine($"  {release.Summary}");

    var result = await services.GetRequiredService<AcquisitionService>()
        .StartAsync(release, cancellation.Token);

    if (!result.Started)
    {
        Console.Error.WriteLine($"not started: {result.Refusal}");
        return 1;
    }

    Console.WriteLine($"  started, hash {result.Hash}");
    return 0;
}

async Task<int> DownloadsAsync()
{
    var downloads = await services.GetRequiredService<AcquisitionService>()
        .ListAsync(cancellation.Token);

    if (downloads.Count == 0)
    {
        Console.Error.WriteLine("nothing downloading.");
        return 0;
    }

    foreach (var download in downloads)
    {
        Console.WriteLine($"{download.Name}");
        Console.WriteLine($"  {download.State} · {download.Summary}"
                          + (download.IsSequential ? " · sequential" : ""));
        Console.WriteLine($"  {download.Hash}");
    }

    return 0;
}

int Budget()
{
    var reading = services.GetRequiredService<DiskBudget>().Read();
    Console.WriteLine(reading.Summary);
    Console.WriteLine($"state: {reading.State}");
    return reading.State == BudgetState.Full ? 1 : 0;
}

int Help()
{
    Console.WriteLine("""
        moviebot-acquire — find films on the tracker and judge what is worth taking.

          search <title>     rank what the tracker has under a title
          imdb <tt0000000>   the same, by exact film
          get <title> [--pick N]   start the top result downloading, or the Nth
          downloads          what is downloading now
          budget             what the download directory holds against its ceiling

        The account is configured through Tracker__Username and Tracker__Passkey, in the
        environment or in user-secrets. Neither is ever read from the repository.
        """);
    return 0;
}

/// <summary>Named so user-secrets has an assembly to hang the store off.</summary>
public partial class Program;
