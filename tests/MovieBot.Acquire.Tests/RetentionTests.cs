using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// A download goes once it has seeded past the tracker's minimum plus the margin, or reached the
/// tracker's ratio plus its margin after the least seeding, and never before either. A wrong answer
/// here is either disk held for nothing or a penalty on the account, so the edges are what is
/// checked.
/// </summary>
public class RetentionTests
{
    private const long GiB = 1L << 30;

    private static Retention Rule(RetentionOptions? options = null) => new(Options.Create(options ?? new RetentionOptions()));

    private static DownloadStatus Download(
        TimeSpan seeded, long uploaded = 0, long downloaded = 10 * GiB, DownloadState state = DownloadState.Complete) =>
        new()
        {
            Hash = "abc", Name = "Heat.1995.1080p", State = state, Seeded = seeded,
            UploadedBytes = uploaded, DownloadedBytes = downloaded,
        };

    [Fact]
    public void The_minimum_plus_the_margin_is_due() =>
        Assert.True(Rule().IsDue(Download(TimeSpan.FromHours(60))));

    [Fact]
    public void A_moment_short_of_the_floor_is_not_due() =>
        Assert.False(Rule().IsDue(Download(TimeSpan.FromHours(60) - TimeSpan.FromSeconds(1))));

    [Fact]
    public void The_tracker_minimum_alone_is_not_enough() =>
        Assert.False(Rule().IsDue(Download(TimeSpan.FromHours(48))));

    [Fact]
    public void Nothing_is_due_while_still_downloading() =>
        Assert.False(Rule().IsDue(Download(TimeSpan.FromDays(30), state: DownloadState.Downloading)));

    [Fact]
    public void The_ratio_plus_its_margin_is_due_after_the_least_seeding() =>
        Assert.True(Rule().IsDue(Download(TimeSpan.FromHours(3), uploaded: 11 * GiB)));

    [Fact]
    public void The_tracker_ratio_alone_is_not_enough() =>
        Assert.False(Rule().IsDue(Download(TimeSpan.FromHours(10), uploaded: 10 * GiB)));

    [Fact]
    public void The_ratio_does_not_count_before_the_least_seeding() =>
        Assert.False(Rule().IsDue(Download(TimeSpan.FromHours(3) - TimeSpan.FromSeconds(1), uploaded: 30 * GiB)));

    [Fact]
    public void Nothing_downloaded_has_no_ratio_and_goes_on_time()
    {
        // Data already on disk is checked rather than downloaded; the client's own ratio for it
        // divides by the size, and the tracker, which saw no download, never reckons that one.
        var rule = Rule();

        Assert.Null(Download(TimeSpan.Zero, uploaded: 30 * GiB, downloaded: 0).Ratio);
        Assert.False(rule.IsDue(Download(TimeSpan.FromHours(10), uploaded: 30 * GiB, downloaded: 0)));
        Assert.True(rule.IsDue(Download(TimeSpan.FromHours(60), uploaded: 0, downloaded: 0)));
    }

    [Fact]
    public void The_defaults_are_coherent() =>
        Assert.True(new RetentionOptions().IsCoherent);

    [Fact]
    public void A_ratio_of_zero_is_incoherent() =>
        Assert.False(new RetentionOptions { TrackerRatio = 0 }.IsCoherent);

    [Fact]
    public void A_negative_margin_is_incoherent() =>
        Assert.False(new RetentionOptions { MarginHours = -1 }.IsCoherent);
}
