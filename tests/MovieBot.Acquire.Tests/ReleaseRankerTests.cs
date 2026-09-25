using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public class ReleaseRankerTests
{
    private static readonly SelectionPolicy Policy = new() { AllowedCategories = ["Movies HD", "Movies 4K"] };

    private static Release Release(
        string name, string category = "Movies HD", int seeders = 20,
        long sizeBytes = 8L << 30, bool freeleech = false, bool @internal = false) =>
        ReleaseParser.Parse(new TrackerTorrent
        {
            Id = 1,
            Name = name,
            Category = category,
            Seeders = seeders,
            Size = sizeBytes,
            Freeleech = freeleech ? 1 : 0,
            Internal = @internal ? 1 : 0,
        });

    [Fact]
    public void Drops_a_release_whose_year_is_not_the_year_asked_for()
    {
        // The tracker matches words, not films: a search for the 1995 film returns the 2015 one
        // when both carry the same title.
        var ranked = new ReleaseRanker(Policy).Rank(
            [
                Release("Heat.1995.1080p.BluRay.x264-AMIABLE"),
                Release("Heat.2015.1080p.BluRay.x264-SOMEONE"),
            ],
            expectedYear: 1995);

        var offered = Assert.Single(ranked.Candidates);
        Assert.Equal(1995, offered.Year);
        Assert.Equal(RejectionReason.WrongYear, Assert.Single(ranked.Rejected).Reason);
    }

    [Fact]
    public void Keeps_a_release_that_carries_no_year_at_all()
    {
        // An absent year is not a disagreement, and the release may be the only one there.
        var ranked = new ReleaseRanker(Policy).Rank(
            [Release("Heat.1080p.BluRay.x264-NOBODY")], expectedYear: 1995);

        Assert.Single(ranked.Candidates);
    }

    [Fact]
    public void Does_not_offer_anything_above_the_resolution_ceiling()
    {
        var ranked = new ReleaseRanker(Policy).Rank(
            [Release("Heat.1995.2160p.UHD.BluRay.x265-GROUP", category: "Movies 4K")],
            expectedYear: 1995);

        Assert.Empty(ranked.Candidates);
        Assert.Equal(RejectionReason.AboveMaximumResolution, Assert.Single(ranked.Rejected).Reason);
    }

    [Fact]
    public void Orders_freeleech_first_then_internal_then_the_rest()
    {
        var ranked = new ReleaseRanker(Policy).Rank(
            [
                Release("Heat.1995.1080p.BluRay.x264-PLAIN"),
                Release("Heat.1995.1080p.BluRay.x264-INTERNAL", @internal: true),
                Release("Heat.1995.1080p.BluRay.x264-FREE", freeleech: true),
            ],
            expectedYear: 1995);

        Assert.Equal(
            ["FREE", "INTERNAL", "PLAIN"],
            ranked.Candidates.Select(r => r.Group));
    }

    [Fact]
    public void Explains_itself_when_it_offers_nothing()
    {
        var ranked = new ReleaseRanker(Policy).Rank(
            [Release("Heat.1995.1080p.BluRay.x264-DEAD", seeders: 0)], expectedYear: 1995);

        Assert.Empty(ranked.Candidates);
        Assert.Contains("nobody seeding", ranked.EmptyExplanation);
    }

    [Fact]
    public void Drops_a_soundtrack_that_matched_the_title()
    {
        var ranked = new ReleaseRanker(Policy).Rank(
            [Release("VA-Heat.OST.CD.FLAC.1995-MusicBits", category: "FLAC")]);

        Assert.Empty(ranked.Candidates);
        Assert.Equal(RejectionReason.NotAFilm, Assert.Single(ranked.Rejected).Reason);
    }
}
