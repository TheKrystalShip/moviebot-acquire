using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Finding rather than describing: a search by name, a person by name and everything they are
/// credited on, a discovery by what a title is like, the titles the database counts as similar, and
/// what names a title. The answers are trimmed copies of the database's own shapes.
/// </summary>
public class TmdbDiscoveryTests
{
    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new HttpClient(new Handler(answer)), Options.Create(new TmdbOptions { ReadAccessToken = "a-token" }),
            NullLogger<TmdbClient>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Decoded(HttpRequestMessage request) => Uri.UnescapeDataString(request.RequestUri!.PathAndQuery);

    [Fact]
    public async Task A_search_for_films_asks_for_films_and_keeps_the_databases_order()
    {
        string? asked = null;
        var found = await Client(request =>
        {
            asked = Decoded(request);
            return Json("""
                { "results": [
                  { "id": 1, "title": "Heat", "release_date": "1995-12-15", "vote_average": 7.94, "vote_count": 8740, "genre_ids": [80, 18], "original_language": "en", "popularity": 30.5, "poster_path": "/h.jpg", "overview": "A thief." },
                  { "id": 2, "title": "Grown-up", "adult": true },
                  { "id": 3, "title": "Heat Again", "vote_average": 0, "vote_count": 0 },
                  { "id": 4, "overview": "No name." } ] }
                """);
        }).SearchTitlesAsync("  heat & dust ", "film", 1, CancellationToken.None);

        Assert.StartsWith("/3/search/movie?", asked);
        Assert.Contains("query=heat & dust", asked);
        Assert.Contains("include_adult=false", asked);
        Assert.Equal(["Heat", "Heat Again"], found.Select(t => t.Title));
        var heat = found[0];
        Assert.Equal(("film", 7.9, 8740, new DateOnly(1995, 12, 15), "en", "/h.jpg"),
            (heat.Kind, heat.Rating, heat.Votes, heat.Released, heat.OriginalLanguage, heat.PosterPath));
        Assert.Equal([80, 18], heat.GenreIds);
        Assert.Null(found[1].Rating);
        Assert.Null(found[1].Released);
    }

    [Fact]
    public async Task A_search_for_both_kinds_names_each_by_what_the_database_says_it_is_and_leaves_people_out()
    {
        string? asked = null;
        var found = await Client(request =>
        {
            asked = request.RequestUri!.AbsolutePath;
            return Json("""
                { "results": [
                  { "id": 1, "media_type": "movie", "title": "Heat", "release_date": "1995-12-15" },
                  { "id": 2, "media_type": "tv", "name": "Heat Wave", "first_air_date": "2019-04-01" },
                  { "id": 3, "media_type": "person", "name": "Al Pacino" } ] }
                """);
        }).SearchTitlesAsync("heat", null, 1, CancellationToken.None);

        Assert.Equal("/3/search/multi", asked);
        Assert.Equal([("film", "Heat"), ("show", "Heat Wave")], found.Select(t => (t.Kind, t.Title)));
        Assert.Equal(new DateOnly(2019, 4, 1), found[1].Released);
    }

    [Fact]
    public async Task A_search_reads_further_pages_only_while_a_page_is_full()
    {
        var page = string.Join(",", Enumerable.Range(1, 20).Select(i => $$"""{ "id": {{i}}, "title": "T{{i}}" }"""));
        var pages = new List<string>();
        var found = await Client(request =>
        {
            pages.Add(request.RequestUri!.Query);
            return Json($$"""{ "results": [ {{page}} ] }""");
        }).SearchTitlesAsync("t", "film", 3, CancellationToken.None);

        Assert.Equal(60, found.Count);
        Assert.Equal(3, pages.Count);

        var short_ = new List<string>();
        await Client(request =>
        {
            short_.Add(request.RequestUri!.Query);
            return Json("""{ "results": [ { "id": 1, "title": "Only" } ] }""");
        }).SearchTitlesAsync("t", "film", 3, CancellationToken.None);
        Assert.Single(short_);
    }

