using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using TheKrystalShip.MovieBot.Acquire.Subtitles;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>An episode's subtitles are asked for by its show and its place, and only that episode's are kept.</summary>
public sealed class OpenSubtitlesEpisodeTests
{
    private sealed class Index(string body) : HttpMessageHandler
    {
        public string? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked = request.RequestUri!.Query;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string Item(string id, int season, int episode) =>
        "{\"id\":\"" + id + "\",\"attributes\":{\"language\":\"en\",\"files\":[{\"file_id\":" + id + "}],"
        + "\"feature_details\":{\"parent_imdb_id\":4477976,\"season_number\":" + season + ",\"episode_number\":" + episode + "}}}";

    [Fact]
    public async Task An_episode_is_asked_for_by_its_show_and_its_place_and_other_episodes_are_left_out()
    {
        var index = new Index($$"""{"total_pages":1,"data":[{{Item("1", 2, 8)}},{{Item("2", 0, 1)}}]}""");
        var client = new OpenSubtitlesClient(new HttpClient(index),
            Options.Create(new OpenSubtitlesOptions { ApiKey = "k", MinimumRequestIntervalMs = 0 }),
            NullLogger<OpenSubtitlesClient>.Instance);

        var found = await client.SearchByEpisodeAsync("tt4477976", 2, 8, "en", CancellationToken.None);

        Assert.Equal("?episode_number=8&languages=en&parent_imdb_id=4477976&season_number=2", index.Asked);
        Assert.Equal("1", Assert.Single(found).Id);
    }
}
