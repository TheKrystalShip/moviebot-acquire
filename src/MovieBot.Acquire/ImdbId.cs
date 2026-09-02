namespace TheKrystalShip.MovieBot.Acquire;

/// <summary>
/// The same identifier written two ways. Trackers and databases write <c>tt0458352</c>; the
/// subtitle index writes <c>458352</c>. Converting in one place keeps a zero-padding mistake from
/// turning into a lookup that silently finds nothing.
/// </summary>
public static class ImdbId
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
}
