using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// A film found by its IMDb id and described from what the database sends: a trimmed copy of its
/// real answer for Heat, with the crew that is not a director or a writer left in on purpose.
/// </summary>
public class TmdbClientTests
{
    private const string Find = """{ "movie_results": [ { "id": 949 } ], "tv_results": [] }""";

    private const string Heat = """
        {
          "id": 949, "imdb_id": "tt0113277", "title": "Heat", "original_title": "Heat",
          "original_language": "en", "overview": "Obsessive master thief Neil McCauley leads a top-notch crew.",
          "tagline": "A Los Angeles crime saga", "release_date": "1995-12-15", "runtime": 170,
          "vote_average": 7.945, "vote_count": 8740,
          "poster_path": "/umSVjVdbVwtx5ryCA2QXL44Durm.jpg", "backdrop_path": "/rfEXNlql4CafRmqgTvVHGaZVCsj.jpg",
          "genres": [ { "id": 80, "name": "Crime" }, { "id": 18, "name": "Drama" }, { "id": 28, "name": "Action" } ],
          "spoken_languages": [ { "english_name": "English", "iso_639_1": "en", "name": "English" },
                                { "english_name": "Spanish", "iso_639_1": "es", "name": "Español" } ],
          "production_countries": [ { "iso_3166_1": "US", "name": "United States of America" } ],
          "credits": {
            "cast": [
              { "name": "Robert De Niro", "character": "Neil McCauley", "order": 1, "profile_path": "/cT8htcckIuyI1Lqwt1CvD02ynTh.jpg" },
              { "name": "Al Pacino", "character": "Lt. Vincent Hanna", "order": 0, "profile_path": null }
            ],
            "crew": [
              { "name": "Michael Mann", "job": "Director", "department": "Directing" },
              { "name": "Ami Canaan Mann", "job": "Second Unit Director", "department": "Directing" },
              { "name": "Michael Mann", "job": "Writer", "department": "Writing" },
              { "name": "Cate Hardman", "job": "Script Supervisor", "department": "Writing" }
            ]
          },
          "release_dates": { "results": [
            { "iso_3166_1": "GB", "release_dates": [ { "certification": "18", "type": 3 } ] },
            { "iso_3166_1": "US", "release_dates": [ { "certification": "", "type": 1 }, { "certification": "R", "type": 3 } ] }
          ] }
        }
        """;

    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> answer, string token = "a-token") =>
        new(new HttpClient(new Handler(answer)), Options.Create(new TmdbOptions { ReadAccessToken = token }),
            NullLogger<TmdbClient>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task A_film_is_found_by_its_imdb_id_and_described()
    {
        var asked = new List<string>();
        var film = await Client(request =>
        {
            asked.Add(request.RequestUri!.PathAndQuery);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Json(request.RequestUri.AbsolutePath.Contains("/find/") ? Find : Heat);
        }).DetailsAsync("tt0113277", CancellationToken.None);

        Assert.NotNull(film);
        Assert.StartsWith("/3/find/tt0113277?external_source=imdb_id", asked[0]);
        Assert.StartsWith("/3/movie/949?append_to_response=credits,release_dates", asked[1]);

        Assert.Equal(949, film.TmdbId);
        Assert.Equal("tt0113277", film.ImdbId);
        Assert.Equal("Heat", film.Title);
        Assert.Null(film.OriginalTitle);
        Assert.Equal(1995, film.Year);
        Assert.Equal(170, film.RuntimeMinutes);
        Assert.Equal(7.9, film.Rating);
        Assert.Equal(8740, film.Votes);
        Assert.Equal(["Crime", "Drama", "Action"], film.Genres);
        Assert.Equal(["Michael Mann"], film.Directors);
        // A script supervisor is in the Writing department and wrote nothing.
        Assert.Equal(["Michael Mann"], film.Writers);
        Assert.Equal(["English", "Spanish"], film.Languages);
        Assert.Equal(["United States of America"], film.Countries);
        Assert.Equal("/umSVjVdbVwtx5ryCA2QXL44Durm.jpg", film.PosterPath);
    }

    [Fact]
    public async Task The_cast_is_in_billing_order_and_a_missing_portrait_is_none()
    {
        var film = await Client(r => Json(r.RequestUri!.AbsolutePath.Contains("/find/") ? Find : Heat))
            .DetailsAsync("tt0113277", CancellationToken.None);

        Assert.Equal("Al Pacino", film!.Cast[0].Name);
        Assert.Equal("Lt. Vincent Hanna", film.Cast[0].Character);
        Assert.Null(film.Cast[0].ProfilePath);
        Assert.Equal("/cT8htcckIuyI1Lqwt1CvD02ynTh.jpg", film.Cast[1].ProfilePath);
        Assert.Equal("Robert De Niro", film.Cast[1].Name);
    }

    [Fact]
    public async Task The_certificate_is_the_configured_country_s_cinema_one()
    {
        var film = await Client(r => Json(r.RequestUri!.AbsolutePath.Contains("/find/") ? Find : Heat))
            .DetailsAsync("tt0113277", CancellationToken.None);

        Assert.Equal("R", film!.Certification);
    }

    [Fact]
    public async Task An_id_the_database_does_not_hold_is_no_film()
    {
        var film = await Client(_ => Json("""{ "movie_results": [] }""")).DetailsAsync("tt9999999", CancellationToken.None);

        Assert.Null(film);
    }

    [Fact]
    public async Task Without_a_token_nothing_is_asked()
    {
        var film = await Client(_ => throw new InvalidOperationException("asked"), token: "")
            .DetailsAsync("tt0113277", CancellationToken.None);

        Assert.Null(film);
    }

    [Fact]
    public async Task A_refused_token_is_an_error_rather_than_no_film()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(_ => Json("""{ "status_code": 7 }""", HttpStatusCode.Unauthorized))
                .DetailsAsync("tt0113277", CancellationToken.None));
    }

    [Fact]
    public async Task An_image_is_fetched_from_the_store_at_the_width_asked_for()
    {
        string? asked = null;
        var bytes = await Client(r =>
        {
            asked = r.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }).ImageAsync("/umSVjVdbVwtx5ryCA2QXL44Durm.jpg", "w500", CancellationToken.None);

        Assert.Equal("https://image.tmdb.org/t/p/w500/umSVjVdbVwtx5ryCA2QXL44Durm.jpg", asked);
        Assert.Equal([1, 2, 3], bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-slash.jpg")]
    [InlineData("/../../etc/passwd")]
    public async Task A_path_that_is_not_the_store_s_is_not_asked_for(string path)
    {
        var bytes = await Client(_ => throw new InvalidOperationException("asked"))
            .ImageAsync(path, "w500", CancellationToken.None);

        Assert.Null(bytes);
    }

    [Fact]
    public async Task The_week_s_trending_films_are_named_by_imdb_id_in_the_database_s_order()
    {
        const string Trending = """
            { "page": 1, "results": [
              { "id": 949, "title": "Heat", "release_date": "1995-12-15", "vote_average": 7.945, "vote_count": 8740, "poster_path": "/heat.jpg", "genre_ids": [ 80, 18 ], "original_language": "en" },
              { "id": 5, "title": "Unlisted", "release_date": "2026-01-01" },
              { "id": 6, "title": "Grown-up", "adult": true },
              { "id": 7, "title": "Upcoming", "release_date": "", "vote_average": 0, "vote_count": 0, "poster_path": null }
            ] }
            """;
        var asked = new List<string>();
        var films = await Client(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            asked.Add(path);
            return path switch
            {
                "/3/trending/movie/week" => Json(Trending),
                "/3/movie/949/external_ids" => Json("""{ "imdb_id": "tt0113277" }"""),
                "/3/movie/5/external_ids" => Json("""{ "imdb_id": null }"""),
                "/3/movie/7/external_ids" => Json("""{ "imdb_id": "tt9999999" }"""),
                _ => Json("{}", HttpStatusCode.NotFound),
            };
        }).TrendingFilmsAsync(CancellationToken.None);

        Assert.Equal(["tt0113277", "tt9999999"], films.Select(f => f.ImdbId));
        Assert.DoesNotContain("/3/movie/6/external_ids", asked);

        var heat = films[0];
        Assert.Equal(("Heat", 1995, 7.9, 8740, "/heat.jpg"), (heat.Title, heat.Year, heat.Rating, heat.Votes, heat.PosterPath));
        Assert.Equal([80, 18], heat.GenreIds);
        Assert.Equal("en", heat.OriginalLanguage);
        var upcoming = films[1];
        Assert.Equal(((int?)null, (double?)null, (string?)null), (upcoming.Year, upcoming.Rating, upcoming.PosterPath));
    }

    [Fact]
    public async Task A_show_s_trending_entry_is_read_from_its_name_and_first_air_date()
    {
        var shows = await Client(request => request.RequestUri!.AbsolutePath switch
        {
            "/3/trending/tv/week" => Json("""{ "results": [ { "id": 42, "name": "Slow Horses", "first_air_date": "2022-04-01" } ] }"""),
            "/3/tv/42/external_ids" => Json("""{ "imdb_id": "tt5875444" }"""),
            _ => Json("{}", HttpStatusCode.NotFound),
        }).TrendingShowsAsync(CancellationToken.None);

        Assert.Equal(("tt5875444", "Slow Horses", (int?)2022), (shows[0].ImdbId, shows[0].Title, shows[0].Year));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }
}
