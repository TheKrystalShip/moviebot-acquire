using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Download;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;

namespace TheKrystalShip.MovieBot.Acquire;

public static class AcquireServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tracker client, the selection policy and the disk budget.
    ///
    /// The passkey is bound from configuration like any other setting, which puts it in the
    /// environment or in user-secrets and never in a file under the repository.
    /// </summary>
    public static IServiceCollection AddAcquire(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TrackerOptions>(configuration.GetSection(TrackerOptions.Section));
        services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.Section));

        services.AddSingleton(_ =>
        {
            var policy = new SelectionPolicy();
            configuration.GetSection(SelectionPolicy.Section).Bind(policy);
            return policy;
        });

        services.AddSingleton(_ =>
        {
            var autocomplete = new AutocompleteOptions();
            configuration.GetSection(AutocompleteOptions.Section).Bind(autocomplete);
            return autocomplete;
        });

        services.AddSingleton<ReleaseRanker>();
        services.AddSingleton<DiskBudget>();

        services.Configure<QBittorrentOptions>(configuration.GetSection(QBittorrentOptions.Section));

        services.AddHttpClient<QBittorrentClient>((sp, http) =>
        {
            var qb = sp.GetRequiredService<IOptions<QBittorrentOptions>>().Value;
            http.BaseAddress = new Uri(qb.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(qb.TimeoutSeconds);

            // The client validates the Referer on anything that changes state, and rejects a
            // request carrying none as a cross-site attempt.
            http.DefaultRequestHeaders.Referrer = new Uri(qb.BaseUrl);
        });

        services.AddSingleton<AcquisitionService>();

        // Validated at startup because the failure mode of a bad window is a tracker penalty.
        services.AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.Section))
            .Validate(o => o.IsCoherent,
                "Retention:SeedDays must be at least Retention:TrackerMinimumHours plus "
                + "Retention:MarginHours, and none of them may be negative.")
            .ValidateOnStart();
        services.AddSingleton<Retention>();

        services.AddHttpClient<TrackerClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<TrackerOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            // The tracker serves a browser by default and answers a client it does not recognise
            // inconsistently, so the client names itself rather than going unidentified.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MovieBot/0.1 (+acquire)");
        });

        // No key, and named as itself: the index answers an unidentified client inconsistently.
        services.AddHttpClient<Imdb.ImdbClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MovieBot/0.1 (+acquire)");
        });

        services.AddSingleton<ReleaseSearch>();
        services.AddSingleton<IReleaseSearch>(sp => sp.GetRequiredService<ReleaseSearch>());
        services.AddSingleton<AutocompleteSearch>();

        return services;
    }
}
