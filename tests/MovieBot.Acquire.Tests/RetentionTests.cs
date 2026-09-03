using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The rule is measured in seeding time and never removes anything under the tracker's minimum.
/// A wrong answer here is either a film gone early or a penalty on the account, so the edges are
/// what is checked.
/// </summary>
public class RetentionTests
{
    private static Retention Rule(double seedDays = 7, double minimumHours = 48, double marginHours = 12) =>
        new(Options.Create(new RetentionOptions
        {
            SeedDays = seedDays, TrackerMinimumHours = minimumHours, MarginHours = marginHours,
        }));

    private static DownloadStatus Seeded(TimeSpan seeded, DownloadState state = DownloadState.Complete) =>
        new() { Hash = "abc", Name = "Heat.1995.1080p", State = state, Seeded = seeded };

    [Fact]
    public void A_week_of_seeding_is_due() =>
        Assert.True(Rule().IsDue(Seeded(TimeSpan.FromDays(7))));

    [Fact]
    public void A_moment_short_of_the_window_is_not_due() =>
        Assert.False(Rule().IsDue(Seeded(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1))));

    [Fact]
    public void Nothing_is_due_while_still_downloading() =>
        Assert.False(Rule().IsDue(Seeded(TimeSpan.FromDays(30), DownloadState.Downloading)));

    [Fact]
    public void The_floor_holds_even_when_the_window_is_under_it()
    {
        // The options refuse this combination at startup; the rule refuses it again on its own,
        // so a process that skipped validation still removes nothing the tracker would penalise.
        var rule = Rule(seedDays: 1);

        Assert.False(rule.IsDue(Seeded(TimeSpan.FromHours(30))));
        Assert.True(rule.IsDue(Seeded(TimeSpan.FromHours(60))));
    }

    [Fact]
    public void A_window_under_the_floor_is_incoherent() =>
        Assert.False(new RetentionOptions { SeedDays = 2, TrackerMinimumHours = 48, MarginHours = 12 }.IsCoherent);

    [Fact]
    public void The_defaults_are_coherent() =>
        Assert.True(new RetentionOptions().IsCoherent);

    [Fact]
    public void Remaining_counts_down_to_the_window() =>
        Assert.Equal(TimeSpan.FromDays(5), Rule().Remaining(Seeded(TimeSpan.FromDays(2))));

    [Fact]
    public void Remaining_never_goes_negative() =>
        Assert.Equal(TimeSpan.Zero, Rule().Remaining(Seeded(TimeSpan.FromDays(9))));

    [Theory]
    [InlineData(5.0, "5 days")]
    [InlineData(4.2, "5 days")]
    [InlineData(1.0, "1 day")]
    [InlineData(0.5, "12 hours")]
    [InlineData(0.06, "2 hours")]
    [InlineData(0.01, "less than an hour")]
    public void Remaining_is_described_in_whole_units(double days, string expected) =>
        Assert.Equal(expected, Retention.Describe(TimeSpan.FromDays(days)));
}
