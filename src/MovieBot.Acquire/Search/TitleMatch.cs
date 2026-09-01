using System.Globalization;
using System.Text;

namespace TheKrystalShip.MovieBot.Acquire.Search;

/// <summary>
/// Whether a release is the film that was asked for.
///
/// The tracker matches words against the whole release name rather than films, so a search
/// carries back anything sharing a token with it: a search for a 1995 film returns every other
/// 1995 film whose name happens to contain the title word. The year cannot catch those, because
/// the year is exactly what they have in common.
/// </summary>
public static class TitleMatch
{
    /// <summary>
    /// Articles are dropped before comparing. They are the one class of word people add or leave
    /// off without meaning a different film, and a release name is as likely to omit one.
    /// </summary>
    private static readonly HashSet<string> Articles =
        new(StringComparer.Ordinal) { "a", "an", "the" };

    /// <summary>
    /// Words that describe which version of a film a release carries rather than which film it
    /// is. They are stripped off the end of a title before the two are compared, so a release
    /// named for its cut still matches a plain title.
    ///
    /// The list is deliberately short and holds no word that could order or number a film:
    /// "part", "two" and "chapter" name a different film and stay.
    /// </summary>
    private static readonly HashSet<string> EditionWords =
        new(StringComparer.Ordinal)
        {
            "directors", "director", "cut", "theatrical", "extended", "unrated", "uncut",
            "remastered", "restored", "redux", "imax", "final", "special", "edition",
            "anniversary", "limited", "complete", "version", "open", "matte", "dubbed",
        };

    /// <summary>
    /// Whether the two name the same film.
    ///
    /// The test is equality once editions are stripped, not containment. Containment reads
    /// "Dead Heat" as "Heat" and "Dune Part Two" as "Dune", which is the same class of wrong
    /// result the check exists to remove.
    /// </summary>
    public static bool Matches(string queryTitle, string releaseTitle)
    {
        var wanted = StripEditions(Tokenize(queryTitle));
        if (wanted.Count == 0) return true;

        return wanted.SequenceEqual(StripEditions(Tokenize(releaseTitle)), StringComparer.Ordinal);
    }

    /// <summary>
    /// Reduces a title to comparable tokens: case, punctuation and accents all vary between what
    /// somebody types and what a release is named, and none of them distinguish a film.
    /// </summary>
    private static List<string> Tokenize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            // An apostrophe is removed rather than replaced, so "Ocean's" and "Oceans" are the
            // same word instead of differing by a stray token.
            if (character is '\'' or '’') continue;

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Articles.Contains(t))
            .ToList();
    }

    /// <summary>
    /// Removes edition words from the end of a title. Only from the end: a leading word is part
    /// of the name, which is what keeps "Final Destination" whole.
    /// </summary>
    private static List<string> StripEditions(List<string> tokens)
    {
        var end = tokens.Count;
        while (end > 1 && EditionWords.Contains(tokens[end - 1]))
            end--;

        return tokens.GetRange(0, end);
    }
}