    [Fact]
    public async Task A_name_search_for_a_person_lists_what_each_match_is_known_for()
    {
        var people = await Client(_ => Json("""
            { "results": [
              { "id": 1158, "name": "Al Pacino", "known_for_department": "Acting", "popularity": 50.5, "profile_path": "/p.jpg",
                "known_for": [ { "media_type": "movie", "title": "Heat", "release_date": "1995-12-15" },
                               { "media_type": "tv", "name": "Hunters", "first_air_date": "2020-02-21" },
                               { "media_type": "movie", "title": "Untitled" },
                               { "media_type": "movie", "title": "Fourth", "release_date": "2001-01-01" } ] },
              { "id": 0, "name": "Nobody" },
              { "id": 5, "name": " " } ] }
            """)).SearchPeopleAsync("al pacino", CancellationToken.None);

        var pacino = Assert.Single(people);
        Assert.Equal((1158, "Al Pacino", "Acting", "/p.jpg", 50.5), (pacino.TmdbId, pacino.Name, pacino.KnownFor, pacino.ProfilePath, pacino.Popularity));
        Assert.Equal(["Heat (1995)", "Hunters (2020)", "Untitled"], pacino.KnownForTitles);
    }

    [Fact]
    public async Task Keywords_and_languages_are_read_as_the_database_lists_them()
    {
        var client = Client(request => request.RequestUri!.AbsolutePath switch
        {
            "/3/search/keyword" => Json("""{ "results": [ { "id": 12999, "name": "time loop" }, { "id": 0, "name": "zero" } ] }"""),
            "/3/configuration/languages" => Json("""[ { "iso_639_1": "ko", "english_name": "Korean", "name": "한국어" }, { "iso_639_1": "", "english_name": "None" } ]"""),
            _ => Json("{}", HttpStatusCode.NotFound),
        });

        Assert.Equal([(12999, "time loop")], (await client.SearchKeywordsAsync("time loop", CancellationToken.None)).Select(k => (k.Id, k.Name)));
        Assert.Equal([("ko", "Korean")], (await client.LanguagesAsync(CancellationToken.None)).Select(l => (l.Code, l.Name)));
    }

    [Fact]
    public async Task A_discovery_of_films_asks_with_every_filter_in_a_fixed_order()
    {
        string? asked = null;
        var found = await Client(request =>
        {
            asked = Decoded(request);
            return Json("""{ "results": [ { "id": 7, "title": "Hollow Hill", "release_date": "2004-09-10", "vote_average": 6.8, "vote_count": 800, "genre_ids": [27] } ] }""");
        }).DiscoverAsync(new DiscoverQuery
        {
            Kind = "film",
            GenreIds = [27, 53],
            From = new DateOnly(2000, 1, 1),
            To = new DateOnly(2005, 12, 31),
            PersonIds = [8001, 9],
            KeywordIds = [12999],
            Language = "ko",
            MinRating = 6.5,
            MinVotes = 200,
            Sort = DiscoverSorts.Rating,
        }, 20, CancellationToken.None);

        Assert.Equal("Hollow Hill", Assert.Single(found).Title);
        Assert.Equal(
            "/3/discover/movie?include_adult=false&language=en-US&primary_release_date.gte=2000-01-01&primary_release_date.lte=2005-12-31"
            + "&sort_by=vote_count.desc&vote_average.gte=6.5&vote_count.gte=200&with_genres=27,53&with_keywords=12999"
            + "&with_original_language=ko&with_people=8001,9&page=1",
            asked);
    }

    [Fact]
    public async Task A_discovery_asks_for_any_of_several_and_for_runtimes_votes_certificates_companies_and_countries()
    {
        string? asked = null;
        await Client(request =>
        {
            asked = Decoded(request);
            return Json("""{ "results": [] }""");
        }).DiscoverAsync(new DiscoverQuery
        {
            Kind = "film",
            AnyGenreIds = [80, 53],
            AnyKeywordIds = [326438, 157171],
            AnyLanguages = ["sv", "da"],
            OriginCountry = "SE",
            CompanyIds = [10342, 3],
            NetworkIds = [49],
            MinRuntime = 60,
            MaxRuntime = 90,
            MaxRating = 5,
            MaxVotes = 1500,
            MaxCertification = "PG",
            AiringFrom = new DateOnly(2026, 1, 1),
        }, 20, CancellationToken.None);

        Assert.Equal(
            "/3/discover/movie?certification.lte=PG&certification_country=US&include_adult=false&language=en-US&sort_by=popularity.desc"
            + "&vote_average.lte=5.0&vote_count.lte=1500&with_companies=10342|3&with_genres=80|53&with_keywords=326438|157171"
            + "&with_origin_country=SE&with_original_language=sv|da&with_runtime.gte=60&with_runtime.lte=90&page=1",
            asked);
    }

