using TheKrystalShip.MovieBot.Acquire.Download;
using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Two of these tags are a prefix and a number, and reading one as the other would send a film's
/// announcement to a person's id as though it were a channel, or ping a channel id as though it
/// were a person. Neither throws.
/// </summary>
public class TorrentTagsTests
{
    [Fact]
    public void A_channel_survives_the_round_trip() =>
        Assert.Equal(385731869163126784UL,
            TorrentTags.ReadNotifyChannel(TorrentTags.Notify(385731869163126784UL)));

    [Theory]
    [InlineData("local:usr_01JABCDEF")]
    [InlineData("https://idp.invalid/|a,b%2C")]
    public void A_requester_survives_the_round_trip(string account) =>
        Assert.Equal(account, TorrentTags.ReadRequester(TorrentTags.Requester(account)));

    [Fact]
    public void A_requester_tag_carries_no_comma()
    {
        // The torrent client splits a tag list on commas, so one in an account would cut the tag in two.
        Assert.DoesNotContain(',', TorrentTags.Requester("a,b"));
    }

    [Fact]
    public void A_requester_tag_is_not_read_as_a_channel() =>
        Assert.Null(TorrentTags.ReadNotifyChannel(TorrentTags.Requester("local:usr_1")));

    [Fact]
    public void A_channel_tag_is_not_read_as_a_requester() =>
        Assert.Null(TorrentTags.ReadRequester(TorrentTags.Notify(1234567890UL)));

    [Fact]
    public void A_mention_survives_the_round_trip() =>
        Assert.Equal(214987225747587072UL, TorrentTags.ReadMention(TorrentTags.Mention(214987225747587072UL)));

    [Fact]
    public void A_mention_is_not_read_as_a_requester_or_a_channel()
    {
        Assert.Null(TorrentTags.ReadRequester(TorrentTags.Mention(1234567890UL)));
        Assert.Null(TorrentTags.ReadNotifyChannel(TorrentTags.Mention(1234567890UL)));
        Assert.Null(TorrentTags.ReadMention(TorrentTags.Requester("1234567890")));
    }

    [Fact]
    public void A_progress_message_survives_the_round_trip()
    {
        var tag = TorrentTags.Progress(385731869163126784UL, 1122334455667788990UL);

        Assert.Equal((385731869163126784UL, 1122334455667788990UL), TorrentTags.ReadProgress(tag));
    }

    [Fact]
    public void A_progress_tag_keeps_the_channel_and_the_message_the_right_way_round()
    {
        // Two ids of the same shape in one tag. Swapped, every edit would be attempted against a
        // channel that is really a message and simply never appear.
        var read = TorrentTags.ReadProgress(TorrentTags.Progress(111UL, 222UL));

        Assert.Equal(111UL, read!.Value.ChannelId);
        Assert.Equal(222UL, read.Value.MessageId);
    }

    [Fact]
    public void A_room_survives_the_round_trip() =>
        Assert.Equal(918273645UL, TorrentTags.ReadRoom(TorrentTags.Room(918273645UL)));

    [Fact]
    public void A_library_id_survives_the_round_trip() =>
        Assert.Equal("heat-1995", TorrentTags.ReadLibrary(TorrentTags.Library("heat-1995")));

    [Fact]
    public void A_room_tag_is_not_read_as_a_channel_or_a_requester()
    {
        // The same shape as both. Read as a channel it would announce a film into a voice
        // channel; read as a requester it would ping a channel as though it were a person.
        Assert.Null(TorrentTags.ReadNotifyChannel(TorrentTags.Room(1234567890UL)));
        Assert.Null(TorrentTags.ReadMention(TorrentTags.Room(1234567890UL)));
        Assert.Null(TorrentTags.ReadRoom(TorrentTags.Notify(1234567890UL)));
    }

    [Theory]
    [InlineData("progress:123")]
    [InlineData("progress:123:456:789")]
    [InlineData("progress::")]
    [InlineData("progress:abc:456")]
    [InlineData("notify:123")]
    public void A_malformed_progress_tag_reads_as_nothing(string tag) =>
        Assert.Null(TorrentTags.ReadProgress(tag));

    [Theory]
    [InlineData("ingest")]
    [InlineData("ingest-failed")]
    [InlineData("notify:")]
    [InlineData("requester:")]
    [InlineData("notify:not-a-number")]
    [InlineData("room:")]
    [InlineData("library:")]
    [InlineData("")]
    public void Anything_else_reads_as_nothing(string tag)
    {
        Assert.Null(TorrentTags.ReadNotifyChannel(tag));
        Assert.Null(TorrentTags.ReadRequester(tag));
        Assert.Null(TorrentTags.ReadMention(tag));
        Assert.Null(TorrentTags.ReadProgress(tag));
        Assert.Null(TorrentTags.ReadRoom(tag));
        Assert.Null(TorrentTags.ReadLibrary(tag));
        Assert.Null(TorrentTags.ReadWatch(tag));
    }

