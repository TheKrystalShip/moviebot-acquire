using TheKrystalShip.MovieBot.Acquire.Subtitles;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Two subtitles for the same film never agree cue for cue, because where one author breaks a
/// line is their own choice. What they do agree on is when somebody starts speaking, and the
/// measurement has to survive the disagreement to find that.
/// </summary>
public class SubtitleSyncTests
{
    /// <summary>
    /// Cues at irregular intervals, because real dialogue is irregular and evenly spaced cues are
    /// a degenerate case: every offset by one interval then aligns exactly as well as the right one.
    /// </summary>
    private static List<double> Cues(int count, double start = 60, int seed = 7)
    {
        var times = new List<double>(count);
        var at = start;
        var state = (uint)seed;

        for (var i = 0; i < count; i++)
        {
            times.Add(Math.Round(at, 3));
            state = state * 1664525 + 1013904223;
            at += 1.5 + (state >> 16) % 7000 / 1000.0;
        }

        return times;
    }

    /// <summary>Evenly spaced cues, which is the shape no offset can be told apart in.</summary>
    private static List<double> EvenCues(int count, double start = 60, double gap = 4.3)
        => Enumerable.Range(0, count).Select(i => start + i * gap).ToList();

    [Fact]
    public void MeasuresNoShiftBetweenIdenticalTimings()
    {
        var cues = Cues(400);

        var result = SubtitleSync.Against(cues, cues);

        Assert.NotNull(result);
        Assert.Equal(0, result.ShiftSeconds);
        Assert.Equal(400, result.Aligned);
    }

    [Theory]
    [InlineData(2.5)]
    [InlineData(-2.5)]
    [InlineData(11.0)]
    public void FindsAConstantOffset(double offset)
    {
        var reference = Cues(400);
        var candidate = reference.Select(t => t - offset).ToList();

        var result = SubtitleSync.Against(reference, candidate);

        Assert.NotNull(result);
        Assert.Equal(offset, result.ShiftSeconds, 2);
        Assert.True(result.AlignedFraction > 0.99);
    }

    [Fact]
    public void RefusesTimingThatStretchesRatherThanShifts()
    {
        // No single offset fits a subtitle whose timing runs progressively further out, so there
        // is no honest answer to give and the useful report is that it does not align.
        var reference = Cues(400);
        var candidate = reference.Select((t, i) => t - i * 0.02).ToList();

        Assert.Null(SubtitleSync.Against(reference, candidate));
    }

    [Fact]
    public void AFrameRateMismatchDoesNotAlignAtAll()
    {
        // A subtitle timed at 25 fps against a 23.976 encode is minutes out by the end, which is
        // far beyond any offset worth searching. The honest answer is that it does not fit, not a
        // drift figure computed from an offset that was never found.
        var reference = Cues(400);
        var candidate = reference.Select(t => t * (25.0 / 23.976)).ToList();

        var result = SubtitleSync.Against(reference, candidate);

        Assert.True(result is null || result.AlignedFraction < 0.5,
            $"a 25 fps subtitle appeared to fit, aligning {result?.AlignedFraction:P0}");
    }

    [Fact]
    public void AnyOffsetItReportsForEvenlySpacedCuesActuallyAligns()
    {
        // Evenly spaced cues fit equally well one interval out, so which offset comes back is
        // arbitrary. What must hold is that whatever it reports is one that genuinely aligns.
        var reference = EvenCues(400);
        var candidate = EvenCues(400).Select(t => t - 2.5).ToList();

        var result = SubtitleSync.Against(reference, candidate);

        Assert.NotNull(result);
        Assert.True(result.AlignedFraction > 0.95);
    }

    [Fact]
    public void SurvivesAnAuthorSplittingLinesDifferently()
    {
        // Only three cues in four are shared; the rest are one author's own line breaks.
        var reference = Cues(400);
        var candidate = reference.Where((_, i) => i % 4 != 0).Select(t => t - 3.0).ToList();

        var result = SubtitleSync.Against(reference, candidate);

        Assert.NotNull(result);
        Assert.Equal(3.0, result.ShiftSeconds, 2);
    }

    [Fact]
    public void ScattersRatherThanInventingAnOffsetForAnUnrelatedFilm()
    {
        var reference = Cues(400);
        var unrelated = Cues(400, start: 73.7, seed: 999);

        var result = SubtitleSync.Against(reference, unrelated);

        Assert.True(result is null || result.AlignedFraction < 0.5,
            $"an unrelated subtitle aligned {result?.AlignedFraction:P0} of its cues");
    }

    [Fact]
    public void RefusesToMeasureTooFewCues()
    {
        Assert.Null(SubtitleSync.Against(Cues(400), Cues(5)));
        Assert.Null(SubtitleSync.Against(Cues(5), Cues(400)));
    }

    [Fact]
    public void ReadsCueStartsFromBothFormats()
    {
        const string srt = "1\n00:01:58,991 --> 00:02:00,367\nline\n\n2\n01:00:00,500 --> 01:00:02,000\nline\n";
        const string vtt = "WEBVTT\n\n01:55.033 --> 02:00.371\nline\n\n01:00:00.500 --> 01:00:02.000\nline\n";

        Assert.Equal([118.991, 3600.5], CueTimings.Read(srt));
        Assert.Equal([115.033, 3600.5], CueTimings.Read(vtt));
    }

    [Fact]
    public void ReadsATwoDigitFractionAsHundredths()
    {
        Assert.Equal([1.05], CueTimings.Read("00:01.05 --> 00:03.00\nline\n"));
    }
}
