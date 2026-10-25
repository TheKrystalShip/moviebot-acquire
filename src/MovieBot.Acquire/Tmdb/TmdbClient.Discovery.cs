using System.Globalization;

namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

// Finding films and shows rather than describing one: a search by name, a person by name and
// everything they are credited on, a discovery by what a title is like, and the titles the database
// counts as similar. Every answer names a title by the database's own id; TitleCard says which
// IMDb id that is, so a caller keeps the cards it has asked for and asks for each title once.

public sealed partial class TmdbClient
{
    /// <summary>
    /// Films and shows by their name or words from it, in the database's order. <paramref name="kind"/>
    /// limits the answer to films or shows, and null asks for both. Each of <paramref name="pages"/>
    /// holds twenty. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<IReadOnlyList<ListedTitle>> SearchTitlesAsync(string query, string? kind, int pages, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(query)) return [];
        var space = kind is null ? "multi" : Space(kind);
        if (space is null) return [];

        var found = new List<ListedTitle>();
        for (var page = 1; page <= pages; page++)
        {
            var answer = await GetAsync(
                $"search/{space}?include_adult=false&language={Uri.EscapeDataString(settings.Language)}&page={page}"
                + $"&query={Uri.EscapeDataString(query.Trim())}",
                TmdbJsonContext.Default.TmdbTitlePage, ct);
            var results = answer?.Results ?? [];
            found.AddRange(results.Select(r => Listed(r, kind ?? KindOf(r.MediaType))).OfType<ListedTitle>());
            if (results.Count < PageSize) break;
        }
        return found;
    }

    /// <summary>People by name, best match first, each with what the database knows them for. Throws <see cref="HttpRequestException"/> when the database cannot be reached.</summary>
    public async Task<IReadOnlyList<PersonMatch>> SearchPeopleAsync(string query, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(query)) return [];

        var answer = await GetAsync(
            $"search/person?include_adult=false&language={Uri.EscapeDataString(settings.Language)}&query={Uri.EscapeDataString(query.Trim())}",
            TmdbJsonContext.Default.TmdbPersonPage, ct);
        return (answer?.Results ?? [])
            .Where(p => p.Id > 0 && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new PersonMatch
            {
                TmdbId = p.Id,
                Name = p.Name!.Trim(),
                KnownFor = string.IsNullOrWhiteSpace(p.KnownForDepartment) ? null : p.KnownForDepartment.Trim(),
                ProfilePath = string.IsNullOrEmpty(p.ProfilePath) ? null : p.ProfilePath,
                Popularity = p.Popularity ?? 0,
                KnownForTitles = (p.KnownFor ?? [])
                    .Where(t => KindOf(t.MediaType) is not null && !string.IsNullOrWhiteSpace(t.Title ?? t.Name))
                    .Take(3)
                    .Select(t => Day(t.ReleaseDate ?? t.FirstAirDate) is { } day
                        ? $"{(t.Title ?? t.Name)!.Trim()} ({day.Year})"
                        : (t.Title ?? t.Name)!.Trim())
                    .ToList(),
            })
            .ToList();
    }

    /// <summary>The database's keywords matching some words, best match first. Throws <see cref="HttpRequestException"/> when the database cannot be reached.</summary>
    public async Task<IReadOnlyList<KeywordMatch>> SearchKeywordsAsync(string query, CancellationToken ct)
    {
        if (!options.Value.IsConfigured || string.IsNullOrWhiteSpace(query)) return [];

        var answer = await GetAsync($"search/keyword?query={Uri.EscapeDataString(query.Trim())}",
            TmdbJsonContext.Default.TmdbNamedPage, ct);
        return (answer?.Results ?? [])
            .Where(k => k.Id > 0 && !string.IsNullOrWhiteSpace(k.Name))
            .Select(k => new KeywordMatch { Id = k.Id, Name = k.Name!.Trim() })
            .ToList();
    }

    /// <summary>
    /// The database's production companies matching some words, in its own order: a studio is listed
    /// once per company it files under (<c>Blumhouse Productions</c>, <c>Blumhouse Television</c>).
    /// Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<IReadOnlyList<CompanyMatch>> SearchCompaniesAsync(string query, CancellationToken ct)
    {
        if (!options.Value.IsConfigured || string.IsNullOrWhiteSpace(query)) return [];

        var answer = await GetAsync($"search/company?query={Uri.EscapeDataString(query.Trim())}",
            TmdbJsonContext.Default.TmdbNamedPage, ct);
        return (answer?.Results ?? [])
            .Where(c => c.Id > 0 && !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new CompanyMatch { Id = c.Id, Name = c.Name!.Trim() })
            .ToList();
    }

    /// <summary>The languages the database names, by code and English name. Throws <see cref="HttpRequestException"/> when the database cannot be reached.</summary>
    public async Task<IReadOnlyList<LanguageName>> LanguagesAsync(CancellationToken ct)
    {
        if (!options.Value.IsConfigured) return [];

        var answer = await GetAsync("configuration/languages", TmdbJsonContext.Default.ListTmdbConfigLanguage, ct);
        return (answer ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Code) && !string.IsNullOrWhiteSpace(l.EnglishName ?? l.Name))
            .Select(l => new LanguageName { Code = l.Code!.Trim(), Name = (l.EnglishName ?? l.Name)!.Trim() })
            .ToList();
    }

    /// <summary>
    /// The films or shows matching a query, at most <paramref name="count"/>, in the sort the query
    /// names. A list of shows leaves out talk, news and reality. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<IReadOnlyList<ListedTitle>> DiscoverAsync(DiscoverQuery query, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(query.Kind) is not { } space || count <= 0) return [];

        // The database orders by a plain average, which puts a title a few hundred people rated above
        // the ones everybody has seen. A sort by rating reads the most voted titles instead and
        // orders them here by a rating weighted by their votes.
        if (query.Sort != DiscoverSorts.Rating) return (await DiscoverPageAsync(query, 1, count, ct)).Titles;

        var parameters = Parameters(query, settings.Language, settings.Region);
        var found = new List<ListedTitle>();
        for (var page = 1; page <= RatingPoolPages; page++)
        {
            var answer = await GetAsync($"discover/{space}?{parameters}&page={page}", TmdbJsonContext.Default.TmdbTitlePage, ct);
            var results = answer?.Results ?? [];
            found.AddRange(results.Select(r => Listed(r, query.Kind)).OfType<ListedTitle>());
            if (results.Count < PageSize) break;
        }
        return WeightedRating.Order(found).Take(count).ToList();
    }

    /// <summary>
    /// One page of a discovery in the database's own order: the <paramref name="page"/>th run of
    /// <paramref name="count"/> titles, rounded up to whole pages of the database's, with how many such
    /// pages the discovery has. A sort by rating is not reordered here; <see cref="DiscoverAsync"/> ranks
    /// it. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<ListedPage> DiscoverPageAsync(DiscoverQuery query, int page, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(query.Kind) is not { } space || page < 1 || count <= 0) return new ListedPage();

        var parameters = Parameters(query, settings.Language, settings.Region);
        return await PageOfAsync(at => $"discover/{space}?{parameters}&page={at}", query.Kind, page, count, null, ct);
    }

    /// <summary>
    /// A page of one of the database's own lists, as <see cref="DiscoverPageAsync"/> pages a discovery.
    /// Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<ListedPage> ChartPageAsync(TitleChart chart, int page, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || page < 1 || count <= 0) return new ListedPage();

        var language = Uri.EscapeDataString(settings.Language);
        var (path, kind) = chart switch
        {
            TitleChart.FilmsTrendingToday => ($"trending/movie/day?language={language}", "film"),
            TitleChart.ShowsTrendingToday => ($"trending/tv/day?language={language}", "show"),
            _ => ($"movie/now_playing?language={language}&region={Uri.EscapeDataString(settings.Region)}", "film"),
        };
        return await PageOfAsync(at => $"{path}&page={at}", kind, page, count,
            t => kind == "film" || !t.GenreIds.Any(UnscriptedShowGenres.Contains), ct);
    }

    /// <summary>One of the database's own lists, at most <paramref name="count"/>, in its order. Throws <see cref="HttpRequestException"/> when the database cannot be reached.</summary>
    public async Task<IReadOnlyList<ListedTitle>> ChartAsync(TitleChart chart, int count, CancellationToken ct) =>
        (await ChartPageAsync(chart, 1, count, ct)).Titles;

    /// <summary>
    /// The <paramref name="page"/>th run of whole database pages that holds <paramref name="count"/>
    /// titles, read from the path <paramref name="at"/> gives for each of the database's pages.
    /// </summary>
    private async Task<ListedPage> PageOfAsync(Func<int, string> at, string kind, int page, int count, Func<ListedTitle, bool>? keep, CancellationToken ct)
    {
        var perPage = (count + PageSize - 1) / PageSize;
        var first = (page - 1) * perPage + 1;
        var found = new List<ListedTitle>();
        var total = 0;
        var exhausted = false;
        for (var number = first; number < first + perPage; number++)
        {
            var answer = await GetAsync(at(number), TmdbJsonContext.Default.TmdbTitlePage, ct);
            var results = answer?.Results ?? [];
            total = answer?.TotalPages ?? 0;
            found.AddRange(results.Select(r => Listed(r, kind)).OfType<ListedTitle>().Where(t => keep?.Invoke(t) ?? true));
            if (results.Count < PageSize) { exhausted = true; break; }
        }

        // The database says how many pages it has; an answer without the count is read as ending
        // at the first short page.
        var pages = total > 0 ? (total + perPage - 1) / perPage : exhausted ? page : page + 1;
        return new ListedPage { Titles = found.Take(count).ToList(), Pages = Math.Max(page, pages) };
    }

    /// <summary>
    /// The films of one series, the first released first, leaving out any the database marks adult;
    /// null when the database has no collection under the id. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<TitleCollection?> CollectionAsync(int collectionId, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || collectionId <= 0) return null;

        var answer = await GetAsync($"collection/{collectionId}?language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbCollection, ct);
        if (answer is null || string.IsNullOrWhiteSpace(answer.Name)) return null;
        return new TitleCollection
        {
            TmdbId = answer.Id,
            Name = answer.Name.Trim(),
            Films = (answer.Parts ?? [])
                .Where(p => p.Adult != true)
                .Select(p => Listed(p, "film"))
                .OfType<ListedTitle>()
                .OrderBy(t => t.Released is null)
                .ThenBy(t => t.Released)
                .ToList(),
        };
    }

    /// <summary>
    /// What the database counts as similar to a title: its recommendations first, the titles people
    /// who watched it went on to watch, then the titles it files alike, over <paramref name="pages"/>
    /// of each. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<IReadOnlyList<ListedTitle>> SimilarAsync(string kind, int tmdbId, int pages, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(kind) is not { } space || tmdbId <= 0) return [];

        var seen = new HashSet<int> { tmdbId };
        var similar = new List<ListedTitle>();
        foreach (var list in new[] { "recommendations", "similar" })
        {
            for (var page = 1; page <= pages; page++)
            {
                var answer = await GetAsync(
                    $"{space}/{tmdbId}/{list}?language={Uri.EscapeDataString(settings.Language)}&page={page}",
                    TmdbJsonContext.Default.TmdbTitlePage, ct);
                var results = answer?.Results ?? [];
                similar.AddRange(results.Select(r => Listed(r, kind)).OfType<ListedTitle>().Where(t => seen.Add(t.TmdbId)));
                if (results.Count < PageSize) break;
            }
        }
        return similar;
    }

    /// <summary>
    /// The title an IMDb id names, a film where it names both; null when the database has neither
    /// under it. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<TitleKey?> FindAsync(string imdbId, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || ImdbId.ToTag(ImdbId.ToNumber(imdbId)) is not { } tag) return null;

        var found = await GetAsync($"find/{tag}?external_source=imdb_id&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbFindResponse, ct);
        return found?.MovieResults is { Count: > 0 } films ? new TitleKey { Kind = "film", TmdbId = films[0].Id }
            : found?.TvResults is { Count: > 0 } shows ? new TitleKey { Kind = "show", TmdbId = shows[0].Id }
            : null;
    }

    /// <summary>
    /// A person and every title they are credited on, cast and crew, films and shows, with nothing
    /// left out and no title named by its IMDb id; null when the database has nobody under the id.
    /// A title credited more than one way is one entry with every way joined. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<Filmography?> FilmographyAsync(int personId, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || personId <= 0) return null;

        var person = await GetAsync(
            $"person/{personId}?append_to_response=combined_credits&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbPerson, ct);
        if (person is null || string.IsNullOrWhiteSpace(person.Name)) return null;

        var credited = new Dictionary<(string Kind, int Id), Credited>();
        Credited Entry(TmdbListedTitle title, string kind)
        {
            if (!credited.TryGetValue((kind, title.Id), out var entry))
                credited[(kind, title.Id)] = entry = new Credited(title, kind);
            return entry;
        }

        foreach (var part in person.CombinedCredits?.Cast ?? [])
        {
            if (KindOf(part.MediaType) is not { } kind || part.Adult == true) continue;
            var entry = Entry(part, kind);
            entry.AddRole(part.Character);
            if (part.EpisodeCount is > 0) entry.Episodes = Math.Max(entry.Episodes ?? 0, part.EpisodeCount.Value);
            if (IsAppearance(part)) entry.Appeared = true;
            else entry.Acting = true;
        }
        foreach (var job in person.CombinedCredits?.Crew ?? [])
        {
            if (KindOf(job.MediaType) is not { } kind || job.Adult == true) continue;
            var entry = Entry(job, kind);
            entry.AddRole(job.Job);
            entry.Crew = true;
            if (string.Equals(job.Department, "Directing", StringComparison.OrdinalIgnoreCase)) entry.Directing = true;
            if (string.Equals(job.Department, "Writing", StringComparison.OrdinalIgnoreCase)) entry.Writing = true;
        }

        return new Filmography
        {
            TmdbId = person.Id,
            Name = person.Name.Trim(),
            KnownFor = string.IsNullOrWhiteSpace(person.KnownForDepartment) ? null : person.KnownForDepartment.Trim(),
            ProfilePath = string.IsNullOrEmpty(person.ProfilePath) ? null : person.ProfilePath,
            Credits = credited.Values
                .Where(c => !string.IsNullOrWhiteSpace(c.Title.Title ?? c.Title.Name))
                .Select(c => c.Read())
                .ToList(),
        };
    }

    /// <summary>
    /// The IMDb id, genres, makers, top-billed actors and a film's runtime of a title, in one call; null when the
    /// database has no such title. Throws <see cref="HttpRequestException"/> when the database cannot
    /// be reached.
    /// </summary>
    public async Task<TitleCard?> CardAsync(string kind, int tmdbId, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || tmdbId <= 0) return null;
        var language = Uri.EscapeDataString(settings.Language);

        if (kind == "film")
        {
            var movie = await GetAsync($"movie/{tmdbId}?append_to_response=credits&language={language}", TmdbJsonContext.Default.TmdbMovie, ct);
            if (movie is null) return null;
            return new TitleCard
            {
                TmdbId = movie.Id,
                Kind = kind,
                ImdbId = ImdbId.ToTag(ImdbId.ToNumber(movie.ImdbId)),
                Title = (movie.Title ?? movie.OriginalTitle ?? "").Trim(),
                Released = Day(movie.ReleaseDate),
                Status = string.IsNullOrWhiteSpace(movie.Status) ? null : movie.Status.Trim(),
                Genres = Names(movie.Genres?.Select(g => g.Name)),
                MadeBy = Names((movie.Credits?.Crew ?? []).Where(c => string.Equals(c.Job, "Director", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)),
                Starring = Names((movie.Credits?.Cast ?? []).OrderBy(c => c.Order ?? int.MaxValue).Select(c => c.Name).Take(2)),
                OriginalLanguage = string.IsNullOrEmpty(movie.OriginalLanguage) ? null : movie.OriginalLanguage,
                RuntimeMinutes = movie.Runtime is > 0 ? movie.Runtime : null,
            };
        }

        if (kind != "show") return null;
        var tv = await GetAsync($"tv/{tmdbId}?append_to_response=external_ids,aggregate_credits&language={language}", TmdbJsonContext.Default.TmdbTv, ct);
        if (tv is null) return null;
        return new TitleCard
        {
            TmdbId = tv.Id,
            Kind = kind,
            ImdbId = ImdbId.ToTag(ImdbId.ToNumber(tv.ExternalIds?.ImdbId)),
            Title = (tv.Name ?? tv.OriginalName ?? "").Trim(),
            Released = Day(tv.FirstAirDate),
            Status = string.IsNullOrWhiteSpace(tv.Status) ? null : tv.Status.Trim(),
            Genres = Names(tv.Genres?.Select(g => g.Name)),
            MadeBy = Names(tv.CreatedBy?.Select(c => c.Name)),
            Starring = Names((tv.AggregateCredits?.Cast ?? []).OrderBy(c => c.Order ?? int.MaxValue).Select(c => c.Name).Take(2)),
            OriginalLanguage = string.IsNullOrEmpty(tv.OriginalLanguage) ? null : tv.OriginalLanguage,
        };
    }

    /// <summary>
    /// The keywords a title is tagged with and, for a film, the people in it and behind it, as ids;
    /// null when the database has no such title. Throws <see cref="HttpRequestException"/> when the
    /// database cannot be reached.
    /// </summary>
    public async Task<TitleTraits?> TraitsAsync(string kind, int tmdbId, CancellationToken ct)
    {
        if (!options.Value.IsConfigured || tmdbId <= 0) return null;

        if (kind == "film")
        {
            var movie = await GetAsync($"movie/{tmdbId}?append_to_response=keywords,credits", TmdbJsonContext.Default.TmdbMovie, ct);
            if (movie is null) return null;
            return new TitleTraits
            {
                Keywords = (movie.Keywords?.Keywords ?? movie.Keywords?.Results ?? []).Select(k => k.Id).Where(id => id > 0).ToList(),
                People = (movie.Credits?.Cast ?? []).Select(c => c.Id)
                    .Concat((movie.Credits?.Crew ?? []).Select(c => c.Id)).Where(id => id > 0).Distinct().ToList(),
            };
        }

        if (kind != "show") return null;
        var tv = await GetAsync($"tv/{tmdbId}?append_to_response=keywords", TmdbJsonContext.Default.TmdbTv, ct);
        return tv is null ? null : new TitleTraits
        {
            Keywords = (tv.Keywords?.Results ?? tv.Keywords?.Keywords ?? []).Select(k => k.Id).Where(id => id > 0).ToList(),
        };
    }

    /// <summary>The query string of a discovery, its keys in order so one query is one string.</summary>
    internal static string Parameters(DiscoverQuery query, string language, string region)
    {
        var film = query.Kind == "film";
        var dates = film ? "primary_release_date" : "first_air_date";
        var sort = query.Sort switch
        {
            DiscoverSorts.Rating => "vote_count.desc",
            DiscoverSorts.Newest => dates + ".desc",
            DiscoverSorts.Oldest => dates + ".asc",
            DiscoverSorts.LeastPopular => "popularity.asc",
            _ => "popularity.desc",
        };

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["include_adult"] = "false",
            ["language"] = language,
            ["sort_by"] = sort,
        };
        // The database reads a comma as "all of" and a bar as "any of", and one parameter cannot say
        // both, so a query asks one way or the other.
        if (query.GenreIds.Count > 0 && query.AnyGenreIds.Count > 0)
            throw new ArgumentException("A discovery names genres all of or any of, not both.", nameof(query));
        if (query.KeywordIds.Count > 0 && query.AnyKeywordIds.Count > 0)
            throw new ArgumentException("A discovery names keywords all of or any of, not both.", nameof(query));
        if (!string.IsNullOrWhiteSpace(query.Language) && query.AnyLanguages.Count > 0)
            throw new ArgumentException("A discovery names one language or several, not both.", nameof(query));
        if (query.GenreIds.Count > 0) parameters["with_genres"] = string.Join(',', query.GenreIds);
        else if (query.AnyGenreIds.Count > 0) parameters["with_genres"] = string.Join('|', query.AnyGenreIds);
        if (query.KeywordIds.Count > 0) parameters["with_keywords"] = string.Join(',', query.KeywordIds);
        else if (query.AnyKeywordIds.Count > 0) parameters["with_keywords"] = string.Join('|', query.AnyKeywordIds);
        if (film && query.PersonIds.Count > 0) parameters["with_people"] = string.Join(',', query.PersonIds);
        if (!string.IsNullOrWhiteSpace(query.Language)) parameters["with_original_language"] = query.Language.Trim();
        else if (query.AnyLanguages.Count > 0) parameters["with_original_language"] = string.Join('|', query.AnyLanguages.Select(l => l.Trim()));
        if (!string.IsNullOrWhiteSpace(query.OriginCountry)) parameters["with_origin_country"] = query.OriginCountry.Trim();
        if (query.CompanyIds.Count > 0) parameters["with_companies"] = string.Join('|', query.CompanyIds);
        if (!film && query.NetworkIds.Count > 0) parameters["with_networks"] = string.Join('|', query.NetworkIds);
        if (query.From is { } from) parameters[dates + ".gte"] = Iso(from);
        if (query.To is { } to) parameters[dates + ".lte"] = Iso(to);
        if (query.MinRuntime is { } shortest) parameters["with_runtime.gte"] = shortest.ToString(CultureInfo.InvariantCulture);
        if (query.MaxRuntime is { } longest) parameters["with_runtime.lte"] = longest.ToString(CultureInfo.InvariantCulture);
        if (query.MinRating is { } rating) parameters["vote_average.gte"] = rating.ToString("0.0", CultureInfo.InvariantCulture);
        if (query.MaxRating is { } ceiling) parameters["vote_average.lte"] = ceiling.ToString("0.0", CultureInfo.InvariantCulture);
        if (query.MinVotes is { } votes) parameters["vote_count.gte"] = votes.ToString(CultureInfo.InvariantCulture);
        if (query.MaxVotes is { } most) parameters["vote_count.lte"] = most.ToString(CultureInfo.InvariantCulture);
        if (film && !string.IsNullOrWhiteSpace(query.MaxCertification))
        {
            parameters["certification_country"] = region;
            parameters["certification.lte"] = query.MaxCertification.Trim();
        }
        if (!film)
        {
            if (query.AiringFrom is { } airingFrom) parameters["air_date.gte"] = Iso(airingFrom);
            if (query.AiringTo is { } airingTo) parameters["air_date.lte"] = Iso(airingTo);
            if (query.ShowStatus is { } status) parameters["with_status"] = status.ToString(CultureInfo.InvariantCulture);
            if (query.ShowType is { } type) parameters["with_type"] = type.ToString(CultureInfo.InvariantCulture);
        }

        IEnumerable<int> without = query.WithoutGenreIds;
        if (!film && query.GenreIds.Count == 0) without = without.Concat(UnscriptedShowGenres);
        if (without.Distinct().ToList() is { Count: > 0 } left) parameters["without_genres"] = string.Join("%7C", left);

        return string.Join('&', parameters.Select(p => $"{p.Key}={(p.Key == "without_genres" ? p.Value : Uri.EscapeDataString(p.Value))}"));
    }

    /// <summary>How many pages of the most voted titles a sort by rating reads and orders.</summary>
    private const int RatingPoolPages = 5;

    /// <summary>How many titles a sort by rating ranks: the most voted of the discovery.</summary>
    public const int RatingPoolSize = RatingPoolPages * PageSize;

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A list's entry as a title, null for an adult one, one with no name or one whose kind is not known.</summary>
    private static ListedTitle? Listed(TmdbListedTitle title, string? kind)
    {
        var name = title.Title ?? title.Name;
        if (kind is null || title.Adult == true || string.IsNullOrWhiteSpace(name)) return null;
        return new ListedTitle
        {
            TmdbId = title.Id,
            Kind = kind,
            Title = name.Trim(),
            Released = Day(title.ReleaseDate ?? title.FirstAirDate),
            Rating = title.VoteCount is > 0 && title.VoteAverage is > 0 ? Math.Round(title.VoteAverage.Value, 1) : null,
            Votes = title.VoteCount ?? 0,
            Overview = string.IsNullOrWhiteSpace(title.Overview) ? null : title.Overview.Trim(),
            PosterPath = string.IsNullOrEmpty(title.PosterPath) ? null : title.PosterPath,
            GenreIds = title.GenreIds?.ToList() ?? [],
            OriginalLanguage = string.IsNullOrEmpty(title.OriginalLanguage) ? null : title.OriginalLanguage,
            Popularity = title.Popularity ?? 0,
        };
    }

    /// <summary>A title's credits gathered up: every way a person is credited on it.</summary>
    private sealed class Credited(TmdbListedTitle title, string kind)
    {
        private readonly List<string> _roles = [];

        public TmdbListedTitle Title { get; } = title;
        public bool Acting { get; set; }
        public int? Episodes { get; set; }
        public bool Appeared { get; set; }
        public bool Crew { get; set; }
        public bool Directing { get; set; }
        public bool Writing { get; set; }

        public void AddRole(string? role)
        {
            if (!string.IsNullOrWhiteSpace(role) && !_roles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase))
                _roles.Add(role.Trim());
        }

        public FilmCredit Read() => new()
        {
            TmdbId = Title.Id,
            Kind = kind,
            Title = (Title.Title ?? Title.Name)!.Trim(),
            Released = Day(Title.ReleaseDate ?? Title.FirstAirDate),
            Role = _roles.Count > 0 ? string.Join(" · ", _roles) : null,
            Acting = Acting,
            Episodes = kind == "show" ? Episodes : null,
            Directing = Directing,
            Writing = Writing,
            Appearance = Appeared && !Acting && !Crew,
            Votes = Title.VoteCount ?? 0,
            Rating = Title.VoteCount is > 0 && Title.VoteAverage is > 0 ? Math.Round(Title.VoteAverage.Value, 1) : null,
            Overview = string.IsNullOrWhiteSpace(Title.Overview) ? null : Title.Overview.Trim(),
            PosterPath = string.IsNullOrEmpty(Title.PosterPath) ? null : Title.PosterPath,
            GenreIds = Title.GenreIds?.ToList() ?? [],
            OriginalLanguage = string.IsNullOrEmpty(Title.OriginalLanguage) ? null : Title.OriginalLanguage,
            Popularity = Title.Popularity ?? 0,
        };
    }
}
