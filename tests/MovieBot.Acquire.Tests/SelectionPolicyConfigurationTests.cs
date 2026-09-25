using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheKrystalShip.MovieBot.Acquire.Search;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public class SelectionPolicyConfigurationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings) =>
        new ServiceCollection()
            .AddLogging()
            .AddAcquire(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();

    [Fact]
    public void A_host_naming_no_category_is_refused()
    {
        using var services = Build([]);

        var refusal = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<SelectionPolicy>());
        Assert.Contains("Selection:AllowedCategories", refusal.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_category_is_refused(string blank)
    {
        using var services = Build(new()
        {
            ["Selection:AllowedCategories:0"] = "Movies HD",
            ["Selection:AllowedCategories:1"] = blank,
        });

        var refusal = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<SelectionPolicy>());
        Assert.Contains("empty entry", refusal.Message);
    }

    [Fact]
    public void The_categories_come_from_the_host_configuration()
    {
        using var services = Build(new()
        {
            ["Selection:AllowedCategories:0"] = "Movies HD",
            ["Selection:AllowedCategories:1"] = "Movies 4K",
            ["Selection:MaximumResults"] = "5",
        });

        var policy = services.GetRequiredService<SelectionPolicy>();

        Assert.Equal(["Movies HD", "Movies 4K"], policy.AllowedCategories);
        Assert.Equal(5, policy.MaximumResults);
    }
}