    [Fact]
    public async Task A_discovery_of_shows_asks_for_networks_what_airs_in_a_window_and_what_a_show_is()
    {
        string? asked = null;
        await Client(request =>
        {
            asked = Decoded(request);
            return Json("""{ "results": [] }""");
        }).DiscoverAsync(new DiscoverQuery
        {
            Kind = "show",
            NetworkIds = [49, 2552],
            AiringFrom = new DateOnly(2026, 10, 4),
            AiringTo = new DateOnly(2026, 10, 10),
            ShowStatus = ShowStatuses.Ended,
            ShowType = ShowTypes.Miniseries,
            MaxCertification = "PG",
        }, 20, CancellationToken.None);

        Assert.Contains("with_networks=49|2552", asked);
        Assert.Contains("air_date.gte=2026-10-04&air_date.lte=2026-10-10", asked);
        Assert.Contains("with_status=3", asked);
        Assert.Contains("with_type=2", asked);
        Assert.DoesNotContain("certification", asked);
    }

    [Fact]
    public async Task A_discovery_cannot_ask_for_all_of_and_any_of_at_once()
    {
        var client = Client(_ => Json("""{ "results": [] }"""));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DiscoverAsync(new DiscoverQuery { Kind = "film", GenreIds = [27], AnyGenreIds = [53] }, 20, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DiscoverAsync(new DiscoverQuery { Kind = "film", Language = "ko", AnyLanguages = ["ja"] }, 20, CancellationToken.None));
    }

    [Fact]
    public async Task A_chart_names_its_kind_reads_the_regions_cinemas_and_leaves_unscripted_shows_out()
    {
        var asked = new List<string>();
        var client = Client(request =>
        {
            asked.Add(Decoded(request));
            return Json(request.RequestUri!.AbsolutePath.Contains("/tv/")
                ? """{ "results": [ { "id": 1, "name": "Slow Horses", "genre_ids": [80] }, { "id": 2, "name": "A Talk Show", "genre_ids": [10767] } ] }"""
                : """{ "results": [ { "id": 3, "title": "Heat", "release_date": "1995-12-15" } ] }""");
        });

        var today = await client.ChartAsync(TitleChart.ShowsTrendingToday, 20, CancellationToken.None);
        var cinemas = await client.ChartAsync(TitleChart.InCinemas, 20, CancellationToken.None);
        await client.ChartAsync(TitleChart.FilmsTrendingToday, 20, CancellationToken.None);

        Assert.Equal([("show", "Slow Horses")], today.Select(t => (t.Kind, t.Title)));
        Assert.Equal([("film", "Heat")], cinemas.Select(t => (t.Kind, t.Title)));
        Assert.Equal(
            ["/3/trending/tv/day?language=en-US&page=1", "/3/movie/now_playing?language=en-US&region=US&page=1", "/3/trending/movie/day?language=en-US&page=1"],
            asked);
    }

    [Fact]
    public async Task A_collection_lists_its_films_first_released_first_and_one_with_no_date_last()
    {
        var collection = await Client(_ => Json("""
            { "id": 10, "name": "Star Wars Collection", "parts": [
              { "id": 3, "title": "Return of the Jedi", "release_date": "1983-05-25" },
              { "id": 9, "title": "Untitled", "release_date": "" },
              { "id": 1, "title": "Star Wars", "release_date": "1977-05-25" },
              { "id": 2, "title": "The Empire Strikes Back", "release_date": "1980-05-20" } ] }
            """)).CollectionAsync(10, CancellationToken.None);

        Assert.Equal("Star Wars Collection", collection!.Name);
        Assert.Equal(["Star Wars", "The Empire Strikes Back", "Return of the Jedi", "Untitled"], collection.Films.Select(f => f.Title));
        Assert.All(collection.Films, f => Assert.Equal("film", f.Kind));
    }

    [Fact]
    public async Task No_collection_under_the_id_is_none()
    {
        Assert.Null(await Client(_ => Json("{}", HttpStatusCode.NotFound)).CollectionAsync(4, CancellationToken.None));
    }

