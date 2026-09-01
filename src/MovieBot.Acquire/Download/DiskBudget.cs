using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>How the download directory stands against its budget.</summary>
public enum BudgetState { Ok = 0, Warning = 1, Full = 2 }

/// <summary>
/// A measurement of the download directory, taken rather than tracked.
/// </summary>
/// <param name="UsedBytes">What the directory holds now.</param>
/// <param name="MaximumBytes">The ceiling from the options.</param>
/// <param name="WarningBytes">Where the budget starts being reported as running out.</param>
/// <param name="FreeOnVolumeBytes">
/// What the volume itself has left, which can be the smaller of the two limits and is the one
/// that produces a half-written file rather than a refusal.
/// </param>
public sealed record BudgetReading(
    long UsedBytes,
    long MaximumBytes,
    long WarningBytes,
    long FreeOnVolumeBytes)
{
    public long RemainingBytes => Math.Max(0, MaximumBytes - UsedBytes);

    /// <summary>What can actually be written: the budget or the volume, whichever runs out first.</summary>
    public long HeadroomBytes => Math.Min(RemainingBytes, FreeOnVolumeBytes);

    public BudgetState State => UsedBytes >= MaximumBytes ? BudgetState.Full
        : UsedBytes >= WarningBytes ? BudgetState.Warning
        : BudgetState.Ok;

    public double UsedGiB => UsedBytes / (double)(1L << 30);
    public double MaximumGiB => MaximumBytes / (double)(1L << 30);
    public double HeadroomGiB => HeadroomBytes / (double)(1L << 30);

    public string Summary =>
        $"{UsedGiB:0.#} of {MaximumGiB:0.#} GiB used, {HeadroomGiB:0.#} GiB free to write";
}

/// <summary>Whether a download may start, and what to say when it may not.</summary>
public sealed record BudgetVerdict(bool Allowed, BudgetReading Reading, string? Reason);

/// <summary>
/// Measures the download directory and decides whether one more film fits.
///
/// The size is measured off the filesystem on every call rather than accumulated as downloads
/// complete. A running total drifts the moment somebody deletes a film by hand, and it drifts
/// silently: the first sign would be a refused download with plenty of disk free, or a full
/// volume under a budget that says there is room.
/// </summary>
public sealed class DiskBudget(IOptions<DownloadOptions> options, ILogger<DiskBudget> logger)
{
    private readonly DownloadOptions _options = options.Value;

    /// <summary>Measures the directory now. Creates it when it does not yet exist.</summary>
    public BudgetReading Read()
    {
        var root = _options.ResolveRoot();
        Directory.CreateDirectory(root);

        long used = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    used += new FileInfo(file).Length;
                }
                catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
                {
                    // A file being written while the directory is walked is normal here.
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            used = 0;
        }

        var free = new DriveInfo(Path.GetPathRoot(root) ?? "/").AvailableFreeSpace;

        return new BudgetReading(
            used,
            (long)(_options.MaximumGiB * (1L << 30)),
            (long)(_options.WarningGiB * (1L << 30)),
            free);
    }

    /// <summary>
    /// Whether a release of this size may be started.
    ///
    /// The size is checked against the headroom before the torrent is added rather than after,
    /// because a torrent client stopped partway through leaves the part it wrote behind and it
    /// still counts against the budget.
    /// </summary>
    public BudgetVerdict CanAccept(long sizeBytes)
    {
        var reading = Read();

        if (reading.State == BudgetState.Full)
            return new BudgetVerdict(false, reading,
                $"The download directory is full: {reading.Summary}. Remove a film to make room.");

        if (sizeBytes > reading.HeadroomBytes)
        {
            var wanted = sizeBytes / (double)(1L << 30);
            return new BudgetVerdict(false, reading,
                $"That release needs {wanted:0.#} GiB and only {reading.HeadroomGiB:0.#} GiB is free. "
                + "Remove a film to make room.");
        }

        if (reading.State == BudgetState.Warning)
            logger.LogWarning("Download budget is running out: {Summary}.", reading.Summary);

        return new BudgetVerdict(true, reading, null);
    }
}
