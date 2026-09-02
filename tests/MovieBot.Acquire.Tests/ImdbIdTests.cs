using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// An id arrives inside whatever somebody pasted, and a bare number is a title, not an id.
/// </summary>
public sealed class ImdbIdTests
{
    [Theory]
    [InlineData("https://www.imdb.com/title/tt0458352/", "tt0458352")]
    [InlineData("https://m.imdb.com/title/tt0458352", "tt0458352")]
    [InlineData("https://www.imdb.com/title/tt0458352/?ref_=nv_sr_srsg_0_tt_8_nm_0_in_0_q_prada", "tt0458352")]
    [InlineData("imdb.com/title/tt33612209/reference/", "tt33612209")]
    [InlineData("tt0458352", "tt0458352")]
    [InlineData("TT0458352", "tt0458352")]
    [InlineData("  tt458352 ", "tt0458352")]
    public void The_id_is_taken_from_a_link_or_a_bare_id(string pasted, string expected)
    {
        Assert.Equal(expected, ImdbId.FromText(pasted));
    }

    [Theory]
    [InlineData("1917")]
    [InlineData("458352")]
    [InlineData("The Devil Wears Prada")]
    [InlineData("https://www.imdb.com/name/nm0000658/")]
    [InlineData("butter")]
    [InlineData("")]
    public void Text_without_an_id_yields_none(string typed)
    {
        Assert.Null(ImdbId.FromText(typed));
    }
}
