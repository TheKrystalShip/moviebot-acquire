using TheKrystalShip.MovieBot.Acquire.Download;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

/// <summary>
/// Resolving the download root reads the environment, so these run alone: two of them swapping
/// the same variable concurrently would each see the other's value.
/// </summary>
[CollectionDefinition("Environment", DisableParallelization = true)]
public class EnvironmentCollection;

[Collection("Environment")]
public class DownloadOptionsTests
{
    private const string Variable = "XDG_DOWNLOAD_DIR";

    private static T WithDownloadDir<T>(string? value, Func<T> body)
    {
        var original = Environment.GetEnvironmentVariable(Variable);
        try
        {
            Environment.SetEnvironmentVariable(Variable, value);
            return body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, original);
        }
    }

    [Fact]
    public void An_explicit_root_wins_over_the_environment()
    {
        // This is what a service sets, and it has to beat whatever the environment happens to
        // say: a unit inherits none of a session's variables and must not depend on them.
        var options = new DownloadOptions { Root = "/srv/films" };

        var resolved = WithDownloadDir("/somewhere/else", options.ResolveRoot);

        Assert.Equal("/srv/films", resolved);
    }

    [Fact]
    public void The_xdg_variable_is_used_when_there_is_no_explicit_root()
    {
        var options = new DownloadOptions();

        var resolved = WithDownloadDir("/home/someone/Downloads", options.ResolveRoot);

        Assert.Equal(Path.Combine("/home/someone/Downloads", "Movies"), resolved);
    }

    [Fact]
    public void A_relative_xdg_value_is_ignored_rather_than_resolved_against_nothing()
    {
        // The variable is specified as absolute. A relative one would otherwise resolve against
        // the process's working directory, which for a service is not anywhere useful.
        var options = new DownloadOptions();

        var resolved = WithDownloadDir("Downloads", options.ResolveRoot);

        Assert.True(Path.IsPathRooted(resolved));
        Assert.DoesNotContain(Path.Combine("Downloads", "Movies"), resolved[..^"Movies".Length]);
    }

    [Fact]
    public void The_folder_under_the_downloads_directory_is_configurable()
    {
        var options = new DownloadOptions { FolderName = "Films" };

        var resolved = WithDownloadDir("/home/someone/Downloads", options.ResolveRoot);

        Assert.Equal(Path.Combine("/home/someone/Downloads", "Films"), resolved);
    }

    [Fact]
    public void Resolving_always_yields_an_absolute_path()
    {
        // Everything downstream joins onto this, so a relative answer would put films wherever
        // each process happened to be started from.
        Assert.True(Path.IsPathRooted(WithDownloadDir(null, new DownloadOptions().ResolveRoot)));
    }
}
