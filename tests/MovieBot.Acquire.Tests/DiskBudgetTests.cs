using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public class DiskBudgetTests
{
    private const long GiB = 1L << 30;

    private static readonly DiskBudget Budget = new(
        Options.Create(new DownloadOptions()), NullLogger<DiskBudget>.Instance);

    private static BudgetReading Reading(double usedGiB, double freeGiB, double reservedGiB = 30) =>
        new((long)(usedGiB * GiB), 300 * GiB, 250 * GiB, (long)(freeGiB * GiB), (long)(reservedGiB * GiB));

    [Fact]
    public void Refuses_a_download_that_fits_the_volume_only_by_eating_the_reserve()
    {
        // The shape that stopped a transcode partway: 19 GiB free, a 12 GiB source accepted
        // into it, and the transcode beside it left to fill the rest.
        var verdict = Budget.Judge(Reading(usedGiB: 181, freeGiB: 19), 12 * GiB);

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void Accepts_a_download_that_leaves_the_reserve_free()
    {
        var verdict = Budget.Judge(Reading(usedGiB: 100, freeGiB: 60), 12 * GiB);

        Assert.True(verdict.Allowed);
    }

    [Fact]
    public void Reports_a_volume_with_nothing_past_the_reserve_as_full_under_a_ceiling_above_it()
    {
        // A 300 GiB ceiling on a 220 GiB volume is never reached by the directory alone.
        var reading = Reading(usedGiB: 190, freeGiB: 25);

        Assert.Equal(BudgetState.Full, reading.State);
        Assert.Contains("disk is full", Budget.Judge(reading, 1 * GiB).Reason);
    }

    [Fact]
    public void Warns_when_the_volume_is_closer_to_its_limit_than_the_warning_margin()
    {
        // 40 GiB past the reserve is under the 50 GiB between the warning and the ceiling.
        var reading = Reading(usedGiB: 150, freeGiB: 70);

        Assert.Equal(BudgetState.Warning, reading.State);
    }

    [Fact]
    public void Is_fine_with_room_on_both_limits()
    {
        Assert.Equal(BudgetState.Ok, Reading(usedGiB: 50, freeGiB: 400).State);
    }

    [Fact]
    public void A_zero_reserve_leaves_the_whole_volume_to_the_downloads()
    {
        var verdict = Budget.Judge(Reading(usedGiB: 181, freeGiB: 19, reservedGiB: 0), 12 * GiB);

        Assert.True(verdict.Allowed);
    }
}
