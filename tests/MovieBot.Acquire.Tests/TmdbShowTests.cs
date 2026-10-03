using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// A show found by its IMDb id and described from what the database sends: a trimmed copy of its
/// real answers for Severance.
/// </summary>
public class TmdbShowTests
{
    private const string Find = """{ "movie_results": [], "tv_results": [ { "id": 95396 } ] }""";

    private const string Show = """
        {
          "id": 95396, "name": "Severance", "original_name": "Severance", "original_language": "en",
          "overview": "Mark leads a team of office workers.", "tagline": "",
          "first_air_date": "2022-02-17", "last_air_date": "2025-03-20", "status": "Returning Series",
          "in_production": true, "vote_average": 8.4, "vote_count": 2900, "episode_run_time": [],
          "poster_path": "/poster.jpg", "backdrop_path": "/backdrop.jpg",
          "genres": [ { "name": "Drama" }, { "name": "Mystery" }, { "name": "Sci-Fi & Fantasy" } ],
          "created_by": [ { "name": "Dan Erickson" } ],
          "networks": [ { "name": "Apple TV+" } ],
          "spoken_languages": [ { "english_name": "English", "name": "English" } ],
          "production_countries": [ { "name": "United States of America" } ],
          "seasons": [
            { "season_number": 2, "name": "Season 2", "episode_count": 10, "air_date": "2025-01-16", "poster_path": "/s2.jpg" },
            { "season_number": 0, "name": "Specials", "episode_count": 1, "air_date": "2021-12-15", "poster_path": null },
            { "season_number": 1, "name": "Season 1", "episode_count": 9, "air_date": "2022-02-17", "poster_path": "/s1.jpg" }
          ],
          "aggregate_credits": { "cast": [
            { "name": "Britt Lower", "order": 1, "profile_path": "/britt.jpg",
              "roles": [ { "character": "Helly R.", "episode_count": 19 }, { "character": "Helena Eagan", "episode_count": 6 } ] },
            { "name": "Adam Scott", "order": 0, "profile_path": "/adam.jpg", "roles": [ { "character": "Mark Scout", "episode_count": 19 } ] }
          ] },
          "content_ratings": { "results": [ { "iso_3166_1": "DE", "rating": "12" }, { "iso_3166_1": "US", "rating": "TV-MA" } ] }
        }
        """;

    private const string Season = """
        {
          "season_number": 2, "name": "Season 2", "overview": "", "air_date": "2025-01-16", "poster_path": "/s2.jpg",
          "episodes": [
            { "season_number": 2, "episode_number": 2, "name": "Goodbye, Mrs. Selvig", "air_date": "2025-01-24",
              "runtime": 46, "vote_average": 7.9, "vote_count": 120, "still_path": "/e2.jpg" },
            { "season_number": 2, "episode_number": 1, "name": "Hello, Ms. Cobel", "air_date": "2025-01-16",
              "runtime": 54, "vote_average": 8.0, "vote_count": 134, "still_path": "/e1.jpg" },
            { "season_number": 2, "episode_number": 3, "name": "Who Is Alive?", "air_date": null,
              "runtime": null, "vote_average": 0.0, "vote_count": 0, "still_path": null }
          ]
        }
        """;

    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new HttpClient(new Handler(answer)), Options.Create(new TmdbOptions { ReadAccessToken = "a-token" }),
            NullLogger<TmdbClient>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task A_show_is_found_by_its_imdb_id_and_described()
    {
        var asked = new List<string>();
        var show = await Client(r =>
        {
            asked.Add(r.RequestUri!.PathAndQuery);
            return Json(r.RequestUri.AbsolutePath.Contains("/find/") ? Find : Show);
        }).ShowAsync("tt11280740", CancellationToken.None);

        Assert.NotNull(show);
        Assert.StartsWith("/3/tv/95396?append_to_response=aggregate_credits,content_ratings", asked[1]);
        Assert.Equal(95396, show.TmdbId);
        Assert.Equal("tt11280740", show.ImdbId);
        Assert.Equal("Severance", show.Title);
        Assert.Null(show.OriginalTitle);
        Assert.Equal(2022, show.FirstYear);
        Assert.Equal(2025, show.LastYear);
        Assert.Equal("Returning Series", show.Status);
        Assert.True(show.InProduction);
        Assert.Equal("TV-MA", show.Certification);
        Assert.Equal(["Dan Erickson"], show.Creators);
        Assert.Equal(["Apple TV+"], show.Networks);
        Assert.Null(show.Tagline);
        Assert.Equal([0, 1, 2], show.Seasons.Select(s => s.Number));
        Assert.Equal(10, show.Seasons[2].EpisodeCount);
    }

    [Fact]
    public async Task The_cast_is_billed_in_order_with_the_role_each_played_most()
    {
        var show = await Client(r => Json(r.RequestUri!.AbsolutePath.Contains("/find/") ? Find : Show))
            .ShowAsync("tt11280740", CancellationToken.None);

        Assert.Equal("Adam Scott", show!.Cast[0].Name);
        Assert.Equal("Helly R.", show.Cast[1].Character);
    }

    [Fact]
    public async Task A_film_s_id_is_no_show()
    {
        var show = await Client(_ => Json("""{ "movie_results": [ { "id": 949 } ], "tv_results": [] }"""))
            .ShowAsync("tt0113277", CancellationToken.None);

        Assert.Null(show);
    }

    [Fact]
    public async Task A_season_lists_its_episodes_in_order()
    {
        string? asked = null;
        var season = await Client(r => { asked = r.RequestUri!.PathAndQuery; return Json(Season); })
            .SeasonAsync(95396, 2, CancellationToken.None);

        Assert.StartsWith("/3/tv/95396/season/2?", asked);
        Assert.Equal([1, 2, 3], season!.Episodes.Select(e => e.Number));
        Assert.Equal("Hello, Ms. Cobel", season.Episodes[0].Name);
        Assert.Equal(54, season.Episodes[0].RuntimeMinutes);
        Assert.Equal(8.0, season.Episodes[0].Rating);
        Assert.Equal("/e1.jpg", season.Episodes[0].StillPath);

        // Not aired yet: no runtime, no still, and no votes behind its zero.
        Assert.Null(season.Episodes[2].RuntimeMinutes);
        Assert.Null(season.Episodes[2].Rating);
        Assert.Null(season.Episodes[2].StillPath);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }
}
