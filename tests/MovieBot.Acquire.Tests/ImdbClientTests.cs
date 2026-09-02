using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.MovieBot.Acquire.Imdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// The index answers a search box, so it is ordered by what people are looking for today rather
/// than by what was asked for. Both of the things that go wrong here follow from that: a sequel
/// standing where the film should be, and a remake standing where the original should.
/// </summary>
public sealed class ImdbClientTests
{
    private const string Prada = """
        {"d":[
          {"id":"tt33612209","l":"The Devil Wears Prada 2","y":2026,"qid":"movie",
           "s":"Meryl Streep, Anne Hathaway",
           "i":{"imageUrl":"https://m.media-amazon.com/images/M/MV5BZmM3.jpg","width":2100,"height":3156}},
          {"id":"tt0458352","l":"The Devil Wears Prada","y":2006,"qid":"movie",
           "s":"Anne Hathaway, Meryl Streep",
           "i":{"imageUrl":"https://m.media-amazon.com/images/M/MV5BOWM3._V1_.jpg","width":2100,"height":3156}}
        ],"q":"the devil wears prada","v":1}
        """;

    private const string Gladiators = """
        {"d":[
          {"id":"tt0172495","l":"Gladiator","y":2000,"qid":"movie","s":"Russell Crowe",
           "i":{"imageUrl":"https://m.media-amazon.com/images/M/MV5BYWQ4._V1_.jpg","width":2100,"height":3156}},
          {"id":"tt9218128","l":"Gladiator II","y":2024,"qid":"movie","s":"Paul Mescal"},
          {"id":"tt0111667","l":"Gladiator","y":1992,"qid":"movie","s":"James Marshall"}
        ],"q":"gladiator","v":1}
        """;

    [Fact]
    public async Task An_exact_name_beats_whatever_the_index_ranks_first()
    {
        var found = await Client(Prada).SearchAsync("The Devil Wears Prada", null, default);

        Assert.Equal("tt0458352", found?.ImdbId);
        Assert.Equal(2006, found?.Year);
        Assert.Equal("Anne Hathaway, Meryl Streep", found?.Starring);
    }

    [Fact]
    public async Task A_year_separates_a_film_from_the_one_that_remade_it()
    {
        Assert.Equal("tt0172495", (await Client(Gladiators).SearchAsync("Gladiator", 2000, default))?.ImdbId);
        Assert.Equal("tt0111667", (await Client(Gladiators).SearchAsync("Gladiator", 1992, default))?.ImdbId);
    }

    /// <summary>
    /// Nothing is offered as a guess when a year was given and no film carries it. A film named
    /// wrongly is worse than a film not named at all: everything downstream believes it.
    /// </summary>
    [Fact]
    public async Task A_year_nothing_carries_finds_nothing()
    {
        Assert.Null(await Client(Gladiators).SearchAsync("Gladiator", 1977, default));
    }

    [Fact]
    public async Task A_lookup_takes_only_the_entry_the_id_names()
    {
        Assert.Equal("The Devil Wears Prada", (await Client(Prada).LookupAsync("tt0458352", default))?.Title);
        Assert.Null(await Client(Prada).LookupAsync("tt0000001", default));
    }

    [Theory]
    [InlineData("458352")]
    [InlineData("tt458352")]
    public async Task A_lookup_accepts_either_spelling_of_an_id(string spelling)
    {
        Assert.Equal("tt0458352", (await Client(Prada).LookupAsync(spelling, default))?.ImdbId);
    }

    [Fact]
    public async Task An_index_that_cannot_be_reached_names_no_film()
    {
        Assert.Null(await Client("", HttpStatusCode.ServiceUnavailable).LookupAsync("tt0458352", default));
        Assert.Null(await Client("", HttpStatusCode.NotFound).LookupAsync("tt0458352", default));
    }

    /// <summary>
    /// The originals are a few thousand pixels tall. Asking the image host for the width that
    /// will actually be looked at is the difference between a poster that renders in a message
    /// and one that times out on the way there.
    /// </summary>
    [Fact]
    public void A_poster_is_asked_for_at_the_size_it_will_be_seen()
    {
        var film = new ImdbTitle
        {
            ImdbId = "tt0133093",
            Title = "The Matrix",
            PosterUrl = new Uri("https://m.media-amazon.com/images/M/MV5BN2Nm._V1_.jpg")
        };

        Assert.Equal(
            "https://m.media-amazon.com/images/M/MV5BN2Nm._V1_QL75_UX600_.jpg",
            film.PosterAt(600)?.ToString());
    }

    [Fact]
    public void A_poster_the_host_does_not_resize_is_asked_for_whole()
    {
        var film = new ImdbTitle
        {
            ImdbId = "tt1", Title = "A film",
            PosterUrl = new Uri("https://example.invalid/poster.jpg")
        };

        Assert.Equal("https://example.invalid/poster.jpg", film.PosterAt(600)?.ToString());
        Assert.Null(new ImdbTitle { ImdbId = "tt1", Title = "A film" }.PosterAt(600));
    }

    [Theory]
    [InlineData("movie", true)]
    [InlineData("tvMovie", true)]
    [InlineData(null, true)]
    [InlineData("tvSeries", false)]
    [InlineData("tvEpisode", false)]
    public void Only_a_film_is_a_film(string? kind, bool expected)
    {
        Assert.Equal(expected, new ImdbTitle { ImdbId = "tt1", Title = "x", Kind = kind }.IsFeature);
    }

    /// <summary>A series that happens to be named like the film is not the film.</summary>
    [Fact]
    public async Task A_search_offers_no_series()
    {
        const string body = """
            {"d":[{"id":"tt1","l":"Heat","y":1995,"qid":"tvSeries"}],"q":"heat","v":1}
            """;

        Assert.Null(await Client(body).SearchAsync("Heat", 1995, default));
    }

    /// <summary>
    /// A pasted link is answered with the film it names, not with a search for the text of the
    /// link. The index answers that search with nothing, which reads as the link being wrong.
    /// </summary>
    [Fact]
    public async Task A_pasted_link_suggests_the_film_it_names()
    {
        var offered = await Client(Prada).SuggestAsync(
            "https://www.imdb.com/title/tt0458352/?ref_=fn_al_tt_1", default);

        Assert.Equal(["tt0458352"], offered.Select(t => t.ImdbId));
    }

    [Fact]
    public async Task Typed_text_suggests_the_films_the_index_offers_in_its_order()
    {
        var offered = await Client(Gladiators).SuggestAsync("gladiator", default);

        Assert.Equal(["tt0172495", "tt9218128", "tt0111667"], offered.Select(t => t.ImdbId));
    }

    [Fact]
    public async Task A_suggestion_offers_no_series()
    {
        const string body = """
            {"d":[{"id":"tt1","l":"Heat","y":1995,"qid":"tvSeries"},
                  {"id":"tt0113277","l":"Heat","y":1995,"qid":"movie"}],"q":"heat","v":1}
            """;

        Assert.Equal(["tt0113277"], (await Client(body).SuggestAsync("heat", default)).Select(t => t.ImdbId));
    }

    private static ImdbClient Client(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new StubHandler(body, status)), NullLogger<ImdbClient>.Instance);

    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