    [Theory]
    [InlineData(DiscoverSorts.Popular, "popularity.desc")]
    [InlineData(DiscoverSorts.Rating, "vote_count.desc")]
    [InlineData(DiscoverSorts.Newest, "primary_release_date.desc")]
    [InlineData(DiscoverSorts.Oldest, "primary_release_date.asc")]
    public async Task Each_sort_asks_the_database_for_its_own_order_or_for_the_pool_a_rating_is_weighed_in(string sort, string sortBy)
    {
        string? asked = null;
        await Client(request =>
        {
            asked = Decoded(request);
            return Json("""{ "results": [] }""");
        }).DiscoverAsync(new DiscoverQuery { Kind = "film", Sort = sort }, 20, CancellationToken.None);

        Assert.Contains($"sort_by={sortBy}", asked);
    }

    [Fact]
    public async Task A_discovery_leaves_out_the_genres_it_is_told_to_and_a_shows_unscripted_ones_besides()
    {
        var asked = new List<string>();
        var client = Client(request =>
        {
            asked.Add(Decoded(request));
            return Json("""{ "results": [] }""");
        });

        await client.DiscoverAsync(new DiscoverQuery { Kind = "film", GenreIds = [27], WithoutGenreIds = [16, 10751] }, 20, CancellationToken.None);
        await client.DiscoverAsync(new DiscoverQuery { Kind = "show", WithoutGenreIds = [16] }, 20, CancellationToken.None);

        Assert.Contains("without_genres=16|10751&", asked[0] + "&");
        Assert.Contains("without_genres=16|10767|10763|10764", asked[1]);
    }

    [Fact]
    public async Task A_sort_by_rating_reads_the_most_voted_titles_and_orders_them_by_the_votes_behind_each_rating()
    {
        var pages = new List<string>();
        var client = Client(request =>
        {
            pages.Add(Decoded(request));
            var first = request.RequestUri!.Query.Contains("page=1");
            return Json(first
                ? string.Join("", """{ "results": [ """, Title(1, "Niche", 8.4, 268), ",", Title(2, "Famous", 7.6, 19000), ",", Title(3, "Known", 7.8, 9000), ",", Title(4, "Seen", 7.0, 5000), ",", Title(5, "Middling", 6.9, 6000), """ ] }""")
                : """{ "results": [] }""");
        });

        var found = await client.DiscoverAsync(new DiscoverQuery { Kind = "film", Sort = DiscoverSorts.Rating }, 2, CancellationToken.None);

        Assert.Equal(["Known", "Famous"], found.Select(t => t.Title));
        Assert.Single(pages);
    }

    private static string Title(int id, string name, double average, int votes) =>
        $$"""{ "id": {{id}}, "title": "{{name}}", "release_date": "2010-01-01", "vote_average": {{average}}, "vote_count": {{votes}} }""";

    [Fact]
    public async Task A_discovery_of_shows_dates_by_first_air_leaves_out_unscripted_shows_and_cannot_filter_by_people()
    {
        string? asked = null;
        await Client(request =>
        {
            asked = Decoded(request);
            return Json("""{ "results": [ { "id": 1, "name": "Slow Horses", "first_air_date": "2022-04-01" } ] }""");
        }).DiscoverAsync(new DiscoverQuery
        {
            Kind = "show", From = new DateOnly(2020, 1, 1), PersonIds = [5], Sort = DiscoverSorts.Newest,
        }, 20, CancellationToken.None);

        Assert.StartsWith("/3/discover/tv?", asked);
        Assert.Contains("first_air_date.gte=2020-01-01", asked);
        Assert.Contains("sort_by=first_air_date.desc", asked);
        Assert.Contains("without_genres=10767|10763|10764", asked);
        Assert.DoesNotContain("with_people", asked);
    }

    [Fact]
    public async Task A_discovery_stops_at_the_count_across_pages_and_never_lists_an_adult_title()
    {
        var page = string.Join(",", Enumerable.Range(1, 20).Select(i => $$"""{ "id": {{i}}, "title": "Film {{i}}" }"""));
        var pages = 0;
        var found = await Client(request =>
        {
            pages++;
            return Json($$"""{ "results": [ {{page}}, { "id": 99, "title": "Adult", "adult": true } ] }""");
        }).DiscoverAsync(new DiscoverQuery { Kind = "film" }, 25, CancellationToken.None);

        Assert.Equal(25, found.Count);
        Assert.Equal(2, pages);
        Assert.DoesNotContain(found, t => t.Title == "Adult");
    }

