using TheKrystalShip.MovieBot.Acquire.Search;
using TheKrystalShip.MovieBot.Acquire.Tracker;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// A show's release names its place in the show, and that marker is what separates it from a
/// film and where its title ends. The season and episode names here are shapes the tracker
/// returned for real shows.
/// </summary>
public class EpisodeMarkerTests
{
    private static Release Parse(string name) =>
        ReleaseParser.Parse(new TrackerTorrent { Id = 1, Name = name, Category = "TV HD", Files = 10 });

    [Fact]
    public void A_season_pack_names_its_season_and_no_episodes()
    {
        var marker = EpisodeMarker.Read("Severance.S02.1080p.ATVP.WEB-DL.DDP5.1.H.264-GROUP")!;

        Assert.Equal(2, marker.Season);
        Assert.True(marker.IsSeasonPack);
        Assert.Equal([2], marker.Seasons);
        Assert.Equal("S02", marker.Label);
    }

    [Fact]
    public void An_episode_names_its_season_and_episode()
    {
        var marker = EpisodeMarker.Read("Slow.Horses.S06E02.Daddy.Issues.1080p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-GROUP")!;

        Assert.Equal(6, marker.Season);
        Assert.Equal([2], marker.Episodes);
        Assert.Equal("S06E02", marker.Label);
    }

    [Theory]
    [InlineData("Show.S01E01E02.720p.WEB.H.264-GROUP", new[] { 1, 2 })]
    [InlineData("Show.S01E01-E02.720p.WEB.H.264-GROUP", new[] { 1, 2 })]
    [InlineData("Show.S01E04-E06.720p.WEB.H.264-GROUP", new[] { 4, 5, 6 })]
    [InlineData("Show.S01E04-06.720p.WEB.H.264-GROUP", new[] { 4, 5, 6 })]
    public void A_run_of_episodes_names_each_of_them(string name, int[] episodes) =>
        Assert.Equal(episodes, EpisodeMarker.Read(name)!.Episodes);

    [Fact]
    public void A_resolution_after_a_dash_is_not_an_episode()
    {
        var marker = EpisodeMarker.Read("Show.S01E01-1080p.WEB")!;

        Assert.Equal([1], marker.Episodes);
    }

    [Theory]
    [InlineData("Show.S01-S03.1080p.BluRay.x264-GROUP")]
    [InlineData("Show.S01-03.1080p.BluRay.x264-GROUP")]
    public void A_pack_of_several_seasons_covers_each(string name)
    {
        var marker = EpisodeMarker.Read(name)!;

        Assert.True(marker.IsSeasonPack);
        Assert.Equal([1, 2, 3], marker.Seasons);
        Assert.Equal("S01–S03", marker.Label);
    }

    [Theory]
    [InlineData("Blade.Runner.2049.2017.1080p.BluRay.x264-GROUP")]
    [InlineData("Se7en.1995.1080p.BluRay.x264-GROUP")]
    [InlineData("S1m0ne.2002.1080p.WEB-DL.H.264-GROUP")]
    [InlineData("Heat.1995.1080p.BluRay.DD5.1.x264-AMIABLE")]
    public void A_film_carries_no_marker(string name) =>
        Assert.Null(EpisodeMarker.Read(name));

    [Fact]
    public void A_show_s_title_ends_at_its_marker()
    {
        var release = Parse("Severance.S02.1080p.ATVP.WEB-DL.DDP5.1.H.264-GROUP");

        Assert.Equal("Severance", release.Title);
        Assert.Null(release.Year);
        Assert.True(release.IsEpisodic);
        Assert.Equal(Resolution.Hd1080, release.Resolution);
        Assert.Equal("Severance S02", release.Display);
    }

    [Fact]
    public void An_episode_s_own_name_is_not_part_of_the_show_s_title()
    {
        var release = Parse("Slow.Horses.S06E02.Daddy.Issues.1080p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-GROUP");

        Assert.Equal("Slow Horses", release.Title);
        Assert.Equal("Slow Horses S06E02", release.Display);
        Assert.Equal(1, release.EpisodeCount);
    }

    [Fact]
    public void A_show_named_with_its_year_keeps_the_year()
    {
        var release = Parse("Doctor.Who.2005.S01.720p.BluRay.x264-GROUP");

        Assert.Equal("Doctor Who", release.Title);
        Assert.Equal(2005, release.Year);
    }

    [Fact]
    public void A_pack_s_size_per_episode_divides_by_its_files()
    {
        var release = ReleaseParser.Parse(new TrackerTorrent
        {
            Id = 1, Name = "Severance.S02.1080p.ATVP.WEB-DL.DDP5.1.H.264-GROUP", Category = "TV HD",
            Files = 10, Size = 40L << 30,
        });

        Assert.Equal(10, release.EpisodeCount);
        Assert.Equal(4L << 30, release.SizePerEpisode);
    }

    [Fact]
    public void A_film_is_not_episodic()
    {
        var release = Parse("Heat.1995.1080p.BluRay.DD5.1.x264-AMIABLE");

        Assert.False(release.IsEpisodic);
        Assert.Equal("Heat (1995)", release.Display);
    }
}
