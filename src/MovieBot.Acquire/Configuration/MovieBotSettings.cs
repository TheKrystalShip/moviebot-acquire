using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace TheKrystalShip.MovieBot.Acquire.Configuration;

/// <summary>
/// Where the MovieBot pipeline's settings file lives, and how it is layered into a program's
/// configuration. Every MovieBot service and the moviebot-acquire CLI read the same file, found by
/// the same rules, so a knob set once is seen by every process that has it.
///
/// Two copies take part, lowest precedence first:
/// <list type="number">
/// <item>The copy shipped beside the program, holding every default. It is replaced on every
/// upgrade and is not meant to be edited.</item>
/// <item>The host's copy in the XDG configuration directory: the first
/// <c>moviebot/moviebot.settings.json</c> found under <c>$XDG_CONFIG_HOME</c> (by default
/// <c>~/.config</c>), then under each entry of <c>$XDG_CONFIG_DIRS</c> (by default
/// <c>/etc/xdg</c>). This is the file a deployment edits.</item>
/// </list>
/// Both sit underneath everything else a host adds, so the environment, and the environment file a
/// systemd unit reads, override single keys of either: that is where secrets go, and anything a
/// host wants to change without touching the file.
///
/// The file may carry <c>//</c> comments, which the JSON configuration provider skips.
/// </summary>
public static class MovieBotSettings
{
    public const string FileName = "moviebot.settings.json";

    /// <summary>The directory under an XDG configuration root that holds <see cref="FileName"/>.</summary>
    public const string DirectoryName = "moviebot";

    /// <summary>The copy shipped beside the running program.</summary>
    public static string ShippedPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// Layers the shipped defaults and the host's settings file underneath every source already
    /// added, in that order.
    /// </summary>
    public static IConfigurationBuilder AddMovieBotSettings(this IConfigurationBuilder configuration)
    {
        if (Locate() is { } hostFile)
            configuration.Sources.Insert(0, JsonFile(hostFile));

        configuration.Sources.Insert(0, JsonFile(ShippedPath));
        return configuration;
    }

    /// <summary>
    /// Which files a program read its settings from, for the line it logs at startup: the answer to
    /// "why is it not using what I wrote" is nearly always a file in a directory it never looked in.
    /// </summary>
    public static string Describe() =>
        Locate() is { } hostFile
            ? $"{ShippedPath}, overridden by {hostFile}"
            : $"{ShippedPath} only; no host settings file at "
              + string.Join(" or ", SearchPaths(Environment.GetEnvironmentVariable));

    /// <summary>The host's settings file, or null when the host has none.</summary>
    public static string? Locate() => Locate(Environment.GetEnvironmentVariable);

    /// <summary>
    /// The first settings file that exists among <see cref="SearchPaths"/>. The first found is the
    /// whole of the host's file: a copy under <c>/etc/xdg</c> does not merge into one in the home.
    /// </summary>
    public static string? Locate(Func<string, string?> environment) =>
        SearchPaths(environment).FirstOrDefault(File.Exists);

    /// <summary>Every place the host's settings file is looked for, most specific first.</summary>
    public static IEnumerable<string> SearchPaths(Func<string, string?> environment)
    {
        if (ConfigHome(environment) is { } home)
            yield return Path.Combine(home, DirectoryName, FileName);

        foreach (var directory in ConfigDirs(environment))
            yield return Path.Combine(directory, DirectoryName, FileName);
    }

    /// <summary>
    /// <c>$XDG_CONFIG_HOME</c>, or <c>$HOME/.config</c> when it is unset. A relative value is
    /// invalid under the specification and is ignored. Null when neither yields an absolute path,
    /// which is a process with no home at all.
    /// </summary>
    public static string? ConfigHome(Func<string, string?> environment)
    {
        if (environment("XDG_CONFIG_HOME") is { Length: > 0 } configHome && Path.IsPathRooted(configHome))
            return configHome;

        return environment("HOME") is { Length: > 0 } home && Path.IsPathRooted(home)
            ? Path.Combine(home, ".config")
            : null;
    }

    /// <summary><c>$XDG_CONFIG_DIRS</c> in order, or <c>/etc/xdg</c> when it names nothing usable.</summary>
    private static IEnumerable<string> ConfigDirs(Func<string, string?> environment)
    {
        var dirs = (environment("XDG_CONFIG_DIRS") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Path.IsPathRooted)
            .ToList();

        return dirs.Count > 0 ? dirs : ["/etc/xdg"];
    }

    /// <summary>
    /// A file read once at startup. No reload: a watcher costs an inotify instance per process, and
    /// every setting here is read when the service starts.
    /// </summary>
    private static JsonConfigurationSource JsonFile(string path) => new()
    {
        FileProvider = new PhysicalFileProvider(Path.GetDirectoryName(path)!),
        Path = Path.GetFileName(path),
        Optional = true,
        ReloadOnChange = false
    };
}
