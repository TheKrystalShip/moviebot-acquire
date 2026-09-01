namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>What the encode is, read out of the release name.</summary>
public enum Resolution { Unknown = 0, Sd = 1, Hd720 = 2, Hd1080 = 3, Uhd2160 = 4 }

/// <summary>Where the encode came from, which is what decides how good it can be.</summary>
public enum Source { Unknown = 0, Cam = 1, Dvd = 2, Web = 3, BluRay = 4, Remux = 5 }

/// <summary>The high dynamic range format, which the ingest has to tone-map away.</summary>
public enum DynamicRange { Sdr = 0, Hdr10 = 1, DolbyVision = 2 }

/// <summary>
/// One candidate as the rest of the application sees it: the tracker's row, parsed, with the
/// download link dropped.
///
/// Nothing here is a credential, so a <see cref="Release"/> is safe to log, to put in an embed
/// and to hand to a select menu. Downloading one takes <see cref="TorrentId"/> back to the
/// client, which is the only thing that knows the passkey.
/// </summary>
public sealed record Release
{
    public required long TorrentId { get; init; }

    /// <summary>The release name, kept whole: it is the only full description of the encode.</summary>
    public required string ReleaseName { get; init; }

    /// <summary>The film's title, as far as the release name reveals it.</summary>
    public required string Title { get; init; }

    public int? Year { get; init; }

    public string? ImdbId { get; init; }

    public Resolution Resolution { get; init; }

    public Source Source { get; init; }

    public DynamicRange DynamicRange { get; init; }

    /// <summary>The release group, after the trailing dash.</summary>
    public string? Group { get; init; }

    /// <summary>
    /// Whether the release is a stereoscopic encode. It is carried rather than dropped at the
    /// parse because the reason it is unusable is worth showing.
    /// </summary>
    public bool IsThreeDimensional { get; init; }

    public long SizeBytes { get; init; }

    public int Seeders { get; init; }

    public int Leechers { get; init; }

    /// <summary>Whether downloading this counts against the account's ratio.</summary>
    public bool IsFreeleech { get; init; }

    /// <summary>Whether the tracker's own group encoded it, which correlates with it being sane.</summary>
    public bool IsInternal { get; init; }

    /// <summary>The tracker's category name, kept for display and for filtering.</summary>
    public string Category { get; init; } = "";

    /// <summary>How many files the torrent holds.</summary>
    public int FileCount { get; init; }

    /// <summary>The genre line the tracker carries, when it has one.</summary>
    public string? Genres { get; init; }

    public DateTimeOffset? UploadedAt { get; init; }

    /// <summary>The size as a person reads it.</summary>
    public string SizeDisplay => SizeBytes switch
    {
        >= 1L << 30 => $"{SizeBytes / (double)(1L << 30):0.#} GiB",
        >= 1L << 20 => $"{SizeBytes / (double)(1L << 20):0.#} MiB",
        _ => $"{SizeBytes} B",
    };

    /// <summary>A one-line description for a menu row, which has very little room.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (Resolution != Resolution.Unknown)
                parts.Add(Resolution switch
                {
                    Resolution.Uhd2160 => "2160p",
                    Resolution.Hd1080 => "1080p",
                    Resolution.Hd720 => "720p",
                    _ => "SD",
                });

            if (Source != Source.Unknown)
                parts.Add(Source switch
                {
                    Source.Remux => "Remux",
                    Source.BluRay => "Blu-ray",
                    Source.Web => "WEB",
                    Source.Dvd => "DVD",
                    _ => "Cam",
                });

            if (DynamicRange == DynamicRange.DolbyVision) parts.Add("DoVi");
            else if (DynamicRange == DynamicRange.Hdr10) parts.Add("HDR");

            parts.Add(SizeDisplay);
            parts.Add($"{Seeders} seed");

            if (IsFreeleech) parts.Add("freeleech");

            return string.Join(" · ", parts);
        }
    }
}
