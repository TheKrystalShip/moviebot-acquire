using TheKrystalShip.MovieBot.Acquire.Tracker;
using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The release name is the only description of an encode the tracker carries, so everything the
/// ranker weighs is parsed from it. Every name here is one the tracker actually returned.
/// </summary>
public class ReleaseParserTests
{
    private static Release Parse(string name, string category = "Movies HD") =>
        ReleaseParser.Parse(new TrackerTorrent { Id = 1, Name = name, Category = category });

    [Fact]
    public void Reads_the_release_year_and_not_a_year_in_the_title()
    {
        // Both 2049 and 2017 are plausible years. The release year is the later token and the
        // one nearest the quality markers; the other belongs to the film.
        var release = Parse("Blade.Runner.2049.2017.1080p.BluRay.DD5.1.x264-playHD");

        Assert.Equal("Blade Runner 2049", release.Title);
        Assert.Equal(2017, release.Year);
    }

    [Fact]
    public void Reads_a_UHD_sourced_1080p_encode_as_1080p()
    {
        // "UHD" here names the disc the encode came from, not the encode. Reading it as a
        // resolution would rank this above the genuine 2160p releases beside it.
        var release = Parse("Blade.Runner.2049.2017.1080p.UHD.BluRay.DD+7.1.DoVi.HDR.x265-SA89");

        Assert.Equal(Resolution.Hd1080, release.Resolution);
        Assert.Equal(Source.BluRay, release.Source);
        Assert.Equal(DynamicRange.DolbyVision, release.DynamicRange);
    }

    [Fact]
    public void Reads_a_remux_as_a_remux_rather_than_the_disc_it_names()
    {
        var release = Parse("Blade.Runner.2049.2017.1080p.Remux.AVC.DTS-HD.MA.5.1-playBD");

        Assert.Equal(Source.Remux, release.Source);
        Assert.Equal("playBD", release.Group);
    }

    [Theory]
    [InlineData("Blade.Runner.2049.2017.1080p.3D.H-SBS.BluRay.x264-PSYCHD")]
    [InlineData("Blade.Runner.2049.2017.GER.3D.1080p.Blu-ray.AVC.DD.5.1-Kuspalazlari")]
    public void Flags_stereoscopic_releases(string name) =>
        Assert.True(Parse(name).IsThreeDimensional);

    [Fact]
    public void Does_not_flag_an_ordinary_release_as_stereoscopic()
    {
        // A substring test reads "3D" out of any name that happens to contain the pair.
        Assert.False(Parse("Blade.Runner.2049.2017.2160p.MA.WEB-DL.TrueHD.Atmos.7.1.H.265-FLUX")
            .IsThreeDimensional);
    }

    [Fact]
    public void Reads_a_web_release()
    {
        var release = Parse("Blade.Runner.2049.2017.2160p.MA.WEB-DL.TrueHD.Atmos.7.1.DoVi.HDR.H.265-FLUX");

        Assert.Equal(Resolution.Uhd2160, release.Resolution);
        Assert.Equal(Source.Web, release.Source);
        Assert.Equal("FLUX", release.Group);
    }

    [Fact]
    public void Reads_a_DVD_release_as_standard_definition()
    {
        var release = Parse("Blade.Runner.2049.2017.RETAiL.PAL.DVD9-No1");

        Assert.Equal(Resolution.Sd, release.Resolution);
        Assert.Equal(Source.Dvd, release.Source);
        Assert.Equal(2017, release.Year);
    }

    [Fact]
    public void Carries_a_release_with_no_year_rather_than_failing()
    {
        var release = Parse("Some.Untagged.Release.1080p.BluRay.x264-NOBODY");

        Assert.Null(release.Year);
        Assert.Equal("Some Untagged Release", release.Title);
    }
}
