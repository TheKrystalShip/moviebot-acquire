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

/// <summary>One of the database's keywords: the vocabulary a film is described by, such as <c>time loop</c>.</summary>
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

    /// <summary>Genres a title is filed under at least one of; a query names these or <see cref="GenreIds"/>.</summary>
    public IReadOnlyList<int> AnyGenreIds { get; init; } = [];

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

    /// <summary>Keywords a title is tagged with at least one of; a query names these or <see cref="KeywordIds"/>.</summary>
    public IReadOnlyList<int> AnyKeywordIds { get; init; } = [];

    /// <summary>The language it was made in, an ISO 639-1 code.</summary>
    public string? Language { get; init; }

    /// <summary>Languages it was made in one of, each an ISO 639-1 code; a query names these or <see cref="Language"/>.</summary>
    public IReadOnlyList<string> AnyLanguages { get; init; } = [];

    /// <summary>The country it was made in, an ISO 3166-1 code.</summary>
    public string? OriginCountry { get; init; }

    /// <summary>The companies that made a title, at least one of them.</summary>
    public IReadOnlyList<int> CompanyIds { get; init; } = [];

    /// <summary>The networks a show is on, at least one of them. Shows only.</summary>
    public IReadOnlyList<int> NetworkIds { get; init; } = [];

    /// <summary>The shortest a film runs, in minutes; for a show, an episode.</summary>
    public int? MinRuntime { get; init; }

    /// <summary>The longest a film runs, in minutes; for a show, an episode.</summary>
    public int? MaxRuntime { get; init; }

    public double? MinRating { get; init; }

    public double? MaxRating { get; init; }

    public int? MinVotes { get; init; }

    public int? MaxVotes { get; init; }

    /// <summary>
    /// The highest certificate a film carries in the database's region, such as <c>PG</c>, on that
    /// region's own scale. Films only.
    /// </summary>
    public string? MaxCertification { get; init; }

    /// <summary>The first day a show has an episode airing on. Shows only.</summary>
    public DateOnly? AiringFrom { get; init; }

    /// <summary>The last day a show has an episode airing on. Shows only.</summary>
    public DateOnly? AiringTo { get; init; }

    /// <summary>Where a show stands, one of <see cref="ShowStatuses"/>. Shows only.</summary>
    public int? ShowStatus { get; init; }

    /// <summary>What a show is, one of <see cref="ShowTypes"/>. Shows only.</summary>
    public int? ShowType { get; init; }

    /// <summary>One of <see cref="DiscoverSorts"/>.</summary>
    public string Sort { get; init; } = DiscoverSorts.Popular;
}

/// <summary>The lists the database keeps of its own rather than answers to a query.</summary>
public enum TitleChart
{
    /// <summary>The films most looked at today.</summary>
    FilmsTrendingToday,

    /// <summary>The shows most looked at today.</summary>
    ShowsTrendingToday,

    /// <summary>The films showing in cinemas in the database's region.</summary>
    InCinemas,
}

/// <summary>The films of one series, in the order they came out.</summary>
public sealed record TitleCollection
{
    public required int TmdbId { get; init; }

    public required string Name { get; init; }

    /// <summary>Its films, the first released first and those with no date after them.</summary>
    public IReadOnlyList<ListedTitle> Films { get; init; } = [];
}

/// <summary>The database's numbers for where a show stands.</summary>
public static class ShowStatuses
{
    public const int Ended = 3;
}

/// <summary>The database's numbers for what a show is.</summary>
public static class ShowTypes
{
    public const int Miniseries = 2;
}
