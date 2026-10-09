namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>A person's whole body of work as the database lists it, before any title is named by its IMDb id.</summary>
public sealed record Filmography
{
    public required int TmdbId { get; init; }

    public required string Name { get; init; }

    /// <summary>The department the database knows them for: <c>Acting</c>, <c>Directing</c>, <c>Writing</c>…</summary>
    public string? KnownFor { get; init; }

    public string? ProfilePath { get; init; }

    public IReadOnlyList<FilmCredit> Credits { get; init; } = [];
}

/// <summary>
/// One title a person is credited on, with everything they did on it joined into <see cref="Role"/>.
/// A credit as oneself, or on a talk, news or reality show, is <see cref="Appearance"/>: promotion
/// rather than work, which a caller leaves out unless it asks for it.
/// </summary>
public sealed record FilmCredit
{
    public required int TmdbId { get; init; }

    public required string Kind { get; init; }

    public required string Title { get; init; }

    /// <summary>The day a film was released or a show first aired; null for an announcement that has no date yet.</summary>
    public DateOnly? Released { get; init; }

    /// <summary>The parts played and jobs done, each once, joined by a middle dot.</summary>
    public string? Role { get; init; }

    public bool Acting { get; init; }

    public bool Directing { get; init; }

    public bool Writing { get; init; }

    /// <summary>Whether the credit is only an appearance as oneself or on an unscripted show.</summary>
    public bool Appearance { get; init; }

    public int Votes { get; init; }

    public double? Rating { get; init; }

    public string? Overview { get; init; }

    public string? PosterPath { get; init; }

    public IReadOnlyList<int> GenreIds { get; init; } = [];

    public string? OriginalLanguage { get; init; }

    public double Popularity { get; init; }

    /// <summary>The credit as an entry of a list, for whatever shows a list's entries.</summary>
    public ListedTitle AsListed() => new()
    {
        TmdbId = TmdbId,
        Kind = Kind,
        Title = Title,
        Released = Released,
        Rating = Rating,
        Votes = Votes,
        Overview = Overview,
        PosterPath = PosterPath,
        GenreIds = GenreIds,
        OriginalLanguage = OriginalLanguage,
        Popularity = Popularity,
    };
}

/// <summary>A person a name search found, and what the database knows them for.</summary>
public sealed record PersonMatch
{
    public required int TmdbId { get; init; }

    public required string Name { get; init; }

    public string? KnownFor { get; init; }

    public string? ProfilePath { get; init; }

    public double Popularity { get; init; }

    /// <summary>Up to three titles they are best known for, each <c>Name (year)</c>.</summary>
    public IReadOnlyList<string> KnownForTitles { get; init; } = [];
}

/// <summary>One of the database's keywords: the vocabulary a film is described by, such as <c>black and white</c>.</summary>
public sealed record KeywordMatch
{
    public required int Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>A language as the database names it: the ISO 639-1 code and the English name.</summary>
public sealed record LanguageName
{
    public required string Code { get; init; }

    public required string Name { get; init; }
}

/// <summary>A title's place in the database: which id space it is in, and its id there.</summary>
public sealed record TitleKey
{
    public required string Kind { get; init; }

    public required int TmdbId { get; init; }
}

/// <summary>What a title is tagged with and who made it, as ids: what a discovery's filters are matched against.</summary>
public sealed record TitleTraits
{
    public IReadOnlyList<int> Keywords { get; init; } = [];

    public IReadOnlyList<int> People { get; init; } = [];
}

/// <summary>The sorts a discovery has.</summary>
public static class DiscoverSorts
{
    public const string Popular = "popular";
    public const string Rating = "rating";
    public const string Newest = "newest";
    public const string Oldest = "oldest";

    public static bool IsSort(string? sort) => sort is Popular or Rating or Newest or Oldest;
}

/// <summary>What a discovery asks the database for. Every filter is optional and they combine.</summary>
public sealed record DiscoverQuery
{
    /// <summary><c>film</c> or <c>show</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The genres a title is filed under all of.</summary>
    public IReadOnlyList<int> GenreIds { get; init; } = [];

    /// <summary>The first day of the release window.</summary>
    public DateOnly? From { get; init; }

    /// <summary>The last day of the release window.</summary>
    public DateOnly? To { get; init; }

    /// <summary>The genres a title is filed under none of.</summary>
    public IReadOnlyList<int> WithoutGenreIds { get; init; } = [];

    /// <summary>People who are in a film or made it, all of them. Films only: the database cannot filter a show by people.</summary>
    public IReadOnlyList<int> PersonIds { get; init; } = [];

    /// <summary>The keywords a title is tagged with all of.</summary>
    public IReadOnlyList<int> KeywordIds { get; init; } = [];

    /// <summary>The language it was made in, an ISO 639-1 code.</summary>
    public string? Language { get; init; }

    public double? MinRating { get; init; }

    public int? MinVotes { get; init; }

    /// <summary>One of <see cref="DiscoverSorts"/>.</summary>
    public string Sort { get; init; } = DiscoverSorts.Popular;
}

/// <summary>What the database's keywords say about a title beyond what it is about.</summary>
public static class KeywordNames
{
    /// <summary>The keyword the database tags a film shot in black and white with. It has none for colour: a title not tagged is not known to be in colour.</summary>
    public const string BlackAndWhite = "black and white";

    /// <summary>Whether the keywords tag the title as black and white.</summary>
    public static bool MarksBlackAndWhite(IEnumerable<string>? keywords) =>
        keywords?.Any(k => string.Equals(k, BlackAndWhite, StringComparison.OrdinalIgnoreCase)) == true;
}
