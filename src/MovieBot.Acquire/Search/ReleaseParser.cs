using System.Globalization;
using System.Text.RegularExpressions;
using TheKrystalShip.MovieBot.Acquire.Tracker;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// Reads a scene release name into the few facts that decide whether a release is worth taking.
///
/// The release name is the only description of an encode that exists — the tracker carries no
/// structured resolution, source or codec — so everything the ranker weighs is parsed from here.
/// </summary>
public static partial class ReleaseParser
{
    // The first token that describes the encode rather than the film. The title and the year sit
    // to the left of the earliest of these, which is what stops a title's own number being read
    // as the year: "Blade.Runner.2049.2017.2160p" has two plausible years and only one of them
    // is to the left of a quality marker as the last such token.
    [GeneratedRegex(
        @"\b(?:2160p|1080p|720p|576p|480p|4K|UHD|REMUX|BluRay|Blu-ray|BDRip|BRRip|BD25|BD50|"
        + @"WEB-DL|WEBRip|WEB|HDTV|DVDRip|DVD9|DVD5|DVD|PAL|NTSC|CAM|TELESYNC|"
        + @"x264|x265|H\.?264|H\.?265|AVC|HEVC|XviD|DivX|"
        + @"DTS|DD\+?|DDP|AC3|AAC|TrueHD|Atmos|FLAC|"
        + @"HDR10|HDR|DoVi|SDR|3D|H-SBS|SBS|"
        + @"PROPER|REPACK|RETAiL|EXTENDED|UNRATED|IMAX|LIMITED|COMPLETE|OST)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityMarker();

    [GeneratedRegex(@"\b(19\d{2}|20\d{2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex YearToken();

    [GeneratedRegex(@"-([^\s.\-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingGroup();

    /// <summary>Parses one tracker row. The download link on the row is deliberately not read.</summary>
    public static Release Parse(TrackerTorrent row)
    {
        var name = row.Name;
        var (title, year) = SplitTitleAndYear(name);

        return new Release
        {
            TorrentId = row.Id,
            ReleaseName = name,
            Title = title,
            Year = year,
            ImdbId = string.IsNullOrWhiteSpace(row.Imdb) ? null : row.Imdb,
            Resolution = ReadResolution(name),
            Source = ReadSource(name),
            DynamicRange = ReadDynamicRange(name),
            Group = TrailingGroup().Match(name) is { Success: true } g ? g.Groups[1].Value : null,
            IsThreeDimensional = IsThreeD(name),
            SizeBytes = row.Size,
            Seeders = row.Seeders,
            Leechers = row.Leechers,
            IsFreeleech = row.Freeleech == 1,
            IsInternal = row.Internal == 1,
            Category = row.Category,
            FileCount = row.Files,
            Genres = string.IsNullOrWhiteSpace(row.SmallDescription) ? null : row.SmallDescription,
            UploadedAt = ReadUploadDate(row.UploadDate),
        };
    }

    private static (string Title, int? Year) SplitTitleAndYear(string name)
    {
        var marker = QualityMarker().Match(name);
        var limit = marker.Success ? marker.Index : name.Length;

        // The last year to the left of the first quality marker. A film whose title is itself a
        // year keeps its title because the release year is always the later of the two tokens.
        Match? chosen = null;
        foreach (Match candidate in YearToken().Matches(name))
        {
            if (candidate.Index >= limit) break;
            chosen = candidate;
        }

        if (chosen is null)
            return (Clean(name[..limit]), null);

        var year = int.Parse(chosen.Groups[1].Value, CultureInfo.InvariantCulture);
        return (Clean(name[..chosen.Index]), year);
    }

    /// <summary>Turns a dotted release fragment back into something a person reads.</summary>
    private static string Clean(string fragment) =>
        string.Join(' ', fragment
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static Resolution ReadResolution(string name)
    {
        // 2160p is checked as itself rather than through UHD: "1080p.UHD.BluRay" is a 1080p
        // encode of a UHD disc, and reading UHD as a resolution would promote it over the
        // genuine 2160p releases beside it.
        if (Contains(name, "2160p")) return Resolution.Uhd2160;
        if (Contains(name, "1080p")) return Resolution.Hd1080;
        if (Contains(name, "720p")) return Resolution.Hd720;
        if (Contains(name, "576p") || Contains(name, "480p")) return Resolution.Sd;
        if (Contains(name, "DVD9") || Contains(name, "DVD5") || Contains(name, "DVDRip"))
            return Resolution.Sd;
        if (Contains(name, "BDRip") || Contains(name, "BRRip")) return Resolution.Sd;
        return Resolution.Unknown;
    }

    private static Source ReadSource(string name)
    {
        if (Contains(name, "REMUX")) return Source.Remux;
        if (Contains(name, "BluRay") || Contains(name, "Blu-ray")
            || Contains(name, "BDRip") || Contains(name, "BRRip")) return Source.BluRay;
        if (Contains(name, "WEB-DL") || Contains(name, "WEBRip") || Contains(name, "WEB"))
            return Source.Web;
        if (Contains(name, "HDTV")) return Source.Web;
        if (Contains(name, "DVDRip") || Contains(name, "DVD9") || Contains(name, "DVD5")
            || Contains(name, "DVD") || Contains(name, "PAL") || Contains(name, "NTSC"))
            return Source.Dvd;
        if (Contains(name, "CAM") || Contains(name, "TELESYNC")) return Source.Cam;
        return Source.Unknown;
    }

    private static DynamicRange ReadDynamicRange(string name)
    {
        if (Contains(name, "DoVi") || Contains(name, "DolbyVision")) return DynamicRange.DolbyVision;
        if (Contains(name, "HDR")) return DynamicRange.Hdr10;
        return DynamicRange.Sdr;
    }

    private static bool IsThreeD(string name) =>
        Contains(name, "3D") || Contains(name, "H-SBS") || Contains(name, "HSBS")
        || Contains(name, "SBS");

    private static DateTimeOffset? ReadUploadDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? new DateTimeOffset(parsed.ToUniversalTime(), TimeSpan.Zero)
            : null;

    /// <summary>
    /// A whole-token search. A plain substring test reads "3D" out of "H.264.3Della" and reads
    /// "WEB" out of a group name, both of which change how a release ranks.
    /// </summary>
    private static bool Contains(string haystack, string token) =>
        Regex.IsMatch(haystack, $@"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
