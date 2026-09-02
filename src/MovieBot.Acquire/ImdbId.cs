using System.Text.RegularExpressions;

namespace TheKrystalShip.MovieBot.Acquire;

/// <summary>
/// The same identifier written two ways. Trackers and databases write <c>tt0458352</c>; the
/// subtitle index writes <c>458352</c>. Converting in one place keeps a zero-padding mistake from
/// turning into a lookup that silently finds nothing.
/// </summary>
public static partial class ImdbId
{
    /// <summary>The bare number, or null when the text is not an IMDb id at all.</summary>
    public static long? ToNumber(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var digits = id.Trim();
        if (digits.StartsWith("tt", StringComparison.OrdinalIgnoreCase)) digits = digits[2..];

        return long.TryParse(digits, out var value) && value > 0 ? value : null;
    }

    /// <summary>
    /// The canonical <c>tt</c> form, padded to seven digits. Older ids are shorter than that and
    /// an unpadded one does not match anywhere it is used as a key.
    /// </summary>
    public static string? ToTag(long? number) =>
        number is > 0 ? $"tt{number.Value:D7}" : null;

    /// <summary>Whether the text is a usable IMDb id in either spelling.</summary>
    public static bool IsValid(string? id) => ToNumber(id) is not null;

    /// <summary>
    /// The id inside whatever somebody pasted, in canonical form, or null when there is none.
    ///
    /// People arrive holding a link more often than an id: the film's page, the mobile site, a
    /// share link with tracking on the end. All of them carry the <c>tt</c> form somewhere in the
    /// path, and that form is distinctive enough to be taken from anywhere in the text. A bare
    /// number is not taken, because a bare number typed into a film search is a title.
    /// </summary>
    public static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Tag().Match(text);
        return match.Success ? ToTag(ToNumber(match.Value)) : null;
    }

    [GeneratedRegex(@"\btt\d{5,10}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Tag();
}
