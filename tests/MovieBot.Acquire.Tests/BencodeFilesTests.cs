using System.Text;
using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>The files a .torrent holds, read before it is handed to the client.</summary>
public class BencodeFilesTests
{
    private static byte[] Torrent(string info) =>
        Encoding.UTF8.GetBytes($"d8:announce19:https://tracker.inv4:info{info}e");

    private static string Str(string s) => $"{Encoding.UTF8.GetByteCount(s)}:{s}";

    [Fact]
    public void A_single_file_torrent_is_its_own_name()
    {
        var files = Bencode.Files(Torrent($"d6:lengthi1234e{Str("name")}{Str("Heat.1995.mkv")}12:piece lengthi16384e6:pieces0:e"));

        var file = Assert.Single(files);
        Assert.Equal("Heat.1995.mkv", file.Path);
        Assert.Equal(1234, file.Size);
    }

    [Fact]
    public void A_pack_lists_its_files_under_its_name_and_skips_padding()
    {
        var files = Bencode.Files(Torrent(
            $"d5:filesl"
            + $"d6:lengthi100e4:pathl{Str("Show.S01E01.mkv")}ee"
            + $"d4:attr1:p6:lengthi7e4:pathl{Str(".pad")}{Str("7")}ee"
            + $"d6:lengthi200e4:pathl{Str("Subs")}{Str("Show.S01E02.mkv")}ee"
            + $"e{Str("name")}{Str("Show.S01")}12:piece lengthi16384e6:pieces0:e"));

        Assert.Equal(["Show.S01/Show.S01E01.mkv", "Show.S01/Subs/Show.S01E02.mkv"], files.Select(f => f.Path));
        Assert.Equal([100L, 200L], files.Select(f => f.Size));
    }

    [Fact]
    public void Reading_the_files_leaves_the_info_hash_as_it_was()
    {
        var torrent = Torrent($"d6:lengthi1234e{Str("name")}{Str("Heat.1995.mkv")}12:piece lengthi16384e6:pieces0:e");
        var before = Bencode.InfoHash(torrent);

        _ = Bencode.Files(torrent);

        Assert.Equal(before, Bencode.InfoHash(torrent));
    }
}
