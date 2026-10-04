using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The claims running transcodes hold on a volume, and the arithmetic that turns them into the
/// room a transcode is placed against.
/// </summary>
public sealed class TranscodeLedgerTests : IDisposable
{
    private const long GiB = 1L << 30;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "moviebot-ledger-tests", Guid.NewGuid().ToString("n"));

    private string Downloads => Path.Combine(_root, "downloads");
    private string Media => Path.Combine(_root, "media");

    public TranscodeLedgerTests()
    {
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Media);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private IOptions<DownloadOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new DownloadOptions { Root = Downloads });

    private TranscodeLedger Ledger(string? mediaRoot = null) => new(
        Microsoft.Extensions.Options.Options.Create(new TranscodeLedgerOptions { MediaRoot = mediaRoot ?? Media }),
        NullLogger<TranscodeLedger>.Instance);

    [Fact]
    public void The_ledger_is_a_dotted_directory_under_the_media_root_and_is_made_on_the_first_claim()
    {
        var fresh = Path.Combine(_root, "fresh", "media");
        var ledger = Ledger(fresh);

        Assert.Empty(ledger.List());
        Assert.False(Directory.Exists(fresh));

        Assert.True(ledger.Reserve(Claim("a", GiB)));

        Assert.True(Directory.Exists(Path.Combine(fresh, TranscodeLedger.FolderName)));
        Assert.StartsWith(".", TranscodeLedger.FolderName);
    }

    [Fact]
    public void A_ledger_with_no_media_root_holds_no_claims_and_blocks_nothing()
    {
        var ledger = Ledger(mediaRoot: "");

        Assert.False(ledger.Reserve(Claim("a", GiB)));
        Assert.Empty(ledger.List());
        Assert.False(ledger.IsWritingUnder(Media));
    }

    [Fact]
    public void A_claim_that_cannot_be_written_is_reported_and_does_not_throw()
    {
        // A file where the ledger's directory should be.
        File.WriteAllText(Path.Combine(Media, TranscodeLedger.FolderName), "");

        var ledger = Ledger();

        Assert.False(ledger.Reserve(Claim("a", GiB)));
        Assert.Empty(ledger.List());
    }

    private TranscodeReservation Claim(string key, long bytes, string? root = null) => new()
    {
        Key = key,
        Root = root ?? Media,
        TitleDirectory = Path.Combine(root ?? Media, key),
        Bytes = bytes
    };

    [Fact]
    public void A_claim_is_listed_until_it_is_released()
    {
        var ledger = Ledger();

        ledger.Reserve(Claim("a", 6 * GiB));
        ledger.Reserve(Claim("b/episode.mkv", 2 * GiB));

        Assert.Equal(["a", "b/episode.mkv"], ledger.List().Select(r => r.Key).Order().ToArray());

        ledger.Release("a");

        Assert.Equal(["b/episode.mkv"], ledger.List().Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Reserving_again_replaces_the_claim_rather_than_adding_one()
    {
        var ledger = Ledger();

        ledger.Reserve(Claim("a", 6 * GiB));
        ledger.Reserve(Claim("a", 9 * GiB));

        var only = Assert.Single(ledger.List());
        Assert.Equal(9 * GiB, only.Bytes);
    }

    [Fact]
    public void A_second_ledger_reads_the_claims_another_process_wrote()
    {
        Ledger().Reserve(Claim("a", 6 * GiB));

        Assert.Single(Ledger().List());
    }

    [Fact]
    public void Clearing_removes_every_claim()
    {
        var ledger = Ledger();
        ledger.Reserve(Claim("a", 6 * GiB));
        ledger.Reserve(Claim("b", 6 * GiB));

        ledger.Clear();

        Assert.Empty(ledger.List());
    }

    [Fact]
    public void Writing_under_a_root_is_whether_any_claim_names_it()
    {
        var ledger = Ledger();
        var cold = Path.Combine(_root, "cold");

        ledger.Reserve(Claim("a", 6 * GiB, root: cold));

        Assert.True(ledger.IsWritingUnder(cold));
        Assert.False(ledger.IsWritingUnder(Media));
    }

    [Fact]
    public void A_title_is_being_made_while_a_claim_names_its_directory()
    {
        var ledger = Ledger();
        ledger.Reserve(Claim("hash-of-heat", 6 * GiB, root: Path.Combine(_root, "cold")) with
        {
            TitleDirectory = Path.Combine(_root, "cold", "heat-1995")
        });

        Assert.True(ledger.IsMaking("heat-1995"));
        Assert.False(ledger.IsMaking("collateral-2004"));
    }

    [Fact]
    public void What_a_claim_still_holds_is_its_size_less_what_is_already_on_disk()
    {
        Assert.Equal(4 * GiB, TranscodeReservation.Outstanding(6 * GiB, 2 * GiB));
        Assert.Equal(0, TranscodeReservation.Outstanding(6 * GiB, 7 * GiB));
    }

    [Fact]
    public void Outstanding_is_measured_from_the_title_directory()
    {
        var claim = Claim("film", 1000);
        Directory.CreateDirectory(Path.Combine(claim.TitleDirectory, "v0"));
        File.WriteAllBytes(Path.Combine(claim.TitleDirectory, "v0", "0.m4s"), new byte[400]);

        Assert.Equal(600, claim.OutstandingBytes());
        Assert.Equal(1000, Claim("not-started", 1000).OutstandingBytes());
    }

    [Fact]
    public void A_volumes_room_is_its_free_space_less_every_claim_on_it()
    {
        var room = new VolumeRoom(50 * GiB, TranscodeOutstandingBytes: 10 * GiB, PendingDownloadBytes: 15 * GiB);

        Assert.Equal(25 * GiB, room.AvailableBytes);
        Assert.True(room.Fits(20 * GiB));
        Assert.False(room.Fits(25 * GiB), "the margin is kept free past every claim");
        Assert.Equal(0, new VolumeRoom(5 * GiB, 10 * GiB, 0).AvailableBytes);
    }

    [Fact]
    public void A_mount_point_is_the_longest_one_that_is_a_parent_of_the_path()
    {
        string[] mounts = ["/", "/home", "/home/heisen/moviebot", "/homestead"];

        Assert.Equal("/home", Volumes.MountOf("/home/heisen/Downloads", mounts));
        Assert.Equal("/home/heisen/moviebot", Volumes.MountOf("/home/heisen/moviebot/media", mounts));
        Assert.Equal("/home/heisen/moviebot", Volumes.MountOf("/home/heisen/moviebot", mounts));
        Assert.Equal("/", Volumes.MountOf("/srv/films", mounts));
        Assert.Equal("/homestead", Volumes.MountOf("/homestead/x", mounts));
    }

    [Fact]
    public void A_volume_the_downloads_are_on_carries_their_pending_bytes_and_one_they_are_not_on_does_not()
    {
        var budget = new DiskBudget(Options(), Ledger(), NullLogger<DiskBudget>.Instance);

        // The temporary directories are on one volume, so the downloads share it.
        Assert.Equal(7 * GiB, budget.ReadVolume(Media, 7 * GiB).PendingDownloadBytes);
    }

    [Fact]
    public void A_claim_under_a_media_root_is_taken_from_that_volumes_room_and_the_downloads_headroom()
    {
        var ledger = Ledger();
        var budget = new DiskBudget(Options(), ledger, NullLogger<DiskBudget>.Instance);

        var before = budget.ReadVolume(Media, 0);
        ledger.Reserve(Claim("a", 6 * GiB));
        var after = budget.ReadVolume(Media, 0);

        Assert.Equal(6 * GiB, after.TranscodeOutstandingBytes);
        Assert.Equal(0, before.TranscodeOutstandingBytes);
        Assert.Equal(6 * GiB, budget.Read().TranscodeOutstandingBytes);
    }
}
