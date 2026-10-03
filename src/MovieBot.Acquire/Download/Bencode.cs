using System.Security.Cryptography;

namespace TheKrystalShip.MovieBot.Acquire.Download;

/// <summary>One file in a torrent: its path as the client lays it out, and its size.</summary>
public sealed record TorrentFileEntry(string Path, long Size);

/// <summary>
/// Just enough bencode to find a torrent's info hash and the files it holds.
///
/// The torrent client's add endpoint answers "Ok." and nothing else — it does not say what it
/// added. Without the hash there is no way to ask after a particular download, and diffing the
/// torrent list before and after would attribute the wrong one whenever two are started close
/// together. The hash is computed from the file instead, which is exact and needs nobody's
/// cooperation.
///
/// The hash is the SHA-1 of the info dictionary's raw bytes, so the dictionary is located rather
/// than parsed into objects: re-encoding it would have to reproduce the original byte for byte,
/// and any disagreement produces a hash that matches nothing. The file list is read by decoding
/// the same dictionary into plain values, which is only ever read and never written back.
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

    /// <summary>
    /// The files a .torrent holds, in its own order, each with its path inside the torrent (the
    /// torrent's name first, as the client lays it out on disk) and its size. Padding files, which
    /// some creators insert to align pieces and no client shows, are left out.
    /// </summary>
    /// <exception cref="FormatException">The file is not a torrent this can read.</exception>
    public static IReadOnlyList<TorrentFileEntry> Files(ReadOnlySpan<byte> torrent)
    {
        var (start, length) = LocateInfoValue(torrent);
        var info = torrent.Slice(start, length);
        var position = 0;
        if (Decode(info, ref position) is not Dictionary<string, object> dictionary)
            throw new FormatException("The torrent's info is not a dictionary.");

        var name = dictionary.TryGetValue("name.utf-8", out var utf8) && utf8 is byte[] u ? Text(u)
            : dictionary.TryGetValue("name", out var plain) && plain is byte[] p ? Text(p)
            : throw new FormatException("The torrent has no name.");

        if (dictionary.TryGetValue("length", out var single) && single is long size)
            return [new TorrentFileEntry(name, size)];

        if (!dictionary.TryGetValue("files", out var listed) || listed is not List<object> files)
            throw new FormatException("The torrent lists no files.");

        var entries = new List<TorrentFileEntry>();
        foreach (var file in files.OfType<Dictionary<string, object>>())
        {
            if (file.TryGetValue("attr", out var attr) && attr is byte[] a && Text(a).Contains('p'))
                continue;

            var segments = (file.TryGetValue("path.utf-8", out var pu) ? pu : file.GetValueOrDefault("path"))
                as List<object> ?? [];
            var path = string.Join('/', segments.OfType<byte[]>().Select(Text));
            if (file.GetValueOrDefault("length") is long bytes && path.Length > 0)
                entries.Add(new TorrentFileEntry($"{name}/{path}", bytes));
        }

        return entries;
    }

    private static string Text(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// One value, as plain objects: a byte string as <c>byte[]</c>, an integer as <c>long</c>, a
    /// list, or a dictionary keyed by its keys' text. Used for reading, never for re-encoding.
    /// </summary>
    private static object Decode(ReadOnlySpan<byte> data, ref int position)
    {
        if (position >= data.Length) throw new FormatException("The torrent file ends mid-value.");

        switch ((char)data[position])
        {
            case 'd':
                position++;
                var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
                while (position < data.Length && data[position] != (byte)'e')
                {
                    if (Decode(data, ref position) is not byte[] key)
                        throw new FormatException("A dictionary key is not a string.");
                    dictionary[Text(key)] = Decode(data, ref position);
                }
                if (position >= data.Length) throw new FormatException("An unterminated container.");
                position++;
                return dictionary;

            case 'l':
                position++;
                var list = new List<object>();
                while (position < data.Length && data[position] != (byte)'e')
                    list.Add(Decode(data, ref position));
                if (position >= data.Length) throw new FormatException("An unterminated container.");
                position++;
                return list;

            case 'i':
                var end = data[position..].IndexOf((byte)'e');
                if (end < 0) throw new FormatException("An unterminated integer.");
                var digits = System.Text.Encoding.ASCII.GetString(data.Slice(position + 1, end - 1));
                position += end + 1;
                return long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);

            default:
                var colon = data[position..].IndexOf((byte)':');
                if (colon < 0) throw new FormatException("A bencoded string has no length separator.");
                var length = ParseLength(data.Slice(position, colon));
                var value = data.Slice(position + colon + 1, length).ToArray();
                position += colon + 1 + length;
                return value;
        }
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
