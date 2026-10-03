using TheKrystalShip.MovieBot.Acquire.Search;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// A show's releases come back in one search by the show's id, as packs for finished seasons and
/// single episodes for an airing one. The sizes and file counts here are the ones measured.
/// </summary>
public class ShowRankingTests
{
    private static readonly SelectionPolicy Policy = new()
    {
        AllowedCategories = ["Movies HD"],
        AllowedShowCategories = ["TV HD", "TV SD", "TV 4K"],
    };

    private static Release Release(string name, double gib, int files = 1, string category = "TV HD", int seeders = 50) =>
        ReleaseParser.Parse(new TrackerTorrent
        {
            Id = name.GetHashCode(), Name = name, Category = category, Seeders = seeders,
            Size = (long)(gib * (1L << 30)), Files = files,
        });

    private static readonly Release[] Severance =
    [
        Release("Severance.S02.1080p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", 38.0, 10),
        Release("Severance.S02.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", 13.1, 10),
        Release("Severance.S02.2160p.ATVP.WEB-DL.DDP5.1.H.265-GROUP", 74.8, 10, "TV 4K"),
        Release("Severance.S01.1080p.BluRay.DD+5.1.x264-GROUP", 44.5, 9),
        Release("Severance.S01.720p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-OTHER", 12.1, 9),
    ];

    [Fact]
    public void A_season_pack_is_judged_by_its_episodes_not_its_total()
    {
        // 38 GiB over ten episodes is an ordinary 1080p encode, far over any film's ceiling.
        var ranked = new ReleaseRanker(Policy).RankShow(Severance);

        Assert.Contains(ranked.Season(2)!.Packs, r => r.Resolution == Resolution.Hd1080);
        Assert.DoesNotContain(ranked.Rejected, r => r.Reason == RejectionReason.TooLarge);
    }

    [Fact]
    public void Seasons_are_in_order_and_720p_is_offered_first()
    {
        var ranked = new ReleaseRanker(Policy).RankShow(Severance);

        Assert.Equal([1, 2], ranked.Seasons.Select(s => s.Season));
        Assert.Equal(Resolution.Hd720, ranked.Season(2)!.Packs[0].Resolution);
        Assert.NotNull(ranked.Season(2)!.PackAt(Resolution.Hd1080));
        Assert.NotNull(ranked.Season(1)!.PackAt(Resolution.Hd720));
    }

    [Fact]
    public void Above_the_resolution_ceiling_is_not_offered()
    {
        var ranked = new ReleaseRanker(Policy).RankShow(Severance);

        var dropped = Assert.Single(ranked.Rejected);
        Assert.Equal(RejectionReason.AboveMaximumResolution, dropped.Reason);
    }

    [Fact]
    public void An_airing_season_is_offered_episode_by_episode()
    {
        var ranked = new ReleaseRanker(Policy).RankShow(
        [
            Release("Slow.Horses.S06E01.Circle.of.Life.720p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-GROUP", 1.2),
            Release("Slow.Horses.S06E01.Circle.of.Life.1080p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-GROUP", 3.6),
            Release("Slow.Horses.S06E02.Daddy.Issues.720p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-GROUP", 1.2),
            Release("Slow.Horses.S05.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", 6.7, 6),
        ]);

        var airing = ranked.Season(6)!;
        Assert.Empty(airing.Packs);
        Assert.Equal([1, 2], airing.Episodes.Select(e => e.Episode));
        Assert.NotNull(airing.EpisodeAt(1, Resolution.Hd1080));
        Assert.Null(airing.EpisodeAt(2, Resolution.Hd1080));
        Assert.Single(ranked.Season(5)!.Packs);
    }

    [Fact]
    public void A_pack_of_several_seasons_is_offered_under_each()
    {
        var ranked = new ReleaseRanker(Policy).RankShow(
            [Release("Show.S01-S03.720p.BluRay.x264-GROUP", 30, 30)]);

        Assert.Equal([1, 2, 3], ranked.Seasons.Select(s => s.Season));
    }

    [Fact]
    public void A_release_outside_the_show_categories_is_not_a_show()
    {
        var ranked = new ReleaseRanker(Policy).RankShow(
        [
            Release("Severance.S02.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", 13.1, 10, category: "Games PC"),
            Release("Severance.2006.1080p.BluRay.DD5.1.x264-GROUP", 10, 1, category: "Movies HD"),
        ]);

        Assert.Empty(ranked.Seasons);
        Assert.All(ranked.Rejected, r => Assert.Equal(RejectionReason.NotAShow, r.Reason));
        Assert.Equal("Found 2, offering none: 2 outside the show categories.", ranked.EmptyExplanation);
    }

    [Fact]
    public void Without_show_categories_no_show_is_offered()
    {
        var ranked = new ReleaseRanker(new SelectionPolicy { AllowedCategories = ["Movies HD"] })
            .RankShow(Severance);

        Assert.Empty(ranked.Seasons);
    }

    [Fact]
    public void A_film_search_never_offers_a_show()
    {
        // A season filed under a film category is still a season.
        var ranked = new ReleaseRanker(Policy).Rank(
        [
            Release("Severance.S02.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", 13.1, 10, category: "Movies HD"),
            Release("Severance.2006.1080p.BluRay.DD5.1.x264-GROUP", 10, 1, category: "Movies HD"),
        ]);

        var offered = Assert.Single(ranked.Candidates);
        Assert.False(offered.IsEpisodic);
        Assert.Equal(RejectionReason.NotAFilm, Assert.Single(ranked.Rejected).Reason);
    }
}
