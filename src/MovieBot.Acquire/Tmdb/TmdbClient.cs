using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;

namespace TheKrystalShip.MovieBot.Acquire.Tmdb;

/// <summary>
/// What a film or a show is, from The Movie Database: its credits, certificate, genres and what it
/// is about, and for a show its seasons and episodes.
///
/// A film or a show is named here by its IMDb id, as everywhere else in MovieBot, and found in the
/// database through it. Nothing is cached in this class. The caller keeps the description and the
/// images on disk and serves its own copies, so the database is asked once per title rather than by
/// every page that shows it, and nobody watching ever reaches it directly.
///
/// The database's terms ask for attribution of anything shown from it; that is the surfaces'.
/// </summary>
public sealed partial class TmdbClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
{
    private const int CastShown = 12;

    private static readonly HashSet<string> WritingJobs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screenplay", "Writer", "Story", "Novel", "Characters", "Author",
    };

    /// <summary>The most trailers a description keeps.</summary>
    public const int TrailersKept = 6;

    /// <summary>The most reviews a description keeps, each cut to <see cref="ExcerptLength"/> characters.</summary>
    public const int ReviewsKept = 3;

    /// <summary>How long a review's excerpt may be.</summary>
    public const int ExcerptLength = 400;

    /// <summary>The most recommendations a description keeps: the database's first page.</summary>
    public const int RecommendationsKept = 20;

    /// <summary>What a description asks for beside the title's own facts, in the same call.</summary>
    private const string Extras = "videos,recommendations,reviews,images,watch/providers";

    /// <summary>The languages the videos and logos of a description are asked in: the configured one, English, and none (speechless, textless).</summary>
    private static string ExtraLanguages(TmdbOptions settings)
    {
        var languages = new List<string> { LanguageCode(settings) };
        if (!languages.Contains("en")) languages.Add("en");
        languages.Add("null");
        var joined = Uri.EscapeDataString(string.Join(',', languages));
        return $"&include_video_language={joined}&include_image_language={joined}";
    }

    /// <summary>The ISO 639-1 part of the configured language (<c>en</c> of <c>en-US</c>).</summary>
    private static string LanguageCode(TmdbOptions settings) =>
        settings.Language.Split('-', '_')[0].ToLowerInvariant();

    public bool IsConfigured => options.Value.IsConfigured;

    /// <summary>
    /// The film an IMDb id names, or null when the database has no film under it. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached or refuses the token,
    /// so a caller holding an older description keeps it rather than forgetting it.
    /// </summary>
    public async Task<FilmDetails?> DetailsAsync(string imdbId, CancellationToken ct)
    {
        if (ImdbId.ToTag(ImdbId.ToNumber(imdbId)) is not { } tag) return null;
        var settings = options.Value;
        if (!settings.IsConfigured) return null;

        var found = await GetAsync($"find/{tag}?external_source=imdb_id&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbFindResponse, ct);
        if (found?.MovieResults is not { Count: > 0 } results)
        {
            logger.LogInformation("{Imdb} names no film in TMDb.", tag);
            return null;
        }

        var movie = await GetAsync(
            $"movie/{results[0].Id}?append_to_response=credits,release_dates,keywords,{Extras}&language={Uri.EscapeDataString(settings.Language)}"
            + ExtraLanguages(settings),
            TmdbJsonContext.Default.TmdbMovie, ct);
        return movie is null ? null : Read(movie, tag, settings);
    }

    /// <summary>
    /// The film or show an IMDb id names, at a glance, in one call; a film where the id names both.
    /// Null when the database has neither under it. Throws <see cref="HttpRequestException"/> when the
    /// database cannot be reached, as for a film.
    /// </summary>
    public async Task<TitleGlance?> GlanceAsync(string imdbId, CancellationToken ct)
    {
        if (ImdbId.ToTag(ImdbId.ToNumber(imdbId)) is not { } tag) return null;
        var settings = options.Value;
        if (!settings.IsConfigured) return null;

        var found = await GetAsync($"find/{tag}?external_source=imdb_id&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbFindResponse, ct);
        var (hit, kind) = found?.MovieResults is { Count: > 0 } films ? (films[0], "film")
            : found?.TvResults is { Count: > 0 } shows ? (shows[0], "show")
            : (null, "");
        if (hit is null) return null;

        return new TitleGlance
        {
            TmdbId = hit.Id,
            Kind = kind,
            Rating = hit.VoteCount is > 0 && hit.VoteAverage is > 0 ? Math.Round(hit.VoteAverage.Value, 1) : null,
            Votes = hit.VoteCount ?? 0,
            Overview = string.IsNullOrWhiteSpace(hit.Overview) ? null : hit.Overview.Trim(),
            Released = DateOnly.TryParseExact(hit.ReleaseDate ?? hit.FirstAirDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day) ? day : null,
            PosterPath = string.IsNullOrEmpty(hit.PosterPath) ? null : hit.PosterPath,
        };
    }

    /// <summary>
    /// The show an IMDb id names, or null when the database has no show under it. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached, as for a film.
    /// </summary>
    public async Task<ShowDetails?> ShowAsync(string imdbId, CancellationToken ct)
    {
        if (ImdbId.ToTag(ImdbId.ToNumber(imdbId)) is not { } tag) return null;
        var settings = options.Value;
        if (!settings.IsConfigured) return null;

        var found = await GetAsync($"find/{tag}?external_source=imdb_id&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbFindResponse, ct);
        if (found?.TvResults is not { Count: > 0 } results)
        {
            logger.LogInformation("{Imdb} names no show in TMDb.", tag);
            return null;
        }

        var tv = await GetAsync(
            $"tv/{results[0].Id}?append_to_response=aggregate_credits,content_ratings,keywords,{Extras}&language={Uri.EscapeDataString(settings.Language)}"
            + ExtraLanguages(settings),
            TmdbJsonContext.Default.TmdbTv, ct);
        return tv is null ? null : ReadShow(tv, tag, settings);
    }

    /// <summary>
    /// One season of a show, by the show's database id, with its episodes; null when the show has
    /// no such season. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<SeasonDetails?> SeasonAsync(int showTmdbId, int season, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || season < 0) return null;

        var answer = await GetAsync(
            $"tv/{showTmdbId}/season/{season}?language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbTvSeason, ct);
        return answer is null ? null : ReadSeason(answer);
    }

    /// <summary>
    /// The films the database counts as trending this week, most first. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public Task<IReadOnlyList<TrendingTitle>> TrendingFilmsAsync(CancellationToken ct) => TrendingAsync("movie", ct);

    /// <summary>The shows the database counts as trending this week, most first, on the same terms.</summary>
    public Task<IReadOnlyList<TrendingTitle>> TrendingShowsAsync(CancellationToken ct) => TrendingAsync("tv", ct);

    /// <summary>
    /// One week's trending list, each entry's IMDb id asked for in turn: the list names a title by
    /// the database's own id alone. An entry with no IMDb id is one nothing here could lead to, and
    /// an adult one is never shown, so neither is kept.
    /// </summary>
    private async Task<IReadOnlyList<TrendingTitle>> TrendingAsync(string kind, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return [];

        var language = Uri.EscapeDataString(settings.Language);
        var page = await GetAsync($"trending/{kind}/week?language={language}", TmdbJsonContext.Default.TmdbTitlePage, ct);

        var titles = new List<TrendingTitle>();
        foreach (var item in page?.Results ?? [])
        {
            var name = item.Title ?? item.Name;
            if (item.Adult == true || string.IsNullOrWhiteSpace(name)) continue;

            var ids = await GetAsync($"{kind}/{item.Id}/external_ids", TmdbJsonContext.Default.TmdbExternalIds, ct);
            if (ImdbId.ToTag(ImdbId.ToNumber(ids?.ImdbId)) is not { } tag) continue;

            var date = item.ReleaseDate ?? item.FirstAirDate;
            titles.Add(new TrendingTitle
            {
                TmdbId = item.Id,
                ImdbId = tag,
                Title = name.Trim(),
                Year = date is { Length: >= 4 } && int.TryParse(date[..4], out var year) ? year : null,
                Rating = item.VoteCount is > 0 && item.VoteAverage is > 0 ? Math.Round(item.VoteAverage.Value, 1) : null,
                Votes = item.VoteCount ?? 0,
                PosterPath = string.IsNullOrEmpty(item.PosterPath) ? null : item.PosterPath,
            });
        }
        return titles;
    }

    /// <summary>
    /// A person and the films and shows they are credited on, best known first, at most
    /// <paramref name="count"/>; null when the database has nobody under the id. A credit is a part
    /// they played or a job that made the title theirs (<see cref="CreditedJobs"/>), and one title
    /// credited both ways is listed once with both. Their appearances as themselves and on talk, news
    /// and reality shows are left out, being promotion rather than work. Throws
    /// <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<PersonCredits?> PersonAsync(int personId, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || personId <= 0) return null;

        var person = await GetAsync(
            $"person/{personId}?append_to_response=combined_credits&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbPerson, ct);
        if (person is null || string.IsNullOrWhiteSpace(person.Name)) return null;

        var credited = new Dictionary<(string Kind, int Id), (TmdbListedTitle Title, List<string> Roles)>();
        void Credit(TmdbListedTitle title, string? role)
        {
            if (KindOf(title.MediaType) is not { } kind || title.Adult == true) return;
            if (!credited.TryGetValue((kind, title.Id), out var entry))
                credited[(kind, title.Id)] = entry = (title, []);
            if (!string.IsNullOrWhiteSpace(role) && !entry.Roles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase))
                entry.Roles.Add(role.Trim());
        }

        foreach (var part in person.CombinedCredits?.Cast ?? [])
            if (!IsAppearance(part)) Credit(part, part.Character);
        foreach (var job in person.CombinedCredits?.Crew ?? [])
            if (job.Job is { } name && CreditedJobs.Contains(name)) Credit(job, name);

        // A title nobody has voted on and with no date is an announcement, not yet anything to watch.
        var best = credited
            .Where(c => c.Value.Title.VoteCount is > 0 || Day(c.Value.Title.ReleaseDate ?? c.Value.Title.FirstAirDate) is not null)
            .OrderByDescending(c => c.Value.Title.VoteCount ?? 0)
            .ThenByDescending(c => c.Value.Title.Popularity ?? 0)
            .Take(Math.Max(0, count))
            .Select(c => (c.Value.Title, c.Key.Kind, Role: c.Value.Roles.Count > 0 ? string.Join(" · ", c.Value.Roles) : null))
            .ToList();

        return new PersonCredits
        {
            TmdbId = person.Id,
            Name = person.Name.Trim(),
            KnownFor = string.IsNullOrWhiteSpace(person.KnownForDepartment) ? null : person.KnownForDepartment.Trim(),
            ProfilePath = string.IsNullOrEmpty(person.ProfilePath) ? null : person.ProfilePath,
            Titles = await NamedAsync(best, ct),
        };
    }

    /// <summary>
    /// The database's genres for films (<c>film</c>) or for shows (<c>show</c>); empty for any other
    /// kind. Throws <see cref="HttpRequestException"/> when the database cannot be reached.
    /// </summary>
    public async Task<IReadOnlyList<CatalogueGenre>> GenresAsync(string kind, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(kind) is not { } space) return [];

        var list = await GetAsync($"genre/{space}/list?language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbGenreList, ct);
        return (list?.Genres ?? [])
            .Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => new CatalogueGenre { Id = g.Id, Name = g.Name!.Trim() })
            .ToList();
    }

    /// <summary>
    /// The films or shows in one of the database's genres, the most popular first, at most
    /// <paramref name="count"/>. Only a title with <see cref="GenreVotesAtLeast"/> votes is listed,
    /// which is what keeps a genre's list to things people have seen, and a list of shows leaves out
    /// talk, news and reality. Throws <see cref="HttpRequestException"/> when the database cannot be
    /// reached.
    /// </summary>
    public Task<IReadOnlyList<CatalogueTitle>> GenreAsync(string kind, int genreId, int count, CancellationToken ct) =>
        GenreAsync(kind, [genreId], null, count, ct);

    /// <summary>
    /// The films or shows filed under every one of <paramref name="genreIds"/>, and made in
    /// <paramref name="originalLanguage"/> (an ISO 639-1 code) when it is named, on the terms of a
    /// single genre's list.
    /// </summary>
    public async Task<IReadOnlyList<CatalogueTitle>> GenreAsync(
        string kind, IReadOnlyList<int> genreIds, string? originalLanguage, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(kind) is not { } space || genreIds.Count == 0 || genreIds.Any(id => id <= 0) || count <= 0) return [];

        var language = Uri.EscapeDataString(settings.Language);
        var without = kind == "show" ? "&without_genres=" + string.Join("%7C", UnscriptedShowGenres) : "";
        var genreId = string.Join(",", genreIds.Distinct());
        var made = string.IsNullOrWhiteSpace(originalLanguage) ? "" : "&with_original_language=" + Uri.EscapeDataString(originalLanguage.Trim());
        var listed = new List<(TmdbListedTitle Title, string Kind, string? Role)>();
        for (var page = 1; listed.Count < count && page <= (count + PageSize - 1) / PageSize; page++)
        {
            var answer = await GetAsync(
                $"discover/{space}?include_adult=false&language={language}&page={page}&sort_by=popularity.desc"
                + $"&vote_count.gte={GenreVotesAtLeast}&with_genres={genreId}{made}{without}",
                TmdbJsonContext.Default.TmdbTitlePage, ct);
            var results = answer?.Results ?? [];
            listed.AddRange(results
                .Where(t => t.Adult != true && !string.IsNullOrWhiteSpace(t.Title ?? t.Name))
                .Select(t => (t, kind, (string?)null)));
            if (results.Count < PageSize) break;
        }
        return await NamedAsync(listed.Take(count).ToList(), ct);
    }

    /// <summary>The fewest votes a title in a genre's list has.</summary>
    public const int GenreVotesAtLeast = 100;

    /// <summary>How many titles one page of a list holds.</summary>
    private const int PageSize = 20;

    /// <summary>How many titles' IMDb ids are asked for at once.</summary>
    private const int LookupsAtOnce = 8;

    /// <summary>The jobs besides acting that make a title part of a person's work.</summary>
    private static readonly HashSet<string> CreditedJobs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Director", "Creator", "Writer", "Screenplay", "Story", "Novel",
    };

    /// <summary>The database's genres for talk, news and reality shows.</summary>
    private static readonly int[] UnscriptedShowGenres = [10767, 10763, 10764];

    /// <summary>A part played as oneself, or any part on a talk, news or reality show.</summary>
    private static bool IsAppearance(TmdbListedTitle part)
    {
        if (part.MediaType == "tv" && part.GenreIds is { } genres && genres.Any(UnscriptedShowGenres.Contains)) return true;
        var character = part.Character?.Trim() ?? "";
        return SelfWords.Any(word => character.StartsWith(word, StringComparison.OrdinalIgnoreCase)
                                     && (character.Length == word.Length || !char.IsLetter(character[word.Length])));
    }

    private static readonly string[] SelfWords = ["Self", "Himself", "Herself", "Themselves", "Themself"];

    /// <summary>
    /// Each listed title with its IMDb id, asked for side by side and kept in the order given; a title
    /// without one is one nothing here could lead to, and is left out.
    /// </summary>
    private async Task<List<CatalogueTitle>> NamedAsync(IReadOnlyList<(TmdbListedTitle Title, string Kind, string? Role)> listed, CancellationToken ct)
    {
        var named = new CatalogueTitle?[listed.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, listed.Count),
            new ParallelOptions { MaxDegreeOfParallelism = LookupsAtOnce, CancellationToken = ct },
            async (i, token) =>
            {
                var (title, kind, role) = listed[i];
                var ids = await GetAsync($"{Space(kind)}/{title.Id}/external_ids", TmdbJsonContext.Default.TmdbExternalIds, token);
                if (ImdbId.ToTag(ImdbId.ToNumber(ids?.ImdbId)) is not { } tag) return;
                named[i] = new CatalogueTitle
                {
                    TmdbId = title.Id,
                    ImdbId = tag,
                    Kind = kind,
                    Title = (title.Title ?? title.Name ?? tag).Trim(),
                    Released = Day(title.ReleaseDate ?? title.FirstAirDate),
                    Rating = title.VoteCount is > 0 && title.VoteAverage is > 0 ? Math.Round(title.VoteAverage.Value, 1) : null,
                    Votes = title.VoteCount ?? 0,
                    Overview = string.IsNullOrWhiteSpace(title.Overview) ? null : title.Overview.Trim(),
                    PosterPath = string.IsNullOrEmpty(title.PosterPath) ? null : title.PosterPath,
                    Role = role,
                };
            });
        return named.OfType<CatalogueTitle>().ToList();
    }

    /// <summary>The database's path segment for a kind: <c>movie</c> for a film, <c>tv</c> for a show.</summary>
    private static string? Space(string kind) => kind switch { "film" => "movie", "show" => "tv", _ => null };

    /// <summary>The kind a credit's <c>media_type</c> names.</summary>
    private static string? KindOf(string? mediaType) => mediaType switch { "movie" => "film", "tv" => "show", _ => null };

    private static DateOnly? Day(string? date) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var day) ? day : null;

    /// <summary>The show's description, from what the database answered.</summary>
    public static ShowDetails ReadShow(TmdbTv tv, string imdbId, TmdbOptions settings)
    {
        static string? Image(string? path) => string.IsNullOrEmpty(path) ? null : path;
        static int? YearOf(string? date) =>
            date is { Length: >= 4 } && int.TryParse(date[..4], out var year) ? year : null;

        return new ShowDetails
        {
            TmdbId = tv.Id,
            ImdbId = imdbId,
            Title = tv.Name ?? tv.OriginalName ?? imdbId,
            OriginalTitle = tv.OriginalName is { Length: > 0 } original && original != tv.Name ? original : null,
            FirstYear = YearOf(tv.FirstAirDate),
            LastYear = YearOf(tv.LastAirDate),
            Status = string.IsNullOrWhiteSpace(tv.Status) ? null : tv.Status.Trim(),
            InProduction = tv.InProduction ?? false,
            Certification = tv.ContentRatings?.Results?
                .FirstOrDefault(r => string.Equals(r.Country, settings.Region, StringComparison.OrdinalIgnoreCase)
                                     && !string.IsNullOrWhiteSpace(r.Rating))?.Rating?.Trim(),
            Genres = Names(tv.Genres?.Select(g => g.Name)),
            Overview = string.IsNullOrWhiteSpace(tv.Overview) ? null : tv.Overview.Trim(),
            Tagline = string.IsNullOrWhiteSpace(tv.Tagline) ? null : tv.Tagline.Trim(),
            Keywords = Names((tv.Keywords?.Results ?? tv.Keywords?.Keywords)?.Select(k => k.Name)),
            Rating = tv.VoteCount is > 0 && tv.VoteAverage is > 0 ? Math.Round(tv.VoteAverage.Value, 1) : null,
            Votes = tv.VoteCount ?? 0,
            Creators = Names(tv.CreatedBy?.Select(c => c.Name)),
            PersonIds = PersonIds(tv.CreatedBy?.Select(c => (c.Id, c.Name))),
            // A show's billing across its seasons: a role is the one the actor played most.
            Cast = (tv.AggregateCredits?.Cast ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Order ?? int.MaxValue)
                .Take(CastShown)
                .Select(c => new CastMember
                {
                    TmdbId = c.Id > 0 ? c.Id : null,
                    Name = c.Name!.Trim(),
                    Character = c.Roles?
                        .Where(r => !string.IsNullOrWhiteSpace(r.Character))
                        .OrderByDescending(r => r.EpisodeCount ?? 0)
                        .FirstOrDefault()?.Character?.Trim(),
                    ProfilePath = Image(c.ProfilePath),
                })
                .ToList(),
            OriginalLanguage = string.IsNullOrEmpty(tv.OriginalLanguage) ? null : tv.OriginalLanguage,
            Languages = Names(tv.SpokenLanguages?.Select(l => l.EnglishName ?? l.Name)),
            Countries = Names(tv.ProductionCountries?.Select(c => c.Name)),
            Networks = Names(tv.Networks?.Select(n => n.Name)),
            Seasons = (tv.Seasons ?? [])
                .OrderBy(s => s.SeasonNumber)
                .Select(s => new ShowSeason
                {
                    Number = s.SeasonNumber,
                    Name = string.IsNullOrWhiteSpace(s.Name) ? null : s.Name.Trim(),
                    EpisodeCount = s.EpisodeCount ?? 0,
                    AirDate = string.IsNullOrEmpty(s.AirDate) ? null : s.AirDate,
                    PosterPath = Image(s.PosterPath),
                })
                .ToList(),
            NextEpisodeToAir = Airing(tv.NextEpisodeToAir),
            LastEpisodeToAir = Airing(tv.LastEpisodeToAir),
            PosterPath = Image(tv.PosterPath),
            BackdropPath = Image(tv.BackdropPath),
            KeywordTags = Tags(tv.Keywords),
            Trailers = Trailers(tv.Videos, settings),
            Providers = ProvidersIn(tv.WatchProviders, settings.Region),
            Recommendations = Recommended(tv.Recommendations, "show", tv.Id),
            Reviews = ReviewsOf(tv.Reviews),
            ReviewCount = tv.Reviews?.TotalResults ?? 0,
            LogoPath = LogoOf(tv.Images, settings),
        };
    }

    /// <summary>An episode a show's description names, or null when it names none or one without a number.</summary>
    private static AiringEpisode? Airing(TmdbEpisode? episode) =>
        episode is { EpisodeNumber: > 0 }
            ? new AiringEpisode
            {
                Season = episode.SeasonNumber,
                Number = episode.EpisodeNumber,
                Name = string.IsNullOrWhiteSpace(episode.Name) ? null : episode.Name.Trim(),
                AirDate = string.IsNullOrEmpty(episode.AirDate) ? null : episode.AirDate,
            }
            : null;

    /// <summary>A season's description, from what the database answered.</summary>
    public static SeasonDetails ReadSeason(TmdbTvSeason season) => new()
    {
        Number = season.SeasonNumber,
        Name = string.IsNullOrWhiteSpace(season.Name) ? null : season.Name.Trim(),
        Overview = string.IsNullOrWhiteSpace(season.Overview) ? null : season.Overview.Trim(),
        AirDate = string.IsNullOrEmpty(season.AirDate) ? null : season.AirDate,
        PosterPath = string.IsNullOrEmpty(season.PosterPath) ? null : season.PosterPath,
        Episodes = (season.Episodes ?? [])
            .OrderBy(e => e.EpisodeNumber)
            .Select(e => new EpisodeDetails
            {
                Season = e.SeasonNumber,
                Number = e.EpisodeNumber,
                Name = string.IsNullOrWhiteSpace(e.Name) ? null : e.Name.Trim(),
                Overview = string.IsNullOrWhiteSpace(e.Overview) ? null : e.Overview.Trim(),
                AirDate = string.IsNullOrEmpty(e.AirDate) ? null : e.AirDate,
                RuntimeMinutes = e.Runtime is > 0 ? e.Runtime : null,
                Rating = e.VoteCount is > 0 && e.VoteAverage is > 0 ? Math.Round(e.VoteAverage.Value, 1) : null,
                Votes = e.VoteCount ?? 0,
                StillPath = string.IsNullOrEmpty(e.StillPath) ? null : e.StillPath,
            })
            .ToList(),
    };

    /// <summary>
    /// One image from the database's store, at a width it serves (<c>w185</c>, <c>w500</c>,
    /// <c>w1280</c>), or null when it has no such image. The store is public and takes no token.
    /// Throws <see cref="HttpRequestException"/> when it cannot be reached.
    /// </summary>
    public async Task<byte[]?> ImageAsync(string path, string size, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/') || path.Contains("..")) return null;
        using var response = await http.GetAsync($"{options.Value.ImageBaseUrl.TrimEnd('/')}/{size}{path}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>The description, from what the database answered.</summary>
    public static FilmDetails Read(TmdbMovie movie, string imdbId, TmdbOptions settings)
    {
        var crew = movie.Credits?.Crew ?? [];
        var directors = crew.Where(c => string.Equals(c.Job, "Director", StringComparison.OrdinalIgnoreCase)).ToList();
        var writers = crew.Where(c => string.Equals(c.Department, "Writing", StringComparison.OrdinalIgnoreCase)
                                      && c.Job is { } job && WritingJobs.Contains(job)).ToList();
        static string? Image(string? path) => string.IsNullOrEmpty(path) ? null : path;

        return new FilmDetails
        {
            TmdbId = movie.Id,
            ImdbId = imdbId,
            Title = movie.Title ?? movie.OriginalTitle ?? imdbId,
            OriginalTitle = movie.OriginalTitle is { Length: > 0 } original && original != movie.Title ? original : null,
            Year = movie.ReleaseDate is { Length: >= 4 } date && int.TryParse(date[..4], out var year) ? year : null,
            ReleaseDate = string.IsNullOrEmpty(movie.ReleaseDate) ? null : movie.ReleaseDate,
            RuntimeMinutes = movie.Runtime is > 0 ? movie.Runtime : null,
            Certification = CertificationIn(movie.ReleaseDates, settings.Region),
            Genres = Names(movie.Genres?.Select(g => g.Name)),
            Overview = string.IsNullOrWhiteSpace(movie.Overview) ? null : movie.Overview.Trim(),
            Tagline = string.IsNullOrWhiteSpace(movie.Tagline) ? null : movie.Tagline.Trim(),
            Keywords = Names((movie.Keywords?.Keywords ?? movie.Keywords?.Results)?.Select(k => k.Name)),
            // A rating with no votes behind it is the database's placeholder, not anybody's opinion.
            Rating = movie.VoteCount is > 0 && movie.VoteAverage is > 0 ? Math.Round(movie.VoteAverage.Value, 1) : null,
            Votes = movie.VoteCount ?? 0,
            Directors = Names(directors.Select(c => c.Name)),
            Writers = Names(writers.Select(c => c.Name)),
            PersonIds = PersonIds(directors.Concat(writers).Select(c => (c.Id, c.Name))),
            Cast = (movie.Credits?.Cast ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Order ?? int.MaxValue)
                .Take(CastShown)
                .Select(c => new CastMember
                {
                    TmdbId = c.Id > 0 ? c.Id : null,
                    Name = c.Name!.Trim(),
                    Character = string.IsNullOrWhiteSpace(c.Character) ? null : c.Character.Trim(),
                    ProfilePath = Image(c.ProfilePath),
                })
                .ToList(),
            OriginalLanguage = string.IsNullOrEmpty(movie.OriginalLanguage) ? null : movie.OriginalLanguage,
            Languages = Names(movie.SpokenLanguages?.Select(l => l.EnglishName ?? l.Name)),
            Countries = Names(movie.ProductionCountries?.Select(c => c.Name)),
            PosterPath = Image(movie.PosterPath),
            BackdropPath = Image(movie.BackdropPath),
            KeywordTags = Tags(movie.Keywords),
            Trailers = Trailers(movie.Videos, settings),
            Providers = ProvidersIn(movie.WatchProviders, settings.Region),
            Recommendations = Recommended(movie.Recommendations, "film", movie.Id),
            Reviews = ReviewsOf(movie.Reviews),
            ReviewCount = movie.Reviews?.TotalResults ?? 0,
            LogoPath = LogoOf(movie.Images, settings),
            Budget = movie.Budget is > 0 ? movie.Budget : null,
            Revenue = movie.Revenue is > 0 ? movie.Revenue : null,
        };
    }

    /// <summary>
    /// The YouTube trailers and teasers, official before unofficial, a trailer before a teaser, the
    /// configured language before the others, and the earliest published first among equals.
    /// </summary>
    private static List<Trailer> Trailers(TmdbVideos? videos, TmdbOptions settings)
    {
        var language = LanguageCode(settings);
        return (videos?.Results ?? [])
            .Where(v => v.Site == "YouTube" && v.Type is "Trailer" or "Teaser" && !string.IsNullOrWhiteSpace(v.Key) && !string.IsNullOrWhiteSpace(v.Name))
            .OrderByDescending(v => v.Official == true)
            .ThenBy(v => v.Type == "Trailer" ? 0 : 1)
            .ThenBy(v => v.Language == language ? 0 : 1)
            .ThenBy(v => v.PublishedAt ?? "", StringComparer.Ordinal)
            .Take(TrailersKept)
            .Select(v => new Trailer
            {
                Key = v.Key!.Trim(),
                Name = v.Name!.Trim(),
                Kind = v.Type!,
                Official = v.Official == true,
                Language = string.IsNullOrEmpty(v.Language) ? null : v.Language,
            })
            .ToList();
    }

    /// <summary>Where a title is watched in <paramref name="region"/>, each way ordered as the database orders the providers there; null when the database lists nothing for it.</summary>
    private static WatchProviders? ProvidersIn(TmdbWatchProviders? providers, string region)
    {
        if (providers?.Results is not { } regions
            || regions.FirstOrDefault(r => string.Equals(r.Key, region, StringComparison.OrdinalIgnoreCase)) is not { Value: { } found }) return null;

        static List<WatchProvider> Of(IReadOnlyList<TmdbProvider>? list) =>
            (list ?? [])
                .Where(p => p.Id > 0 && !string.IsNullOrWhiteSpace(p.Name))
                .OrderBy(p => p.Priority ?? int.MaxValue)
                .DistinctBy(p => p.Id)
                .Select(p => new WatchProvider { ProviderId = p.Id, Name = p.Name!.Trim(), LogoPath = string.IsNullOrEmpty(p.LogoPath) ? null : p.LogoPath })
                .ToList();

        var result = new WatchProviders
        {
            Region = region.ToUpperInvariant(),
            Link = string.IsNullOrWhiteSpace(found.Link) ? null : found.Link,
            Flatrate = Of(found.Flatrate),
            Rent = Of(found.Rent),
            Buy = Of(found.Buy),
            Free = Of(found.Free),
            Ads = Of(found.Ads),
        };
        return result.All.Any() ? result : null;
    }

    /// <summary>The database's first page of recommendations, without the title itself.</summary>
    private static List<ListedTitle> Recommended(TmdbTitlePage? page, string kind, int self) =>
        (page?.Results ?? [])
            .Select(r => Listed(r, kind))
            .OfType<ListedTitle>()
            .Where(t => t.TmdbId != self)
            .DistinctBy(t => t.TmdbId)
            .Take(RecommendationsKept)
            .ToList();

    /// <summary>The first reviews with text, each cut to its beginning on a word.</summary>
    private static List<Review> ReviewsOf(TmdbReviews? reviews) =>
        (reviews?.Results ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Content))
            .Take(ReviewsKept)
            .Select(r => new Review
            {
                Author = string.IsNullOrWhiteSpace(r.Author) ? "Anonymous" : r.Author.Trim(),
                Rating = r.AuthorDetails?.Rating is > 0 and var rating ? rating : null,
                Excerpt = Excerpt(r.Content!),
                Url = string.IsNullOrWhiteSpace(r.Url) ? null : r.Url,
            })
            .ToList();

    private static string Excerpt(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (line.Length <= ExcerptLength) return line;
        var cut = line.LastIndexOf(' ', ExcerptLength - 1);
        return line[..(cut > ExcerptLength / 2 ? cut : ExcerptLength)].TrimEnd(',', ';', ':', '.', ' ') + "…";
    }

    /// <summary>
    /// The title's logo: a PNG in the configured language, else English, else with no text at all,
    /// the best voted and widest among those.
    /// </summary>
    private static string? LogoOf(TmdbImages? images, TmdbOptions settings)
    {
        var language = LanguageCode(settings);
        int Rank(TmdbImage logo) => logo.Language == language ? 0 : logo.Language == "en" ? 1 : string.IsNullOrEmpty(logo.Language) ? 2 : 3;
        return (images?.Logos ?? [])
            .Where(l => l.FilePath is { } path && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && Rank(l) < 3)
            .OrderBy(Rank)
            .ThenByDescending(l => l.VoteAverage ?? 0)
            .ThenByDescending(l => l.Width ?? 0)
            .FirstOrDefault()?.FilePath;
    }

    /// <summary>A title's keywords by id and name.</summary>
    private static List<KeywordTag> Tags(TmdbKeywords? keywords) =>
        ((keywords?.Keywords ?? keywords?.Results) ?? [])
            .Where(k => k.Id > 0 && !string.IsNullOrWhiteSpace(k.Name))
            .DistinctBy(k => k.Id)
            .Select(k => new KeywordTag { Id = k.Id, Name = k.Name!.Trim() })
            .ToList();

    /// <summary>
    /// The certificate in one country. A theatrical release's certificate is preferred, because a
    /// home release is often recertified and the cinema's is the one people know.
    /// </summary>
    private static string? CertificationIn(TmdbReleaseDates? dates, string region)
    {
        var releases = dates?.Results?
            .FirstOrDefault(r => string.Equals(r.Country, region, StringComparison.OrdinalIgnoreCase))?
            .Releases?
            .Where(r => !string.IsNullOrWhiteSpace(r.Certification))
            .ToList();
        if (releases is not { Count: > 0 }) return null;
        return (releases.FirstOrDefault(r => r.Type == 3) ?? releases[0]).Certification!.Trim();
    }

    /// <summary>Each named person's id, by their trimmed name; the first id a name is listed with.</summary>
    private static Dictionary<string, int> PersonIds(IEnumerable<(int Id, string? Name)>? people)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, name) in people ?? [])
            if (id > 0 && !string.IsNullOrWhiteSpace(name)) ids.TryAdd(name.Trim(), id);
        return ids;
    }

    /// <summary>Distinct, trimmed, in the order given.</summary>
    private static List<string> Names(IEnumerable<string?>? names) =>
        names is null ? [] : names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var settings = options.Value;
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{settings.BaseUrl.TrimEnd('/')}/{path}")
            {
                Version = http.DefaultRequestVersion,
                VersionPolicy = http.DefaultVersionPolicy,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ReadAccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return default;
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < RateLimitAttempts)
            {
                // The database says how long to wait; asking again sooner is what it is refusing.
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                logger.LogInformation("The film database asked for {Seconds} s before the next request.", wait.TotalSeconds);
                await Task.Delay(wait > MaxRateLimitWait ? MaxRateLimitWait : wait, ct);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync(type, ct);
        }
    }

    /// <summary>How many times a request is made when the database answers that too many have been.</summary>
    private const int RateLimitAttempts = 3;

    private static readonly TimeSpan MaxRateLimitWait = TimeSpan.FromSeconds(5);
}
