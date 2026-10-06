using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Browsing the database by a person and by a genre: what is listed, in what order, and every title
/// named by its IMDb id. The answers are trimmed copies of the database's own shapes.
/// </summary>
public class TmdbCatalogueTests
{
    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new HttpClient(new Handler(answer)), Options.Create(new TmdbOptions { ReadAccessToken = "a-token" }),
            NullLogger<TmdbClient>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string Crowe = """
        {
          "id": 934, "name": "Russell Crowe", "known_for_department": "Acting", "profile_path": "/crowe.jpg",
          "combined_credits": {
            "cast": [
              { "id": 98, "media_type": "movie", "title": "Gladiator", "release_date": "2000-05-04", "vote_average": 8.2, "vote_count": 21601, "popularity": 33.3, "poster_path": "/gladiator.jpg", "character": "Maximus", "overview": "A general becomes a gladiator." },
              { "id": 453, "media_type": "movie", "title": "A Beautiful Mind", "release_date": "2001-12-11", "vote_average": 7.9, "vote_count": 11000, "popularity": 9.1, "character": "John Nash" },
              { "id": 1, "media_type": "tv", "name": "A Late Show", "genre_ids": [35, 10767], "vote_count": 400, "popularity": 492.5, "character": "" },
              { "id": 2, "media_type": "movie", "title": "A Documentary", "release_date": "2010-01-01", "vote_count": 30, "character": "Self - Interviewee" },
              { "id": 3, "media_type": "movie", "title": "Announced", "vote_count": 0, "character": "Lead" },
              { "id": 204, "media_type": "movie", "title": "The Water Diviner", "release_date": "2014-12-26", "vote_average": 6.8, "vote_count": 1500, "character": "Joshua Connor" },
              { "id": 5, "media_type": "movie", "title": "Grown-up", "adult": true, "vote_count": 99999 },
              { "id": 6, "media_type": "movie", "title": "Unlisted", "release_date": "2005-01-01", "vote_count": 10 }
            ],
            "crew": [
              { "id": 204, "media_type": "movie", "title": "The Water Diviner", "release_date": "2014-12-26", "vote_count": 1500, "job": "Director" },
              { "id": 98, "media_type": "movie", "title": "Gladiator", "vote_count": 21601, "job": "Thanks" }
            ]
          }
        }
        """;

    private static HttpResponseMessage CroweAnswer(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/3/person/934" => Json(Crowe),
        "/3/movie/98/external_ids" => Json("""{ "imdb_id": "tt0172495" }"""),
        "/3/movie/453/external_ids" => Json("""{ "imdb_id": "tt0268978" }"""),
        "/3/movie/204/external_ids" => Json("""{ "imdb_id": "tt3007512" }"""),
        "/3/movie/6/external_ids" => Json("""{ "imdb_id": null }"""),
        _ => Json("{}", HttpStatusCode.NotFound),
    };

    [Fact]
    public async Task A_person_s_credits_are_their_work_best_known_first()
    {
        var asked = new List<string>();
        var person = await Client(request =>
        {
            lock (asked) asked.Add(request.RequestUri!.PathAndQuery);
            return CroweAnswer(request);
        }).PersonAsync(934, 40, CancellationToken.None);

        Assert.NotNull(person);
        Assert.Equal((934, "Russell Crowe", "Acting", "/crowe.jpg"), (person.TmdbId, person.Name, person.KnownFor, person.ProfilePath));
        Assert.StartsWith("/3/person/934?append_to_response=combined_credits", asked[0]);

        // The talk show, the part as himself, the announcement and the adult title are not his work,
        // and a title without an IMDb id leads nowhere.
        Assert.Equal(["tt0172495", "tt0268978", "tt3007512"], person.Titles.Select(t => t.ImdbId));
        Assert.DoesNotContain("/3/tv/1/external_ids", asked.Select(a => a.Split('?')[0]));

        var gladiator = person.Titles[0];
        Assert.Equal(("film", "Gladiator", 8.2, 21601, "/gladiator.jpg", "Maximus"),
            (gladiator.Kind, gladiator.Title, gladiator.Rating, gladiator.Votes, gladiator.PosterPath, gladiator.Role));
        Assert.Equal(new DateOnly(2000, 5, 4), gladiator.Released);
        Assert.Equal("A general becomes a gladiator.", gladiator.Overview);
    }

    [Fact]
    public async Task A_title_a_person_acted_in_and_directed_is_listed_once_with_both()
    {
        var person = await Client(CroweAnswer).PersonAsync(934, 40, CancellationToken.None);

        Assert.Equal("Joshua Connor · Director", person!.Titles.Single(t => t.TmdbId == 204).Role);
    }