    [Fact]
    public async Task A_page_of_a_discovery_reads_its_own_pages_of_the_database_and_says_how_many_pages_there_are()
    {
        var asked = new List<string>();
        var page = string.Join(",", Enumerable.Range(1, 20).Select(i => $$"""{ "id": {{i}}, "title": "Film {{i}}" }"""));
        var found = await Client(request =>
        {
            asked.Add(Decoded(request));
            return Json($$"""{ "total_pages": 5, "results": [ {{page}} ] }""");
        }).DiscoverPageAsync(new DiscoverQuery { Kind = "film" }, 2, 40, CancellationToken.None);

        Assert.Equal(["page=3", "page=4"], asked.Select(a => a[a.IndexOf("page=", StringComparison.Ordinal)..]));
        Assert.Equal(40, found.Titles.Count);
        Assert.Equal(3, found.Pages);
    }

    [Fact]
    public async Task A_page_the_database_answers_short_is_the_last()
    {
        var found = await Client(_ => Json("""{ "results": [ { "id": 1, "title": "Only" } ] }"""))
            .DiscoverPageAsync(new DiscoverQuery { Kind = "film" }, 1, 20, CancellationToken.None);

        Assert.Equal(["Only"], found.Titles.Select(t => t.Title));
        Assert.Equal(1, found.Pages);
    }

    [Fact]
    public async Task What_is_similar_is_recommendations_first_then_the_rest_without_the_title_itself()
    {
        var asked = new List<string>();
        var similar = await Client(request =>
        {
            asked.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath.EndsWith("/recommendations")
                ? Json("""{ "results": [ { "id": 2, "title": "B" }, { "id": 1, "title": "Itself" }, { "id": 3, "title": "C" } ] }""")
                : Json("""{ "results": [ { "id": 3, "title": "C" }, { "id": 4, "title": "D" } ] }""");
        }).SimilarAsync("film", 1, 1, CancellationToken.None);

        Assert.Equal(["B", "C", "D"], similar.Select(t => t.Title));
        Assert.Equal(["/3/movie/1/recommendations", "/3/movie/1/similar"], asked);
    }

    [Fact]
    public async Task An_imdb_id_is_found_as_a_film_before_a_show()
    {
        var client = Client(request => Json(request.RequestUri!.AbsolutePath.EndsWith("/tt0000001")
            ? """{ "movie_results": [ { "id": 949 } ], "tv_results": [ { "id": 5 } ] }"""
            : request.RequestUri.AbsolutePath.EndsWith("/tt0000002")
                ? """{ "movie_results": [], "tv_results": [ { "id": 5 } ] }"""
                : """{ "movie_results": [], "tv_results": [] }"""));

        var both = await client.FindAsync("tt0000001", CancellationToken.None);
        Assert.Equal(("film", 949), (both!.Kind, both.TmdbId));
        Assert.Equal("show", (await client.FindAsync("2", CancellationToken.None))!.Kind);
        Assert.Null(await client.FindAsync("tt0000003", CancellationToken.None));
        Assert.Null(await client.FindAsync("not an id", CancellationToken.None));
    }

    private const string Pat = """
        {
          "id": 934, "name": "Pat Test", "known_for_department": "Acting", "profile_path": "/pat.jpg",
          "combined_credits": {
            "cast": [
              { "id": 1, "media_type": "movie", "title": "Played And Directed", "release_date": "2014-12-26", "vote_count": 1500, "vote_average": 6.84, "character": "Joshua" },
              { "id": 2, "media_type": "movie", "title": "A Documentary", "release_date": "2010-01-01", "vote_count": 30, "character": "Self - Interviewee" },
              { "id": 3, "media_type": "tv", "name": "A Late Show", "genre_ids": [35, 10767], "first_air_date": "2020-01-01", "character": "", "episode_count": 40 },
              { "id": 4, "media_type": "movie", "title": "Announced", "vote_count": 0, "character": "Lead" },
              { "id": 5, "media_type": "movie", "title": "Grown-up", "adult": true, "character": "X" },
              { "id": 6, "media_type": "person", "name": "Someone", "character": "X" },
              { "id": 7, "media_type": "tv", "name": "Himself Only", "first_air_date": "2021-01-01", "character": "Himself" },
              { "id": 9, "media_type": "tv", "name": "Recurring", "first_air_date": "2022-01-01", "character": "Odin", "episode_count": 2 },
              { "id": 9, "media_type": "tv", "name": "Recurring", "first_air_date": "2022-01-01", "character": "Young Odin", "episode_count": 5 }
            ],
            "crew": [
              { "id": 1, "media_type": "movie", "title": "Played And Directed", "release_date": "2014-12-26", "job": "Director", "department": "Directing" },
              { "id": 1, "media_type": "movie", "title": "Played And Directed", "release_date": "2014-12-26", "job": "Screenplay", "department": "Writing" },
              { "id": 8, "media_type": "movie", "title": "Only Produced", "release_date": "2016-01-01", "job": "Producer", "department": "Production" },
              { "id": 7, "media_type": "tv", "name": "Himself Only", "first_air_date": "2021-01-01", "job": "Creator", "department": "Writing" }
            ]
          }
        }
        """;

