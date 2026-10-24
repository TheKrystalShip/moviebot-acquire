using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// What one description call carries beyond the facts: official trailers, recommendations, reviews,
/// the logo, box office and keywords with their ids.
/// </summary>
public class TmdbExtrasTests
{
    private const string Find = """{ "movie_results": [ { "id": 27205 } ], "tv_results": [] }""";

    private const string Inception = """
        {
          "id": 27205, "imdb_id": "tt1375666", "title": "Inception", "tagline": "Your mind is the scene of the crime.",
          "release_date": "2010-07-15", "budget": 160000000, "revenue": 839030630,
          "keywords": { "keywords": [ { "id": 1, "name": "dream" }, { "id": 2, "name": "heist" }, { "id": 1, "name": "dream" } ] },
          "videos": { "results": [
            { "key": "late", "name": "Final Trailer", "site": "YouTube", "type": "Trailer", "official": true, "iso_639_1": "en", "published_at": "2010-06-01T00:00:00.000Z" },
            { "key": "teaser", "name": "Teaser", "site": "YouTube", "type": "Teaser", "official": true, "iso_639_1": "en", "published_at": "2009-01-01T00:00:00.000Z" },
            { "key": "fan", "name": "Fan trailer", "site": "YouTube", "type": "Trailer", "official": false, "iso_639_1": "en", "published_at": "2009-02-01T00:00:00.000Z" },
            { "key": "first", "name": "Official Trailer", "site": "YouTube", "type": "Trailer", "official": true, "iso_639_1": "en", "published_at": "2010-03-01T00:00:00.000Z" },
            { "key": "fr", "name": "Bande-annonce", "site": "YouTube", "type": "Trailer", "official": true, "iso_639_1": "fr", "published_at": "2010-02-01T00:00:00.000Z" },
            { "key": "vimeo", "name": "Elsewhere", "site": "Vimeo", "type": "Trailer", "official": true, "iso_639_1": "en" },
            { "key": "clip", "name": "A clip", "site": "YouTube", "type": "Clip", "official": true, "iso_639_1": "en" },
            { "key": "making", "name": "Making of", "site": "YouTube", "type": "Behind the Scenes", "official": true, "iso_639_1": "en" } ] },
          "recommendations": { "results": [
            { "id": 27205, "title": "Itself" },
            { "id": 157336, "title": "Interstellar", "release_date": "2014-11-05", "vote_average": 8.4, "vote_count": 30000, "poster_path": "/i.jpg" },
            { "id": 157336, "title": "Interstellar again" },
            { "id": 9, "title": "Hidden", "adult": true } ] },
          "reviews": { "total_results": 8, "results": [
            { "author": "A", "content": "Short.", "url": "https://example.test/a", "author_details": { "rating": 9.0 } },
            { "author": "", "content": "   ", "url": "https://example.test/blank" },
            { "author": "B", "content": "WORDS", "url": "https://example.test/b", "author_details": { "rating": null } },
            { "author": "C", "content": "Three.", "url": "https://example.test/c" },
            { "author": "D", "content": "Four.", "url": "https://example.test/d" } ] },
          "images": { "logos": [
            { "file_path": "/fr.png", "iso_639_1": "fr", "vote_average": 9, "width": 900 },
            { "file_path": "/none.png", "iso_639_1": null, "vote_average": 5, "width": 400 },
            { "file_path": "/en-low.png", "iso_639_1": "en", "vote_average": 1, "width": 300 },
            { "file_path": "/en.png", "iso_639_1": "en", "vote_average": 3, "width": 500 },
            { "file_path": "/en.svg", "iso_639_1": "en", "vote_average": 9, "width": 900 } ] }
        }
        """;

    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new HttpClient(new Handler(answer)), Options.Create(new TmdbOptions { ReadAccessToken = "a-token" }),
            NullLogger<TmdbClient>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<(FilmDetails? Film, List<string> Asked)> Described(string movie = Inception)
    {
        var asked = new List<string>();
        var film = await Client(request =>
        {
            asked.Add(Uri.UnescapeDataString(request.RequestUri!.PathAndQuery));
            return Json(request.RequestUri.AbsolutePath.Contains("/find/") ? Find : movie);
        }).DetailsAsync("tt1375666", CancellationToken.None);
        return (film, asked);
    }

    [Fact]
    public async Task One_call_asks_for_everything_a_description_carries()
    {
        var (film, asked) = await Described();

        Assert.NotNull(film);
        Assert.Equal(2, asked.Count);
        Assert.StartsWith("/3/movie/27205?append_to_response=credits,release_dates,keywords,videos,recommendations,reviews,images", asked[1]);
        Assert.Contains("include_video_language=en,null", asked[1]);
        Assert.Contains("include_image_language=en,null", asked[1]);
    }

    [Fact]
    public async Task Trailers_are_the_official_youtube_trailers_and_teasers_a_trailer_before_a_teaser()
    {
        var (film, _) = await Described();

        Assert.Equal(["first", "late", "fr", "teaser"], film!.Trailers!.Select(t => t.Key));
        var first = film.Trailers![0];
        Assert.Equal(("Official Trailer", "Trailer", "en"), (first.Name, first.Kind, first.Language));
    }

