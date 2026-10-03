using TheKrystalShip.MovieBot.Acquire.Download;
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

    [Fact]
    public void A_requester_survives_the_round_trip() =>
        Assert.Equal(214987225747587072UL,
            TorrentTags.ReadRequester(TorrentTags.Requester(214987225747587072UL)));

    [Fact]
    public void A_requester_tag_is_not_read_as_a_channel() =>
        Assert.Null(TorrentTags.ReadNotifyChannel(TorrentTags.Requester(1234567890UL)));

    [Fact]
    public void A_channel_tag_is_not_read_as_a_requester() =>
        Assert.Null(TorrentTags.ReadRequester(TorrentTags.Notify(1234567890UL)));

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
        Assert.Null(TorrentTags.ReadRequester(TorrentTags.Room(1234567890UL)));
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
        Assert.Null(TorrentTags.ReadProgress(tag));
        Assert.Null(TorrentTags.ReadRoom(tag));
        Assert.Null(TorrentTags.ReadLibrary(tag));
    }
}