    [Fact]
    public async Task A_filmography_lists_every_title_once_with_everything_the_person_did_on_it()
    {
        var person = await Client(_ => Json(Pat)).FilmographyAsync(934, CancellationToken.None);

        Assert.Equal(("Pat Test", "Acting", "/pat.jpg"), (person!.Name, person.KnownFor, person.ProfilePath));
        Assert.Equal(["Played And Directed", "A Documentary", "A Late Show", "Announced", "Himself Only", "Recurring", "Only Produced"],
            person.Credits.Select(c => c.Title));

        var both = person.Credits.Single(c => c.TmdbId == 1);
        Assert.Equal(("Joshua · Director · Screenplay", true, true, true, false, new DateOnly(2014, 12, 26), 6.8),
            (both.Role, both.Acting, both.Directing, both.Writing, both.Appearance, both.Released, both.Rating));
    }

    [Fact]
    public async Task A_filmography_marks_appearances_and_keeps_announcements_without_a_date()
    {
        var person = await Client(_ => Json(Pat)).FilmographyAsync(934, CancellationToken.None);

        Assert.True(person!.Credits.Single(c => c.Title == "A Documentary").Appearance);
        Assert.True(person.Credits.Single(c => c.Title == "A Late Show").Appearance);

        // Appearing as himself on a show he also created is work.
        var created = person.Credits.Single(c => c.Title == "Himself Only");
        Assert.False(created.Appearance);
        Assert.True(created.Writing);

        var announced = person.Credits.Single(c => c.Title == "Announced");
        Assert.Null(announced.Released);
        Assert.False(person.Credits.Single(c => c.Title == "Only Produced").Acting);
        Assert.DoesNotContain(person.Credits, c => c.Title is "Grown-up" or "Someone");
    }

    [Fact]
    public async Task A_filmography_counts_the_episodes_of_a_show_across_the_parts_played_on_it()
    {
        var person = await Client(_ => Json(Pat)).FilmographyAsync(934, CancellationToken.None);

        Assert.Equal(5, person!.Credits.Single(c => c.Title == "Recurring").Episodes);
        Assert.Equal("Odin · Young Odin", person.Credits.Single(c => c.Title == "Recurring").Role);
        Assert.Null(person.Credits.Single(c => c.Title == "Himself Only").Episodes);
        Assert.Null(person.Credits.Single(c => c.Title == "Played And Directed").Episodes);
    }