    [Fact]
    public async Task A_film_with_no_videos_has_an_empty_list_and_not_a_missing_one()
    {
        var (film, _) = await Described("""{ "id": 27205, "title": "Inception" }""");

        Assert.NotNull(film!.Trailers);
        Assert.Empty(film.Trailers);
        Assert.Empty(film.Recommendations);
        Assert.Null(film.LogoPath);
        Assert.Null(film.Budget);
    }

    [Fact]
    public async Task Recommendations_leave_out_the_film_itself_repeats_and_adult_titles()
    {
        var (film, _) = await Described();

        var recommended = Assert.Single(film!.Recommendations);
        Assert.Equal(("film", 157336, "Interstellar", 8.4, "/i.jpg"), (recommended.Kind, recommended.TmdbId, recommended.Title, recommended.Rating, recommended.PosterPath));
    }

    [Fact]
    public async Task Reviews_are_the_first_few_with_text_and_say_how_many_there_are()
    {
        var (film, _) = await Described();

        Assert.Equal(8, film!.ReviewCount);
        Assert.Equal(["A", "B", "C"], film.Reviews.Select(r => r.Author));
        Assert.Equal((9.0, "Short.", "https://example.test/a"), (film.Reviews[0].Rating, film.Reviews[0].Excerpt, film.Reviews[0].Url));
        Assert.Null(film.Reviews[1].Rating);
    }

    [Fact]
    public async Task A_long_review_is_cut_on_a_word()
    {
        var long_ = string.Join(' ', Enumerable.Repeat("remarkable", 100));
        var (film, _) = await Described($$"""{ "id": 27205, "title": "Inception", "reviews": { "total_results": 1, "results": [ { "author": "A", "content": "{{long_}}" } ] } }""");

        var excerpt = film!.Reviews[0].Excerpt;
        Assert.True(excerpt.Length <= TmdbClient.ExcerptLength + 1);
        Assert.EndsWith("remarkable…", excerpt);
    }

    [Fact]
    public async Task The_logo_is_a_png_in_the_configured_language_then_english_then_textless_best_voted_first()
    {
        var (film, _) = await Described();
        Assert.Equal("/en.png", film!.LogoPath);

        var (textless, _) = await Described("""{ "id": 1, "title": "T", "images": { "logos": [ { "file_path": "/fr.png", "iso_639_1": "fr" }, { "file_path": "/none.png", "iso_639_1": null } ] } }""");
        Assert.Equal("/none.png", textless!.LogoPath);

        var (foreign, _) = await Described("""{ "id": 1, "title": "T", "images": { "logos": [ { "file_path": "/fr.png", "iso_639_1": "fr" } ] } }""");
        Assert.Null(foreign!.LogoPath);
    }

    [Fact]
    public async Task Box_office_and_keywords_with_their_ids_are_carried()
    {
        var (film, _) = await Described();

        Assert.Equal((160000000L, 839030630L), (film!.Budget, film.Revenue));
        Assert.Equal("Your mind is the scene of the crime.", film.Tagline);
        Assert.Equal([(1, "dream"), (2, "heist")], film.KeywordTags!.Select(k => (k.Id, k.Name)));
        Assert.Equal(["dream", "heist"], film.Keywords);

        var (unknown, _) = await Described("""{ "id": 1, "title": "T", "budget": 0, "revenue": 0 }""");
        Assert.Null(unknown!.Budget);
        Assert.Null(unknown.Revenue);
    }

    [Fact]
    public async Task A_show_is_described_in_one_call_with_the_same_extras()
    {
        var asked = new List<string>();
        var show = await Client(request =>
        {
            asked.Add(Uri.UnescapeDataString(request.RequestUri!.PathAndQuery));
            return Json(request.RequestUri.AbsolutePath.Contains("/find/")
                ? """{ "movie_results": [], "tv_results": [ { "id": 1396 } ] }"""
                : """
                  { "id": 1396, "name": "Breaking Bad",
                    "keywords": { "results": [ { "id": 7, "name": "drug" } ] },
                    "videos": { "results": [ { "key": "bb", "name": "Trailer", "site": "YouTube", "type": "Trailer", "official": true, "iso_639_1": "en" } ] },
                    "recommendations": { "results": [ { "id": 60059, "name": "Better Call Saul", "first_air_date": "2015-02-08" } ] } }
                  """);
        }).ShowAsync("tt0903747", CancellationToken.None);

        Assert.Equal(2, asked.Count);
        Assert.Contains("append_to_response=aggregate_credits,content_ratings,keywords,videos,recommendations,reviews,images", asked[1]);
        Assert.Equal(["bb"], show!.Trailers!.Select(t => t.Key));
        Assert.Equal([(7, "drug")], show.KeywordTags!.Select(k => (k.Id, k.Name)));
        Assert.Equal(("show", "Better Call Saul"), (show.Recommendations[0].Kind, show.Recommendations[0].Title));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }
}