    [Fact]
    public async Task A_person_s_credits_stop_at_the_count_asked_for()
    {
        var person = await Client(CroweAnswer).PersonAsync(934, 1, CancellationToken.None);

        Assert.Equal(["tt0172495"], person!.Titles.Select(t => t.ImdbId));
    }

    [Fact]
    public async Task Nobody_under_the_id_is_no_person()
    {
        Assert.Null(await Client(_ => Json("{}", HttpStatusCode.NotFound)).PersonAsync(1, 40, CancellationToken.None));
    }

    [Fact]
    public async Task A_genre_lists_its_most_popular_titles_across_pages()
    {
        var page = Enumerable.Range(1, 20)
            .Select(i => $$"""{ "id": {{i}}, "title": "Film {{i}}", "release_date": "2020-01-01", "vote_average": 7, "vote_count": 500 }""");
        var asked = new List<string>();
        var titles = await Client(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (asked) asked.Add(request.RequestUri.PathAndQuery);
            if (path == "/3/discover/movie")
                return Json(request.RequestUri.Query.Contains("page=1&")
                    ? $$"""{ "results": [ {{string.Join(",", page)}} ] }"""
                    : """{ "results": [ { "id": 21, "title": "Film 21", "vote_count": 500 }, { "id": 22, "title": "Film 22", "vote_count": 500 } ] }""");
            var id = int.Parse(path.Split('/')[3]);
            return Json($$"""{ "imdb_id": "tt{{id:0000000}}" }""");
        }).GenreAsync("film", 878, 21, CancellationToken.None);

        Assert.Equal(21, titles.Count);
        Assert.Equal("tt0000001", titles[0].ImdbId);
        Assert.Equal("tt0000021", titles[20].ImdbId);
        Assert.All(titles, t => Assert.Equal("film", t.Kind));
        var first = asked.First(a => a.StartsWith("/3/discover/movie", StringComparison.Ordinal));
        Assert.Contains("with_genres=878", first);
        Assert.Contains("sort_by=popularity.desc", first);
        Assert.Contains($"vote_count.gte={TmdbClient.GenreVotesAtLeast}", first);
        Assert.Equal(2, asked.Count(a => a.StartsWith("/3/discover/movie", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_genre_of_shows_leaves_out_talk_news_and_reality()
    {
        string? asked = null;
        var titles = await Client(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/3/discover/tv")
            {
                asked = request.RequestUri.PathAndQuery;
                return Json("""{ "results": [ { "id": 42, "name": "Slow Horses", "first_air_date": "2022-04-01", "vote_count": 900 } ] }""");
            }
            return Json("""{ "imdb_id": "tt5875444" }""");
        }).GenreAsync("show", 80, 40, CancellationToken.None);

        Assert.Contains("without_genres=10767", asked);
        Assert.Equal(("tt5875444", "show", "Slow Horses"), (titles[0].ImdbId, titles[0].Kind, titles[0].Title));
    }

    [Fact]
    public async Task The_genres_are_listed_per_kind()
    {
        var genres = await Client(request => request.RequestUri!.AbsolutePath == "/3/genre/movie/list"
            ? Json("""{ "genres": [ { "id": 878, "name": "Science Fiction" }, { "id": 18, "name": "Drama" } ] }""")
            : Json("{}", HttpStatusCode.NotFound)).GenresAsync("film", CancellationToken.None);

        Assert.Equal([(878, "Science Fiction"), (18, "Drama")], genres.Select(g => (g.Id, g.Name)));
        Assert.Empty(await Client(_ => throw new InvalidOperationException()).GenresAsync("episode", CancellationToken.None));
    }

    [Fact]
    public async Task A_cast_member_and_a_director_carry_their_person_id()
    {
        const string Find = """{ "movie_results": [ { "id": 949 } ] }""";
        const string Heat = """
            { "id": 949, "title": "Heat",
              "credits": {
                "cast": [ { "id": 1158, "name": "Al Pacino", "character": "Lt. Vincent Hanna", "order": 0 } ],
                "crew": [ { "id": 638, "name": "Michael Mann", "job": "Director", "department": "Directing" },
                          { "id": 638, "name": "Michael Mann", "job": "Writer", "department": "Writing" } ] } }
            """;
        var film = await Client(r => Json(r.RequestUri!.AbsolutePath.Contains("/find/") ? Find : Heat))
            .DetailsAsync("tt0113277", CancellationToken.None);

        Assert.Equal(1158, film!.Cast[0].TmdbId);
        Assert.Equal(638, film.PersonIds["Michael Mann"]);
        Assert.Single(film.PersonIds);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }
}
