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

        services.AddSingleton<ReleaseRanker>();
        services.AddSingleton<DiskBudget>();

        services.AddHttpClient<TrackerClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<TrackerOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            // The tracker serves a browser by default and answers a client it does not recognise
            // inconsistently, so the client names itself rather than going unidentified.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MovieBot/0.1 (+acquire)");
        });

        services.AddSingleton<ReleaseSearch>();

        return services;
    }
}
