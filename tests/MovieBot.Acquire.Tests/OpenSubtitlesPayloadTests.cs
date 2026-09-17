using System.Text.Json;
using TheKrystalShip.MovieBot.Acquire.Subtitles;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// What the index sends, rather than what it documents.
///
/// It writes <c>null</c> for flags an uploader left unset, and a search arrives as one document,
/// so a single unreadable row costs the whole response and leaves a film with no subtitles at all.
/// Every scalar is read as nullable for that reason, and these are the payloads that hold it to it.
/// </summary>
public class OpenSubtitlesPayloadTests
{
    private static OpenSubtitlesSearchResponse Read(string json) =>
        JsonSerializer.Deserialize(json, OpenSubtitlesJsonContext.Default.OpenSubtitlesSearchResponse)!;

    [Fact]
    public void A_row_whose_flags_are_null_is_read_rather_than_failing_the_search()
    {
        var response = Read(
            """
            {
              "total_pages": 1, "total_count": 1, "page": 1,
              "data": [{
                "id": "12345",
                "attributes": {
                  "language": "en",
                  "release": "Evil.Dead.Burn.2026.1080p.BluRay.x264",
                  "download_count": null,
                  "fps": null,
                  "hearing_impaired": null,
                  "foreign_parts_only": null,
                  "from_trusted": null,
                  "ai_translated": null,
                  "machine_translated": null,
                  "nb_cd": null,
                  "files": [{ "file_id": 987, "file_name": "evil-dead-burn.srt" }]
                }
              }]
            }
            """);

        var candidate = SubtitleCandidate.From(response.Data[0]);

        Assert.NotNull(candidate);
        Assert.Equal(987, candidate.FileId);
        Assert.False(candidate.FromTrusted);
        Assert.False(candidate.HearingImpaired);
        Assert.False(candidate.MachineTranslated);
        Assert.Equal(0, candidate.DownloadCount);
        Assert.Equal(0, candidate.Fps);
        // Split subtitles are the exception, so an undeclared count is one disc rather than none.
        Assert.Equal(1, candidate.CdCount);
    }

    /// <summary>
    /// One null among otherwise ordinary fields, in the middle of a page. The rows around it have
    /// to survive it.
    /// </summary>
    [Fact]
    public void One_bad_row_does_not_take_the_rows_beside_it()
    {
        var response = Read(
            """
            {
              "data": [
                { "id": "1", "attributes": { "language": "en", "from_trusted": true,
                  "files": [{ "file_id": 1, "file_name": "a.srt" }] } },
                { "id": "2", "attributes": { "language": "en", "from_trusted": null,
                  "files": [{ "file_id": 2, "file_name": "b.srt" }] } },
                { "id": "3", "attributes": { "language": "en", "from_trusted": false,
                  "files": [{ "file_id": 3, "file_name": "c.srt" }] } }
              ]
            }
            """);

        var candidates = response.Data.Select(SubtitleCandidate.From).OfType<SubtitleCandidate>().ToList();

        Assert.Equal(3, candidates.Count);
        Assert.True(candidates[0].FromTrusted);
        Assert.False(candidates[1].FromTrusted);
        Assert.False(candidates[2].FromTrusted);
    }

    [Fact]
    public void A_row_naming_no_file_to_fetch_is_not_a_candidate()
    {
        var response = Read(
            """
            {
              "data": [
                { "id": "1", "attributes": { "language": "en", "files": [] } },
                { "id": "2", "attributes": { "language": "en",
                  "files": [{ "file_id": null, "file_name": "no-id.srt" }] } },
                { "id": "3", "attributes": { "language": "en",
                  "files": [{ "file_id": 0, "file_name": "zero.srt" }] } }
              ]
            }
            """);

        Assert.All(response.Data, item => Assert.Null(SubtitleCandidate.From(item)));
    }

    [Fact]
    public void A_response_whose_counters_are_null_still_reads()
    {
        var response = Read("""{ "total_pages": null, "total_count": null, "page": null, "data": [] }""");

        Assert.Empty(response.Data);
    }
}
