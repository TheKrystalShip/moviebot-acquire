using System.Security.Cryptography;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>
/// Just enough bencode to find a torrent's info hash.
///
/// The torrent client's add endpoint answers "Ok." and nothing else — it does not say what it
/// added. Without the hash there is no way to ask after a particular download, and diffing the
/// torrent list before and after would attribute the wrong one whenever two are started close
/// together. The hash is computed from the file instead, which is exact and needs nobody's
/// cooperation.
///
/// The hash is the SHA-1 of the info dictionary's raw bytes, so the dictionary is located rather
/// than parsed into objects: re-encoding it would have to reproduce the original byte for byte,
/// and any disagreement produces a hash that matches nothing.
/// </summary>
public static class Bencode
{
    /// <summary>
    /// The v1 info hash of a .torrent file, lowercase hex, as the client's API spells it.
    /// </summary>
    /// <exception cref="FormatException">The file is not a bencoded dictionary with an info key.</exception>
    public static string InfoHash(ReadOnlySpan<byte> torrent)
    {
        var (start, length) = LocateInfoValue(torrent);
        return Convert.ToHexStringLower(SHA1.HashData(torrent.Slice(start, length)));
    }

    private static (int Start, int Length) LocateInfoValue(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data[0] != (byte)'d')
            throw new FormatException("The torrent file is not a bencoded dictionary.");

        var position = 1;

        // Only the top level is walked. A nested "info" key belongs to something else.
        while (position < data.Length && data[position] != (byte)'e')
        {
            var keyStart = position;
            position = SkipValue(data, position);
            var key = ExtractString(data, keyStart);

            var valueStart = position;
            position = SkipValue(data, position);

            if (key == "info")
                return (valueStart, position - valueStart);
        }

        throw new FormatException("The torrent file carries no info dictionary.");
    }

    /// <summary>Reads a byte string's contents, for comparing a key.</summary>
    private static string ExtractString(ReadOnlySpan<byte> data, int position)
    {
        var colon = data[position..].IndexOf((byte)':');
        if (colon < 0) throw new FormatException("A bencoded string has no length separator.");

        var length = ParseLength(data.Slice(position, colon));
        var start = position + colon + 1;

        return System.Text.Encoding.UTF8.GetString(data.Slice(start, length));
    }

    /// <summary>Returns the position just past the value starting at <paramref name="position"/>.</summary>
    private static int SkipValue(ReadOnlySpan<byte> data, int position)
    {
        if (position >= data.Length) throw new FormatException("The torrent file ends mid-value.");

        switch ((char)data[position])
        {
            case 'd':
            case 'l':
                position++;
                while (position < data.Length && data[position] != (byte)'e')
                    position = SkipValue(data, position);

                if (position >= data.Length) throw new FormatException("An unterminated container.");
                return position + 1;

            case 'i':
                var end = data[position..].IndexOf((byte)'e');
                if (end < 0) throw new FormatException("An unterminated integer.");
                return position + end + 1;

            default:
                var colon = data[position..].IndexOf((byte)':');
                if (colon < 0) throw new FormatException("A bencoded string has no length separator.");

                var length = ParseLength(data.Slice(position, colon));
                return position + colon + 1 + length;
        }
    }

    private static int ParseLength(ReadOnlySpan<byte> digits)
    {
        var length = 0;
        foreach (var digit in digits)
        {
            if (digit is < (byte)'0' or > (byte)'9')
                throw new FormatException("A bencoded string length is not a number.");

            length = length * 10 + (digit - '0');
        }

        return length;
    }
}
