namespace TheKrystalShip.MovieBot.Acquire.Subtitles;

/// <summary>
/// One subtitle as the rest of the application sees it: the index's row, flattened, with nothing
/// in it that costs an allowance to look at. Downloading one takes <see cref="FileId"/> back to
/// the client.
/// </summary>
public sealed record SubtitleCandidate
{
    /// <summary>What a download is asked for by. Not the same number as the subtitle's own id.</summary>
    public required long FileId { get; init; }

    /// <summary>The release this was timed against, as the uploader wrote it. Often empty.</summary>
    public string Release { get; init; } = "";

    public string? FileName { get; init; }

    public string Language { get; init; } = "";

    /// <summary>Zero when the uploader declared none, which is common and is not a fault.</summary>
    public double Fps { get; init; }

    public int DownloadCount { get; init; }

    public bool HearingImpaired { get; init; }

    /// <summary>Subtitles for the foreign-language lines only, not for the whole film.</summary>
    public bool ForeignPartsOnly { get; init; }

    public bool FromTrusted { get; init; }

    public bool MachineTranslated { get; init; }

    public bool AiTranslated { get; init; }

    /// <summary>True only when the index was asked by hash and answered that this file matched.</summary>
    public bool HashMatch { get; init; }

    /// <summary>More than one means the subtitle is split for a release that came on two discs.</summary>
    public int CdCount { get; init; } = 1;

    public DateTimeOffset? UploadedAt { get; init; }

    public string? ImdbId { get; init; }

    public static SubtitleCandidate? From(OpenSubtitlesItem item)
    {
        var a = item.Attributes;
        var file = a?.Files.FirstOrDefault();

        // A row with no file cannot be downloaded, so it is not a candidate for anything. The test
        // is positive because the id is nullable, and a comparison against a missing one is false.
        if (a is null || file is null || file.FileId is not > 0) return null;

        return new SubtitleCandidate
        {
            FileId = file.FileId.Value,
            Release = a.Release ?? "",
            FileName = file.FileName,
            Language = a.Language ?? "",
            // An absent flag is the flag not set. The index sends null for one an uploader left
            // alone, and carrying that as a third state through everything that judges a subtitle
            // would say nothing more than false already says.
            Fps = a.Fps ?? 0,
            DownloadCount = a.DownloadCount ?? 0,
            HearingImpaired = a.HearingImpaired ?? false,
            ForeignPartsOnly = a.ForeignPartsOnly ?? false,
            FromTrusted = a.FromTrusted ?? false,
            MachineTranslated = a.MachineTranslated ?? false,
            AiTranslated = a.AiTranslated ?? false,
            HashMatch = a.MovieHashMatch ?? false,
            CdCount = a.CdCount is > 0 ? a.CdCount.Value : 1,
            UploadedAt = a.UploadDate,
            ImdbId = Acquire.ImdbId.ToTag(a.Feature?.ImdbId)
        };
    }
}

/// <summary>
/// What a subtitle is being judged against: the film as it actually sits on this disc. Kept as
/// its own shape so this library needs nothing from the player's manifest.
/// </summary>
public sealed record SubtitleTarget
{
    /// <summary>The release the film arrived as.</summary>
    public string? Release { get; init; }

    /// <summary>The film's frame rate. Zero when it is not known.</summary>
    public double FrameRate { get; init; }

    public string? MovieHash { get; init; }

    public string? ImdbId { get; init; }
}
