using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public class TitleMatchTests
{
    [Theory]
    [InlineData("Heat", "Heat")]
    [InlineData("heat", "Heat")]
    // An edition or a cut is extra description of the same film.
    [InlineData("Heat", "Heat Directors Cut")]
    [InlineData("Blade Runner 2049", "Blade Runner 2049")]
    // Articles are added and dropped without meaning a different film.
    [InlineData("The Thing", "Thing")]
    [InlineData("Thing", "The Thing")]
    // Punctuation and accents vary between what is typed and what a release is named.
    [InlineData("Oceans Eleven", "Ocean's Eleven")]
    [InlineData("Amelie", "Amélie")]
    public void Accepts_the_film_that_was_asked_for(string query, string release) =>
        Assert.True(TitleMatch.Matches(query, release));

    [Theory]
    // The case the tracker actually produces: another film sharing the year it was narrowed by.
    [InlineData("Heat", "Lord of Illusions")]
    [InlineData("Heat", "Dead Heat")]
    [InlineData("Dune", "Dune Part Two")]
    public void Rejects_a_different_film(string query, string release) =>
        Assert.False(TitleMatch.Matches(query, release));

    [Fact]
    public void Accepts_everything_when_there_is_no_title_to_compare() =>
        Assert.True(TitleMatch.Matches("", "Anything At All"));
}
