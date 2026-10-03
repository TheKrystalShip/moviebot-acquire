using TheKrystalShip.MovieBot.Acquire.Imdb;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>Why a release was not offered, so an empty menu can explain itself.</summary>
public enum RejectionReason
{
    None = 0,
    NotAFilm,
    NotAShow,
    NoSeeders,
    TooLarge,
    Stereoscopic,
    AboveMaximumResolution,
    WrongYear,
    DifferentFilm,
}

/// <summary>A release that was filtered out, and the reason.</summary>
public sealed record RejectedRelease(Release Release, RejectionReason Reason);

/// <summary>The candidates worth offering, and what was dropped to arrive at them.</summary>
/// <param name="Film">
/// The film the search settled on, when it was found by name in the title index rather than by
/// the words of a release. It is what lets a row show the name somebody typed beside the name the
/// release carries, which for a film released abroad under another name are not the same words.
/// </param>
public sealed record RankedReleases(
    IReadOnlyList<Release> Candidates,
    IReadOnlyList<RejectedRelease> Rejected,
    ImdbTitle? Film = null)
{
    /// <summary>
    /// A sentence naming why nothing is on offer, or null when something is.
    ///
    /// A search that finds a dozen releases and offers none is a normal outcome — every one of
    /// them a 60 GiB remux, say — and a menu that simply comes back empty is indistinguishable
    /// from a broken tracker call.
    /// </summary>
    public string? EmptyExplanation => Rejections.Explain(Candidates.Count, Rejected);
}

/// <summary>Why a search offers nothing, said the same way for a film and a show.</summary>
public static class Rejections
{
    /// <summary>A sentence naming why nothing is on offer, or null when something is.</summary>
    public static string? Explain(int offered, IReadOnlyList<RejectedRelease> rejected)
    {
        if (offered > 0) return null;
        if (rejected.Count == 0) return "The tracker has nothing under that name.";

        var counts = rejected
            .GroupBy(r => r.Reason)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key switch
            {
                RejectionReason.NotAFilm => $"{g.Count()} outside the film categories",
                RejectionReason.NotAShow => $"{g.Count()} outside the show categories",
                RejectionReason.NoSeeders => $"{g.Count()} with nobody seeding",
                RejectionReason.TooLarge => $"{g.Count()} over the size ceiling",
                RejectionReason.Stereoscopic => $"{g.Count()} stereoscopic",
                RejectionReason.AboveMaximumResolution => $"{g.Count()} above the resolution ceiling",
                RejectionReason.WrongYear => $"{g.Count()} from another year",
                RejectionReason.DifferentFilm => $"{g.Count()} for a different film",
                _ => $"{g.Count()} filtered",
            });

        return $"Found {rejected.Count}, offering none: {string.Join(", ", counts)}.";
    }
}

/// <summary>
/// Applies the policy: drops what cannot be played or cannot be fetched, then orders what is
/// left.
/// </summary>
public sealed class ReleaseRanker(SelectionPolicy policy)
{
    /// <param name="releases">Everything the tracker returned, parsed.</param>
    /// <param name="expectedYear">
    /// The year the search asked for, when one was given. The tracker matches words rather than
    /// films, so a query narrowed by a year can still come back carrying a different film that
    /// happens to contain the same tokens.
    /// </param>
    /// <param name="expectedTitle">
    /// The title the search named, when there is one. The tracker matches words rather than
    /// films, so a release can agree on every token it was narrowed by and still be another film.
    /// </param>
    public RankedReleases Rank(
        IEnumerable<Release> releases, int? expectedYear = null, string? expectedTitle = null)
    {
        var candidates = new List<Release>();
        var rejected = new List<RejectedRelease>();
        var ceiling = (long)(policy.MaximumSizeGiB * (1L << 30));

        foreach (var release in releases)
        {
            var reason = Judge(release, ceiling, expectedYear, expectedTitle);
            if (reason == RejectionReason.None)
                candidates.Add(release);
            else
                rejected.Add(new RejectedRelease(release, reason));
        }

        var ordered = Order(candidates, policy.PreferredResolution)
            .Take(policy.MaximumResults)
            .ToList();

        return new RankedReleases(ordered, rejected);
    }

    /// <summary>
    /// What a show's releases offer, season by season.
    ///
    /// Everything returned for a show's id is that show, so nothing is matched by title or year.
    /// A pack is offered under every season it covers, and a single-episode release under its
    /// episode: a finished season is usually a pack and an airing one only episodes, and a person
    /// picking episodes can be served by either.
    /// </summary>
    public ShowReleases RankShow(IEnumerable<Release> releases)
    {
        var candidates = new List<Release>();
        var rejected = new List<RejectedRelease>();
        var ceiling = (long)(policy.MaximumEpisodeSizeGiB * (1L << 30));

        foreach (var release in releases)
        {
            var reason = JudgeEpisodic(release, ceiling);
            if (reason == RejectionReason.None)
                candidates.Add(release);
            else
                rejected.Add(new RejectedRelease(release, reason));
        }

        var ordered = Order(candidates, policy.PreferredShowResolution).ToList();

        var seasons = ordered
            .SelectMany(r => r.Episodic!.Seasons.Select(s => (Season: s, Release: r)))
            .GroupBy(x => x.Season)
            .OrderBy(g => g.Key)
            .Select(g => new SeasonOffer(
                g.Key,
                g.Where(x => x.Release.Episodic!.IsSeasonPack)
                    .Select(x => x.Release).Take(policy.MaximumResults).ToList(),
                g.Where(x => !x.Release.Episodic!.IsSeasonPack)
                    .SelectMany(x => x.Release.Episodic!.Episodes.Select(e => (Episode: e, x.Release)))
                    .GroupBy(x => x.Episode)
                    .OrderBy(e => e.Key)
                    .Select(e => new EpisodeOffer(
                        e.Key, e.Select(x => x.Release).Take(policy.MaximumResults).ToList()))
                    .ToList()))
            .ToList();

        return new ShowReleases(seasons, rejected);
    }

