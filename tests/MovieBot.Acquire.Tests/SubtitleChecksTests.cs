using TheKrystalShip.MovieBot.Acquire.Subtitles;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The checks decide what a person sees before spending one of a hundred daily downloads, so the
/// distinction that matters most here is between a subtitle that is wrong and one that simply
/// says nothing about itself. Most uploads declare no frame rate and almost none are indexed
/// against a particular file; treating that silence as a fault would condemn nearly all of them.
/// </summary>
public class SubtitleChecksTests
{
    private static readonly SubtitleTarget Ours = new()
    {
        Release = "The.Devil.Wears.Prada.2006.720p.BluRay.DD5.1.x264-playHD",
        FrameRate = 23.976,
        MovieHash = "b34d4cae8361d877"
    };

    private static SubtitleCandidate Candidate(
        string release = "", double fps = 0, bool hash = false, int cds = 1,
        bool machine = false, bool foreignOnly = false, int downloads = 100) =>
        new()
        {
            FileId = 1,
            Release = release,
            Fps = fps,
            HashMatch = hash,
            CdCount = cds,
            MachineTranslated = machine,
            ForeignPartsOnly = foreignOnly,
            DownloadCount = downloads
        };

    private static SubtitleCheck Check(CheckedSubtitle result, string name) =>
        result.Checks.First(c => c.Name == name);

    [Fact]
    public void AHashMatchOutranksEverythingElse()
    {
        var exact = SubtitleChecks.For(Candidate(hash: true), Ours);
        var popular = SubtitleChecks.For(Candidate(downloads: 500_000, fps: 23.976), Ours);

        Assert.Equal(CheckResult.Match, Check(exact, "This file").Result);
        Assert.True(exact.Score > popular.Score);
    }

    [Fact]
    public void AMissingHashIsUnknownRatherThanAFailure()
    {
        var result = SubtitleChecks.For(Candidate(), Ours);

        Assert.Equal(CheckResult.Unknown, Check(result, "This file").Result);
        Assert.False(result.HasMismatch);
    }

    [Fact]
    public void AFrameRateThatDriftsIsAMismatchAndSaysSo()
    {
        var result = SubtitleChecks.For(Candidate(fps: 25.0), Ours);

        var check = Check(result, "Frame rate");
        Assert.Equal(CheckResult.Mismatch, check.Result);
        Assert.Contains("25", check.Detail);
        Assert.Contains("23.976", check.Detail);
    }

    [Fact]
    public void AnUndeclaredFrameRateIsUnknown()
    {
        Assert.Equal(CheckResult.Unknown, Check(SubtitleChecks.For(Candidate(), Ours), "Frame rate").Result);
    }

    [Fact]
    public void TheSameReleaseGroupIsTheStrongestReleaseSignal()
    {
        var same = SubtitleChecks.For(
            Candidate("The.Devil.Wears.Prada.2006.1080p.BluRay.x264-playHD"), Ours);
        var otherGroup = SubtitleChecks.For(
            Candidate("The.Devil.Wears.Prada.2006.1080p.BluRay.x264-WiKi"), Ours);

        Assert.Equal(CheckResult.Match, Check(same, "Release").Result);
        Assert.True(same.Score > otherGroup.Score);
    }

    [Fact]
    public void AnotherGroupOffTheSameDiscStillMatches()
    {
        // Measured rather than assumed: a Blu-ray subtitle from a different group needed no shift
        // at all against our Blu-ray encode.
        var result = SubtitleChecks.For(
            Candidate("The.Devil.Wears.Prada.2006.BluRay.1080p.x264.DTS-WiKi", fps: 23.976), Ours);

        Assert.Equal(CheckResult.Match, Check(result, "Release").Result);
        Assert.False(result.HasMismatch);
    }

    [Fact]
    public void ADifferentKindOfSourceIsAMismatch()
    {
        var result = SubtitleChecks.For(
            Candidate("The.Devil.Wears.Prada.2006.DVDRip.XviD-DoNE"), Ours);

        var check = Check(result, "Release");
        Assert.Equal(CheckResult.Mismatch, check.Result);
        Assert.Contains("DVD", check.Detail);
    }

    [Fact]
    public void ASubtitleSplitAcrossDiscsIsRejectedOnSight()
    {
        var result = SubtitleChecks.For(Candidate(cds: 2), Ours);

        Assert.True(result.HasMismatch);
        Assert.Contains(result.Checks, c => c.Detail.Contains("2 discs"));
    }

    [Fact]
    public void MachineTranslationAndPartialCoverageAreBothMismatches()
    {
        Assert.True(SubtitleChecks.For(Candidate(machine: true), Ours).HasMismatch);
        Assert.True(SubtitleChecks.For(Candidate(foreignOnly: true), Ours).HasMismatch);
    }

    [Fact]
    public void PopularityNeverOutranksAMeasurement()
    {
        var popularButDrifting = SubtitleChecks.For(Candidate(fps: 25.0, downloads: 900_000), Ours);
        var obscureButRight = SubtitleChecks.For(Candidate(fps: 23.976, downloads: 3), Ours);

        Assert.True(obscureButRight.Score > popularButDrifting.Score);
    }
}
