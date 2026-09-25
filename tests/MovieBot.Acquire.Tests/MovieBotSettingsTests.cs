using Microsoft.Extensions.Configuration;
using TheKrystalShip.MovieBot.Acquire.Configuration;
using Xunit;

namespace TheKrystalShip.MovieBot.Acquire.Tests;

public sealed class MovieBotSettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("moviebot-settings-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string configRoot, string json = "{}")
    {
        var directory = Path.Combine(_root, configRoot, MovieBotSettings.DirectoryName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, MovieBotSettings.FileName);
        File.WriteAllText(path, json);
        return path;
    }

    private Func<string, string?> Environment(params (string Key, string Value)[] variables)
    {
        var map = variables.ToDictionary(v => v.Key, v => v.Value);
        return key => map.GetValueOrDefault(key);
    }

    [Fact]
    public void The_config_home_is_searched_before_the_config_dirs()
    {
        var home = Write("home");
        Write("system");

        var found = MovieBotSettings.Locate(Environment(
            ("XDG_CONFIG_HOME", Path.Combine(_root, "home")),
            ("XDG_CONFIG_DIRS", Path.Combine(_root, "system"))));

        Assert.Equal(home, found);
    }

    [Fact]
    public void Without_XDG_CONFIG_HOME_the_home_directory_config_is_searched()
    {
        var file = Write(Path.Combine("user", ".config"));

        var found = MovieBotSettings.Locate(Environment(
            ("HOME", Path.Combine(_root, "user")),
            ("XDG_CONFIG_DIRS", Path.Combine(_root, "nothing-here"))));

        Assert.Equal(file, found);
    }

    [Fact]
    public void The_config_dirs_are_searched_in_order()
    {
        Write("second");
        var first = Write("first");

        var found = MovieBotSettings.Locate(Environment(
            ("HOME", Path.Combine(_root, "no-home")),
            ("XDG_CONFIG_DIRS", $"{Path.Combine(_root, "first")}:{Path.Combine(_root, "second")}")));

        Assert.Equal(first, found);
    }

    [Fact]
    public void A_relative_path_is_ignored_as_the_specification_requires()
    {
        Assert.Null(MovieBotSettings.ConfigHome(Environment(("XDG_CONFIG_HOME", "relative/config"))));
        Assert.Equal(
            Path.Combine(_root, ".config"),
            MovieBotSettings.ConfigHome(Environment(("XDG_CONFIG_HOME", "relative"), ("HOME", _root))));
    }

    [Fact]
    public void Without_XDG_CONFIG_DIRS_the_system_directory_is_etc_xdg()
    {
        var paths = MovieBotSettings.SearchPaths(Environment(("HOME", _root))).ToList();

        Assert.Equal(Path.Combine(_root, ".config", "moviebot", "moviebot.settings.json"), paths[0]);
        Assert.Equal("/etc/xdg/moviebot/moviebot.settings.json", paths[1]);
    }

    [Fact]
    public void No_file_anywhere_is_no_host_file()
    {
        Assert.Null(MovieBotSettings.Locate(Environment(
            ("XDG_CONFIG_HOME", Path.Combine(_root, "empty")),
            ("XDG_CONFIG_DIRS", Path.Combine(_root, "also-empty")))));
    }

    [Fact]
    public void Sources_added_by_the_host_override_the_settings_file()
    {
        var file = Write("home", """
            {
              // Comments are allowed: the file is meant to be read and edited by a person.
              "Download": { "Root": "/from/the/file", "MaximumGiB": 100 }
            }
            """);
        System.Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_root, "home"));
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Download:MaximumGiB"] = "42" })
                .AddMovieBotSettings()
                .Build();

            Assert.Equal(file, MovieBotSettings.Locate());
            Assert.Equal("/from/the/file", configuration["Download:Root"]);
            Assert.Equal("42", configuration["Download:MaximumGiB"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        }
    }
}
