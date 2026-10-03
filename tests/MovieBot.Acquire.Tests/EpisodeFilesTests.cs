using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>Which file in a show's torrent is which episode.</summary>
public class EpisodeFilesTests
{
    private const string Pack = "Severance.S02.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP";

    [Fact]
    public void Each_file_is_placed_by_the_marker_in_its_own_name()
    {
        var files = EpisodeFiles.Map(
        [
            ($"{Pack}/Severance.S02E02.Goodbye.Mrs.Selvig.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP.mkv", 1_300_000_000L),
            ($"{Pack}/Severance.S02E01.Hello.Ms.Cobel.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP.mkv", 1_400_000_000L),
            ($"{Pack}/Severance.S02E01.Sample.mkv", 40_000_000L),
            ($"{Pack}/GROUP.nfo", 4_000L),
        ], EpisodeMarker.Read(Pack));

        Assert.Equal([new EpisodeNumber(2, 1), new EpisodeNumber(2, 2)], files.Select(f => f.Episode));
        Assert.EndsWith("Hello.Ms.Cobel.720p.ATVP.WEB-DL.DDP5.1.H.264-GROUP.mkv", files[0].Path);
    }

    [Fact]
    public void A_pack_naming_its_files_only_by_number_is_placed_in_name_order()
    {
        var files = EpisodeFiles.Map(
        [
            ("Show.S03.720p/02.mkv", 1L << 30),
            ("Show.S03.720p/01.mkv", 1L << 30),
        ], EpisodeMarker.Read("Show.S03.720p.WEB.H.264-GROUP"));

        Assert.Equal([new EpisodeNumber(3, 1), new EpisodeNumber(3, 2)], files.Select(f => f.Episode));
        Assert.Equal("Show.S03.720p/01.mkv", files[0].Path);
    }

    [Fact]
    public void A_double_episode_is_one_file_holding_both()
    {
        var file = Assert.Single(EpisodeFiles.Map(
            [("Show.S01E01E02.720p.WEB.H.264-GROUP.mkv", 2L << 30)],
            EpisodeMarker.Read("Show.S01E01E02.720p.WEB.H.264-GROUP")));

        Assert.Equal([new EpisodeNumber(1, 1), new EpisodeNumber(1, 2)], file.Episodes);
    }

    [Fact]
    public void A_single_episode_release_without_a_marker_in_its_file_takes_the_release_s()
    {
        var file = Assert.Single(EpisodeFiles.Map(
            [("Slow.Horses.S06E02.1080p/episode.mkv", 3L << 30)],
            EpisodeMarker.Read("Slow.Horses.S06E02.Daddy.Issues.1080p.ATVP.WEB-DL-GROUP")));

        Assert.Equal(new EpisodeNumber(6, 2), file.Episode);
    }

    [Fact]
    public void A_film_s_torrent_places_nothing() =>
        Assert.Empty(EpisodeFiles.Map([("Heat.1995.1080p/Heat.1995.1080p.mkv", 9L << 30)], null));
}
