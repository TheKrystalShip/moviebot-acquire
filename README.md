# moviebot-acquire

Finds films on a private tracker and downloads them in a form that can be watched while they
arrive. It is the acquiring half of [MovieBot](https://github.com/TheKrystalShip/moviebot): this
library searches the tracker, drives the torrent client, looks films up, finds subtitles and
manages disk, and MovieBot takes what lands on disk and plays it to a Discord voice channel.

It is a .NET library with a small CLI on top. MovieBot's services reference the library by
relative path, so the two repositories are checked out side by side and move together.

## Features

- **Tracker search and ranking.** Queries are parsed for a title and year, matched against each
  release's own title, filtered by a selection policy and ranked, with every rejection counted.
- **Autocomplete.** A search built for a chat surface's suggestion list, which answers every
  keystroke while keeping a typed query to roughly one tracker call.
- **Downloads that can be watched early.** Torrents are added to qBittorrent sequentially with
  first-and-last-piece priority, and the library reports how much of a file is readable from its
  start without a gap.
- **Film lookup.** Names, years, billing and posters by IMDb id, from IMDb's public suggestion index.
- **Subtitles.** Search and download through the OpenSubtitles API, with checks that a subtitle
  fits the film it is for.
- **Disk budget and retention.** A ceiling on the download directory, and a seeding window after
  which a film nobody has kept is let go.

## Requirements

- .NET 10 SDK
- An account on a tracker that exposes a member API (username and passkey)
- qBittorrent with its Web API enabled on loopback, set to let local requests through without
  authentication
- An OpenSubtitles API key, for subtitles only

## Getting started

Each [release](https://github.com/TheKrystalShip/moviebot-acquire/releases) carries the CLI as one
self-contained linux-x64 executable, in `moviebot-acquire-linux-x64.tar.gz`. MovieBot's own
releases include everything MovieBot needs from this library, so deploying MovieBot does not need
this download.

To build from source:

```bash
dotnet build moviebot-acquire.slnx -c Release
dotnet test
```

The CLI is the library's own surface, and the way to exercise the tracker without Discord:

```bash
moviebot-acquire search Heat 1995        # rank what the tracker has
moviebot-acquire imdb tt0113277          # the same, by exact film
moviebot-acquire film Heat               # what the title index calls a film
moviebot-acquire get Heat 1995           # start the top result downloading
moviebot-acquire get Heat 1995 --pick 4  # or the fourth
moviebot-acquire downloads               # what is downloading now
moviebot-acquire budget                  # what the download directory holds
```

The binary is built to `src/MovieBot.Acquire.Cli/bin/Release/net10.0/moviebot-acquire`, or run it
with `dotnet run --project src/MovieBot.Acquire.Cli -c Release -- <verb> ...`. Results go to stdout
and everything else to stderr, so a search is pipeable.

## Configuration

The tracker is configured entirely by the host, in the environment or in user-secrets, and never
from a file in this repository:

| Setting | What it is |
|---|---|
| `Tracker__BaseUrl` | The tracker's root URL |
| `Tracker__Username` | The account name |
| `Tracker__Passkey` | The account's passkey |
| `Selection__AllowedCategories__0`, `__1`, … | The tracker's category names a film may come from. Required: a host without them refuses to start |
| `OpenSubtitles__ApiKey` | The subtitle index's API key |

The passkey authenticates the API and is also embedded in every download URL the tracker returns,
so it identifies the account in anything downloaded with it. It is held by `TrackerClient` alone:
a `Release` carries a torrent id rather than a download link, so nothing that reaches a log or a
chat surface ever contains it.

Everything else is read from `moviebot.settings.json`, the settings file MovieBot and this CLI
share. The CLI looks for it at `$XDG_CONFIG_HOME/moviebot/moviebot.settings.json` (by default
`~/.config/moviebot/`), then under `moviebot/` in each of `$XDG_CONFIG_DIRS` (by default
`/etc/xdg`). Run as the account MovieBot's services use, it therefore reads their settings. Any key
the file leaves out takes the library's default:

```json
"Selection": {
  "MaximumResolution": "Hd1080",
  "PreferredResolution": "Hd1080",
  "MaximumSizeGiB": 25,
  "MaximumResults": 10,
  "TierByTrackerFlags": true
},
"Download": {
  "Root": "",
  "MaximumGiB": 300,
  "WarningGiB": 250
},
"QBittorrent": {
  "BaseUrl": "http://127.0.0.1:8080",
  "Category": "moviebot",
  "SequentialDownload": true,
  "FirstLastPiecePriority": true
}
```

The environment overrides any single key, written `Section__Key` (`Download__MaximumGiB=500`).
An empty `Download.Root` means `~/Downloads/Movies`. Retention is set under `Retention`:
`SeedDays` (default 7), and the tracker's minimum seeding time `TrackerMinimumHours` (default 48)
plus a safety `MarginHours` (default 12), below which nothing is ever removed.

## How it works

### Tracker access

The tracker publishes a member API, so there is no scraping: no HTML parser, no login, no cookie
jar. Every call is one GET carrying the username and passkey. Calls are paced to stay under the
tracker's rate cap, and search results are cached briefly so the same query from two people is one
call.

### Search

- **A year is read out of the query and used twice.** The whole phrase goes to the tracker, which
  narrows on it, and the year is then checked against each release's own year. A four-digit number
  counts as a year only if a film could have been released in it and the title survives without
  it, which keeps `Blade Runner 2049` and `1917` whole.
- **Titles are compared for equality once edition words are stripped**, not by containment, so
  `Dead Heat` does not match `Heat` and `Dune Part Two` does not match `Dune`.
- **1080p is a ceiling, not a preference.** MovieBot transcodes to H.264 at a fixed bitrate and
  tone-maps HDR to SDR, so a 2160p source plays no better than a 1080p one and costs several times
  the download, disk and transcode time.
- **Freeleech first, then internal releases, then the rest**, with encode quality deciding within
  each group. Freeleech costs the account nothing against its ratio.
- **Rejections are counted, never dropped**, so a search that finds releases and offers none says
  why instead of looking like a failed call.

### Autocomplete

`AutocompleteSearch` answers a slash command's suggestion list, which is requested on every
keystroke and must answer within a few seconds:

- Nothing under three characters is looked up.
- A query extending one already answered is filtered locally, since the tracker's results for a
  longer query are a subset of a shorter one's.
- Anything else waits for typing to stop, per person, and a newer keystroke supersedes an older one.
- One tracker call is in flight at a time, and a second person queues behind the first rather than
  getting nothing.

Suggestion labels are built to fit the surface's hundred-character limit, keeping title and year
whole, and the value behind each row is a torrent id.

### Downloading

qBittorrent is driven over its Web API on loopback. Torrents are added **sequentially with
first-and-last-piece priority**: sequential order alone leaves a file that is complete from the
start and still will not open, because a container keeps its index at one end or the other.
Together they make a partial file playable.

The info hash is computed from the torrent file rather than read back from the client, since the
add endpoint does not say what it added. Disk space is checked before the tracker is asked, and the
torrent file is fetched before the client is asked to take it, so a failure leaves nothing half
started.

`ReadableBytesAsync` reports how many bytes of a file are readable from its start **without a
gap**. That is the only useful measure for something reading the file forwards, and it is asked
rather than assumed, because pieces can arrive out of order even in a sequential download.

### Disk and retention

The disk budget is measured from the filesystem on every call rather than kept as a running total,
so a film deleted by hand is accounted for immediately.

A finished download keeps seeding. Retention measures seeding on the client's own clock, which is
the time the tracker credits, and a film becomes due for removal once it has seeded for the full
window, never below the tracker's minimum plus the margin. A film can be kept, which is a tag on its
torrent. Pruning itself is done by MovieBot's hand-off service, which also knows which films are
being watched or transcoded.

## Repository layout

```
src/MovieBot.Acquire/          the library
  Tracker/                     the tracker's member API, and the only holder of the passkey
  Search/                      query parsing, release parsing, selection, ranking, autocomplete
  Configuration/               where moviebot.settings.json lives, and how it is layered
  Download/                    qBittorrent client, disk budget, retention, torrent tags
  Imdb/                        film lookup by IMDb id
  Subtitles/                   OpenSubtitles client and subtitle checks
src/MovieBot.Acquire.Cli/      the moviebot-acquire CLI
tests/MovieBot.Acquire.Tests/  unit tests
scripts/                       release packaging, version
```

The parser tests run against release names the tracker actually returned.

## Releases

Pushing a tag `v<version>` that matches the newest entry in `CHANGELOG.md` builds the CLI with
`scripts/package-release.sh` and publishes it as a GitHub release. Every push to `main` and every
pull request is built and tested.

## License

[GPL-3.0-or-later](LICENSE).
