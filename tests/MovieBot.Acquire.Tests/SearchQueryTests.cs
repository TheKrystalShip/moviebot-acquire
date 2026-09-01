using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// What people type is a title and, very often, a year. Reading the year out is what separates
/// a film from its remake, and reading one out that was never there breaks the search entirely.
/// </summary>
public class SearchQueryTests
{
    [Theory]
    [InlineData("Heat 1995", "Heat", 1995)]
    [InlineData("heat 1995", "heat", 1995)]
    [InlineData("Dune 2021", "Dune", 2021)]
    [InlineData("The Thing 1982", "The Thing", 1982)]
    [InlineData("Blade Runner 2049 2017", "Blade Runner 2049", 2017)]
    [InlineData("Heat (1995)", "Heat", 1995)]
    [InlineData("Heat.1995", "Heat", 1995)]
    [InlineData("  Heat   1995  ", "Heat", 1995)]
    public void Reads_a_trailing_year(string input, string title, int year)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(title, query.Title);
        Assert.Equal(year, query.Year);
    }

    [Theory]
    // Set too far ahead to be a release year, so it belongs to the title.
    [InlineData("Blade Runner 2049")]
    [InlineData("2001 A Space Odyssey")]
    // The only token: stripping it would leave nothing to search for.
    [InlineData("1917")]
    [InlineData("2012")]
    // Not a year at all.
    [InlineData("Se7en")]
    [InlineData("Ocean's 11")]
    public void Leaves_a_title_whole_when_the_number_is_not_a_year(string input)
    {
        var query = SearchQuery.Parse(input);

        Assert.Null(query.Year);
        Assert.Equal(input.Trim(), query.Title);
    }

    [Fact]
    public void Keeps_what_was_typed_for_the_tracker_to_narrow_on()
    {
        // The tracker matches the year itself, and a bare common word comes back against a
        // server-side cap, so the whole phrase is what it gets asked.
        var query = SearchQuery.Parse("Heat 1995");

        Assert.Equal("Heat 1995", query.Raw);
    }

    [Fact]
    public void Survives_an_empty_query()
    {
        var query = SearchQuery.Parse("   ");

        Assert.Equal("", query.Title);
        Assert.Null(query.Year);
    }
}
