using TheKrystalShip.MovieBot.Acquire.Tmdb;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public class WeightedRatingTests
{
    private static ListedTitle Rated(string name, double? rating, int votes) =>
        new() { TmdbId = name.GetHashCode(), Kind = "film", Title = name, Rating = rating, Votes = votes };

    [Fact]
    public void A_title_few_people_rated_does_not_outrank_one_many_rated_a_little_lower()
    {
        var ordered = WeightedRating.Order([
            Rated("Niche", 8.4, 268),
            Rated("Famous", 7.6, 19000),
            Rated("Known", 7.8, 9000),
            Rated("Middling", 6.9, 6000),
            Rated("Seen", 7.0, 5000),
        ]);

        Assert.Equal(["Known", "Famous", "Niche", "Seen", "Middling"], ordered.Select(t => t.Title));
    }

    [Fact]
    public void With_equal_weight_the_higher_average_leads_and_ties_go_to_the_more_voted()
    {
        var ordered = WeightedRating.Order([Rated("A", 7.0, 1000), Rated("B", 8.0, 1000), Rated("C", 8.0, 2000)]);

        Assert.Equal(["C", "B", "A"], ordered.Select(t => t.Title));
    }

    [Fact]
    public void A_title_nobody_rated_comes_last_and_a_set_with_none_rated_is_left_alone()
    {
        Assert.Equal(["Rated", "Unrated"], WeightedRating.Order([Rated("Unrated", null, 0), Rated("Rated", 5.0, 10)]).Select(t => t.Title));
        Assert.Equal(["B", "A"], WeightedRating.Order([Rated("B", null, 0), Rated("A", null, 0)]).Select(t => t.Title));
    }
}