    [Fact]
    public void A_site_room_round_trips() =>
        Assert.Equal(new WatchTag("a1b2c3d4", null), TorrentTags.ReadWatch(TorrentTags.Watch("a1b2c3d4")));

    [Fact]
    public void A_room_waiting_on_one_episode_round_trips()
    {
        var tag = TorrentTags.Watch("a1b2c3d4", new EpisodeNumber(5, 3));

        Assert.Equal("watch:a1b2c3d4:s05e03", tag);
        Assert.Equal(new WatchTag("a1b2c3d4", new EpisodeNumber(5, 3)), TorrentTags.ReadWatch(tag));
    }

    [Theory]
    [InlineData("watch:")]
    [InlineData("watch:a b")]
    [InlineData("watch:a,b")]
    [InlineData("watch:../x")]
    [InlineData("watch:room:s05")]
    [InlineData("watch:room:s05e03:extra")]
    public void A_watch_tag_naming_no_usable_room_reads_as_nothing(string tag) =>
        Assert.Null(TorrentTags.ReadWatch(tag));

    [Fact]
    public void A_passed_episode_round_trips()
    {
        var tag = TorrentTags.Passed(new EpisodeNumber(2, 11));

        Assert.Equal("passed:s02e11", tag);
        Assert.Equal(new EpisodeNumber(2, 11), TorrentTags.ReadPassed(tag));
    }

    [Fact]
    public void A_promise_of_an_episode_or_a_film_round_trips_its_person()
    {
        var episode = TorrentTags.TellOf("usr_1", new EpisodeNumber(4, 13));
        var film = TorrentTags.TellOf("a,b:c%");

        Assert.Equal("tell:s04e13:usr_1", episode);
        Assert.Equal(("usr_1", (EpisodeNumber?)new EpisodeNumber(4, 13)), TorrentTags.ReadTellOf(episode));
        Assert.Equal("tell:film:a%2Cb:c%25", film);
        Assert.Equal(("a,b:c%", (EpisodeNumber?)null), TorrentTags.ReadTellOf(film));
    }

    [Theory]
    [InlineData("tell")]
    [InlineData("tell:")]
    [InlineData("tell:s04e13")]
    [InlineData("tell:s04e13:")]
    [InlineData("tell:season:usr_1")]
    [InlineData("requester:usr_1")]
    public void Anything_but_a_promise_reads_as_none(string tag) =>
        Assert.Null(TorrentTags.ReadTellOf(tag));

    [Theory]
    [InlineData("passed:")]
    [InlineData("passed:s02")]
    [InlineData("watch:a1b2c3d4:s02e11")]
    [InlineData("keep:s02e11")]
    public void Anything_but_a_passed_tag_reads_as_no_passed_episode(string tag) =>
        Assert.Null(TorrentTags.ReadPassed(tag));

    [Fact]
    public void A_keep_names_its_episode_and_no_other()
    {
        var tag = TorrentTags.Keep(new EpisodeNumber(2, 4));

        Assert.True(TorrentTags.Keeps(tag, new EpisodeNumber(2, 4)));
        Assert.False(TorrentTags.Keeps(tag, new EpisodeNumber(2, 5)));
        Assert.False(TorrentTags.Keeps(tag, new EpisodeNumber(1, 4)));
    }

    [Fact]
    public void A_bare_keep_keeps_every_episode() =>
        Assert.True(TorrentTags.Keeps(TorrentTags.Keep(), new EpisodeNumber(7, 22)));

    [Fact]
    public void A_room_waiting_on_an_episode_does_not_keep_it() =>
        Assert.False(TorrentTags.Keeps(TorrentTags.Watch("a1b2c3d4", new EpisodeNumber(2, 4)), new EpisodeNumber(2, 4)));

    [Fact]
    public void A_want_survives_the_round_trip() =>
        Assert.Equal("tt0458352", TorrentTags.ReadWant(TorrentTags.Want("tt0458352")));

    [Fact]
    public void A_want_is_not_read_as_a_film_or_as_a_want_of_nothing()
    {
        Assert.Null(TorrentTags.ReadImdb(TorrentTags.Want("tt0458352")));
        Assert.Null(TorrentTags.ReadWant(TorrentTags.Imdb("tt0458352")));
        Assert.Null(TorrentTags.ReadWant("want:"));
        Assert.Null(TorrentTags.ReadWant("want:not-an-id"));
    }
}
