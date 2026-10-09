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
            $"movie/{results[0].Id}?append_to_response=credits,release_dates,keywords&language={Uri.EscapeDataString(settings.Language)}",
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
            $"tv/{results[0].Id}?append_to_response=aggregate_credits,content_ratings,keywords&language={Uri.EscapeDataString(settings.Language)}",
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
    public async Task<IReadOnlyList<CatalogueTitle>> GenreAsync(string kind, int genreId, int count, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || Space(kind) is not { } space || genreId <= 0 || count <= 0) return [];

        var language = Uri.EscapeDataString(settings.Language);
        var without = kind == "show" ? "&without_genres=" + string.Join("%7C", UnscriptedShowGenres) : "";
        var listed = new List<(TmdbListedTitle Title, string Kind, string? Role)>();
        for (var page = 1; listed.Count < count && page <= (count + PageSize - 1) / PageSize; page++)
        {
            var answer = await GetAsync(
                $"discover/{space}?include_adult=false&language={language}&page={page}&sort_by=popularity.desc"
                + $"&vote_count.gte={GenreVotesAtLeast}&with_genres={genreId}{without}",
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
        };
    }

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
