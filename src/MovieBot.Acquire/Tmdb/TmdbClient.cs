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
public sealed class TmdbClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
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
            $"movie/{results[0].Id}?append_to_response=credits,release_dates&language={Uri.EscapeDataString(settings.Language)}",
            TmdbJsonContext.Default.TmdbMovie, ct);
        return movie is null ? null : Read(movie, tag, settings);
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
            $"tv/{results[0].Id}?append_to_response=aggregate_credits,content_ratings&language={Uri.EscapeDataString(settings.Language)}",
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
            Rating = tv.VoteCount is > 0 && tv.VoteAverage is > 0 ? Math.Round(tv.VoteAverage.Value, 1) : null,
            Votes = tv.VoteCount ?? 0,
            Creators = Names(tv.CreatedBy?.Select(c => c.Name)),
            // A show's billing across its seasons: a role is the one the actor played most.
            Cast = (tv.AggregateCredits?.Cast ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Order ?? int.MaxValue)
                .Take(CastShown)
                .Select(c => new CastMember
                {
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
            // A rating with no votes behind it is the database's placeholder, not anybody's opinion.
            Rating = movie.VoteCount is > 0 && movie.VoteAverage is > 0 ? Math.Round(movie.VoteAverage.Value, 1) : null,
            Votes = movie.VoteCount ?? 0,
            Directors = Names(crew.Where(c => string.Equals(c.Job, "Director", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)),
            Writers = Names(crew.Where(c => string.Equals(c.Department, "Writing", StringComparison.OrdinalIgnoreCase)
                                            && c.Job is { } job && WritingJobs.Contains(job)).Select(c => c.Name)),
            Cast = (movie.Credits?.Cast ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Order ?? int.MaxValue)
                .Take(CastShown)
                .Select(c => new CastMember
                {
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{settings.BaseUrl.TrimEnd('/')}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ReadAccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(type, ct);
    }
}
