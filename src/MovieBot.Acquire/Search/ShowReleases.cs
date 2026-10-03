using TheKrystalShip.MovieBot.Acquire.Imdb;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>The releases that carry one episode on its own, best first.</summary>
public sealed record EpisodeOffer(int Episode, IReadOnlyList<Release> Releases);

/// <summary>
/// What the tracker offers for one season: packs holding the whole of it, and releases holding
/// single episodes, each best first. A finished season is mostly packs and an airing one only
/// episodes; either can serve a person picking particular episodes, since a pack downloads only
/// the files picked from it.
/// </summary>
public sealed record SeasonOffer(int Season, IReadOnlyList<Release> Packs, IReadOnlyList<EpisodeOffer> Episodes)
{
    /// <summary>The best pack at one resolution, or null when none is offered at it.</summary>
    public Release? PackAt(Resolution resolution) =>
        Packs.FirstOrDefault(p => p.Resolution == resolution);

    /// <summary>The best single-episode release at one resolution, or null.</summary>
    public Release? EpisodeAt(int episode, Resolution resolution) =>
        Episodes.FirstOrDefault(e => e.Episode == episode)?.Releases
            .FirstOrDefault(r => r.Resolution == resolution);
}

/// <summary>A show's releases, season by season, and what was dropped to arrive at them.</summary>
/// <param name="Show">The show the search was for, when the title index named it.</param>
public sealed record ShowReleases(
    IReadOnlyList<SeasonOffer> Seasons,
    IReadOnlyList<RejectedRelease> Rejected,
    ImdbTitle? Show = null)
{
    /// <summary>A sentence naming why nothing is on offer, or null when something is.</summary>
    public string? EmptyExplanation => Rejections.Explain(Seasons.Count, Rejected);

    /// <summary>One season's offer, or null when the tracker holds nothing usable for it.</summary>
    public SeasonOffer? Season(int season) => Seasons.FirstOrDefault(s => s.Season == season);
}