    /// <summary>
    /// Best first. Hi10P sorts below everything else, ahead of the tracker's flags. It plays, but
    /// its transcode runs on the processor rather than the GPU at under half the usual pace, and a
    /// room is waiting on it; a freeleech is not worth that when another release exists.
    /// </summary>
    private IOrderedEnumerable<Release> Order(IEnumerable<Release> candidates, Resolution preferred) =>
        candidates
            .OrderBy(r => r.IsHighBitDepthAvc)
            .ThenByDescending(Tier)
            .ThenByDescending(r => Score(r, preferred))
            .ThenByDescending(r => r.Seeders);

    private RejectionReason Judge(
        Release release, long ceiling, int? expectedYear, string? expectedTitle)
    {
        if (!string.IsNullOrWhiteSpace(expectedTitle)
            && !TitleMatch.Matches(expectedTitle, release.Title))
            return RejectionReason.DifferentFilm;

        // A release whose own year disagrees with the one asked for is a different film. A
        // release that carries no year is not a disagreement, so it stays.
        if (expectedYear is { } wanted && release.Year is { } actual && actual != wanted)
            return RejectionReason.WrongYear;

        // A season or an episode is a show's whatever category it was filed under.
        if (release.IsEpisodic)
            return RejectionReason.NotAFilm;

        if (policy.AllowedCategories.Length > 0
            && !policy.AllowedCategories.Contains(release.Category, StringComparer.OrdinalIgnoreCase))
            return RejectionReason.NotAFilm;

        return JudgeEncode(release, release.SizeBytes, ceiling);
    }

    private RejectionReason JudgeEpisodic(Release release, long ceiling)
    {
        if (!release.IsEpisodic
            || !policy.AllowedShowCategories.Contains(release.Category, StringComparer.OrdinalIgnoreCase))
            return RejectionReason.NotAShow;

        return JudgeEncode(release, release.SizePerEpisode, ceiling);
    }

    /// <summary>What decides whether an encode is worth taking, film or show.</summary>
    private RejectionReason JudgeEncode(Release release, long size, long ceiling)
    {
        if (release.IsThreeDimensional && !policy.AllowThreeDimensional)
            return RejectionReason.Stereoscopic;

        // Unknown stays in: a release whose name does not say what it is may still be the only
        // one there, and the size ceiling already bounds how wrong taking it can go.
        if (release.Resolution != Resolution.Unknown && release.Resolution > policy.MaximumResolution)
            return RejectionReason.AboveMaximumResolution;

        if (release.Seeders < policy.MinimumSeeders)
            return RejectionReason.NoSeeders;

        if (size > ceiling)
            return RejectionReason.TooLarge;

        return RejectionReason.None;
    }

    /// <summary>
    /// The group a release sorts into ahead of any judgement about the encode. Flat when the
    /// policy asks for every factor to be weighed together instead.
    /// </summary>
    private int Tier(Release release)
    {
        if (!policy.TierByTrackerFlags) return 0;
        if (release.IsFreeleech) return 2;
        if (release.IsInternal) return 1;
        return 0;
    }

    /// <summary>
    /// A release's standing within its group, highest first. The weights are ordered so that the
    /// encode decides between two sane releases and availability only breaks a tie: a release
    /// with eight times the seeders of another is not eight times better to watch.
    /// </summary>
    private double Score(Release release, Resolution preferred)
    {
        var score = 0.0;

        // Distance from the preferred resolution. Anything above the ceiling is already gone, so
        // this only ever reaches down.
        var distance = release.Resolution - preferred;
        score += distance switch
        {
            0 => 100,
            > 0 => 70 - (distance - 1) * 15,
            _ => 60 + distance * 25,
        };

        score += release.Source switch
        {
            Source.Remux => 30,
            Source.BluRay => 30,
            Source.Web => 25,
            Source.Dvd => 5,
            Source.Cam => -100,
            _ => 0,
        };

        // Weighed here only when the policy is not tiering by them, so a flag never counts twice.
        if (!policy.TierByTrackerFlags)
        {
            if (release.IsFreeleech) score += 20;
            if (release.IsInternal) score += 10;
        }

        // Availability, flattened: it separates a dead release from a live one without letting a
        // popular poor encode outrank a well-seeded good one.
        score += Math.Log10(release.Seeders + 1) * 10;

        // A film's torrent holding many files is a disc structure rather than one film, and the
        // ingest expects a single feature. A season pack holds one file per episode by design.
        if (!release.IsEpisodic && release.FileCount > 5) score -= 25;

        return score;
    }
}