    [Fact]
    public async Task Nobody_under_the_id_has_no_filmography()
    {
        Assert.Null(await Client(_ => Json("{}", HttpStatusCode.NotFound)).FilmographyAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task A_film_card_names_its_imdb_id_directors_and_two_top_billed_actors()
    {
        string? asked = null;
        var card = await Client(request =>
        {
            asked = request.RequestUri!.PathAndQuery;
            return Json("""
                { "id": 949, "imdb_id": "tt113277", "title": "Heat", "release_date": "1995-12-15", "status": "Released", "original_language": "en",
                  "genres": [ { "id": 80, "name": "Crime" }, { "id": 18, "name": "Drama" } ],
                  "credits": { "cast": [ { "id": 3, "name": "Third", "order": 2 }, { "id": 1, "name": "First", "order": 0 }, { "id": 2, "name": "Second", "order": 1 } ],
                               "crew": [ { "id": 638, "name": "Michael Mann", "job": "Director" }, { "id": 9, "name": "Someone", "job": "Editor" } ] } }
                """);
        }).CardAsync("film", 949, CancellationToken.None);

        Assert.StartsWith("/3/movie/949?append_to_response=credits", asked);
        Assert.Equal(("tt0113277", "Heat", "Released", new DateOnly(1995, 12, 15)), (card!.ImdbId, card.Title, card.Status, card.Released));
        Assert.Equal(["Crime", "Drama"], card.Genres);
        Assert.Equal(["Michael Mann"], card.MadeBy);
        Assert.Equal(["First", "Second"], card.Starring);
    }

    [Fact]
    public async Task A_show_card_names_its_creators_and_leaves_the_imdb_id_empty_when_the_database_has_none()
    {
        var card = await Client(_ => Json("""
            { "id": 6000, "name": "Heat Wave", "first_air_date": "2019-04-01", "status": "Ended",
              "created_by": [ { "id": 71, "name": "Rae Cho" } ], "external_ids": { "imdb_id": null },
              "aggregate_credits": { "cast": [ { "id": 72, "name": "Ola Beck", "order": 0 } ] } }
            """)).CardAsync("show", 6000, CancellationToken.None);

        Assert.Null(card!.ImdbId);
        Assert.Equal(["Rae Cho"], card.MadeBy);
        Assert.Equal(["Ola Beck"], card.Starring);
        Assert.Null(await Client(_ => Json("{}", HttpStatusCode.NotFound)).CardAsync("show", 1, CancellationToken.None));
        Assert.Null(await Client(_ => throw new InvalidOperationException()).CardAsync("episode", 1, CancellationToken.None));
    }

    [Fact]
    public async Task A_titles_traits_are_its_keyword_ids_and_for_a_film_everyone_credited_on_it()
    {
        var traits = await Client(_ => Json("""
            { "id": 7004, "keywords": { "keywords": [ { "id": 12999, "name": "time loop" } ] },
              "credits": { "cast": [ { "id": 8001, "name": "Ray" } ], "crew": [ { "id": 7777, "name": "Jo", "job": "Director" }, { "id": 8001, "name": "Ray", "job": "Producer" } ] } }
            """)).TraitsAsync("film", 7004, CancellationToken.None);

        Assert.Equal([12999], traits!.Keywords);
        Assert.Equal([8001, 7777], traits.People);

        var show = await Client(_ => Json("""{ "id": 1, "keywords": { "results": [ { "id": 5, "name": "x" } ] } }""")).TraitsAsync("show", 1, CancellationToken.None);
        Assert.Equal([5], show!.Keywords);
        Assert.Empty(show.People);
    }

    [Fact]
    public async Task A_description_carries_the_keywords_the_database_gives()
    {
        var asked = new List<string>();
        var film = await Client(request =>
        {
            asked.Add(request.RequestUri!.PathAndQuery);
            return Json(request.RequestUri.AbsolutePath.Contains("/find/")
                ? """{ "movie_results": [ { "id": 7004 } ] }"""
                : """{ "id": 7004, "title": "Silver Harbour", "keywords": { "keywords": [ { "id": 12999, "name": "Time Loop" }, { "id": 5, "name": "harbour" } ] } }""");
        }).DetailsAsync("tt9000004", CancellationToken.None);

        Assert.Contains("keywords", asked[1]);
        Assert.Equal(["Time Loop", "harbour"], film!.Keywords);
    }

    [Fact]
    public async Task A_film_the_database_lists_no_keywords_for_has_an_empty_list_rather_than_none()
    {
        var film = await Client(request => Json(request.RequestUri!.AbsolutePath.Contains("/find/")
            ? """{ "movie_results": [ { "id": 1 } ] }"""
            : """{ "id": 1, "title": "Plain" }""")).DetailsAsync("tt0000001", CancellationToken.None);

        Assert.NotNull(film!.Keywords);
        Assert.Empty(film.Keywords);
    }

    [Fact]
    public async Task A_request_the_database_says_was_one_too_many_is_made_again_after_the_wait_it_names()
    {
        var calls = 0;
        var keywords = await Client(_ =>
        {
            if (++calls < 3)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }
            return Json("""{ "results": [ { "id": 1, "name": "heist" } ] }""");
        }).SearchKeywordsAsync("heist", CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Single(keywords);
    }

    [Fact]
    public async Task A_database_that_keeps_refusing_is_an_error_not_a_title_missing()
    {
        var calls = 0;
        var client = Client(_ =>
        {
            calls++;
            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return limited;
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchKeywordsAsync("heist", CancellationToken.None));
        Assert.Equal(3, calls);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }
}
