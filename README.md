# moviebot-acquire

Finds films on a private tracker and fetches them to disk. It is the acquiring half of the
MovieBot pipeline: this reaches the tracker, the torrent client and the title index, and MovieBot
takes what lands on disk and makes it watchable. MovieBot compiles against this checkout, so the
two move together.

## Status

| Piece | State |
|---|---|
| Tracker search, parsing and ranking | built, verified against the live tracker |
| `moviebot-acquire` CLI — `search`, `imdb`, `film`, `get`, `downloads`, `budget` | built |
| Autocomplete suggestions, for a slash command | built |
| Disk budget | built |
| Torrent client, and the download itself | built, verified against a real torrent |
| Title index, for what a film is called | built |
| Retention, and keeping a film | built |
| Hand-off to MovieBot ingest | built, and running as `moviebot-handoff` |

## Why there is no scraping in here

The tracker publishes a member API, so this holds no HTML parser, no login, no cookie jar and no
CSRF token. Every call is one GET carrying a username and a passkey, and nothing breaks when the
site is restyled.

## Configuration

The account is configured through `Tracker__Username` and `Tracker__Passkey`, in the
environment or in user-secrets. Neither is ever read from a file in this repository.

The passkey is a credential twice over: it authenticates the API, and the tracker embeds it in
every download URL it hands back, which is what makes a leak attributable to the account. It
lives in `TrackerClient` and nowhere else — a `Release` carries a torrent id instead of a
download link, so nothing that reaches a chat surface or a log has ever held it.

Everything else is in `appsettings.json`:

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
}
```

`Download.Root` empty means `~/Downloads/Movies`.

## Use

```bash
dotnet build moviebot-acquire.slnx -c Release

moviebot-acquire search Heat 1995      # rank what the tracker has
moviebot-acquire imdb tt0113277        # the same, by exact film
moviebot-acquire get Heat 1995         # start the top result downloading
moviebot-acquire get Heat 1995 --pick 4  # or the fourth
moviebot-acquire downloads             # what is downloading now
moviebot-acquire budget                # what the download directory holds
```

Results go to stdout and everything else to stderr, so a search is pipeable.

## What the search does

**A year is read out of the query and used twice.** People search by title and usually a year.
The whole phrase goes to the tracker, because it narrows on the year itself and a bare common
word comes back against a server-side cap. The year is then applied again to each release's own
year, since matching words is not the same as matching films.

A four-digit number is only read as a year if a film could have been released in it and the
title survives without it. That is what keeps `Blade Runner 2049` and `1917` whole.

**Titles are compared once editions are stripped.** The tracker returns anything sharing a token
with the query, so a search narrowed by a year still carries back other films from that year. The
comparison is equality after removing trailing edition words, not containment: containment reads
`Dead Heat` as `Heat` and `Dune Part Two` as `Dune`.

**1080p is a ceiling, not a preference.** MovieBot transcodes to H.264 at a fixed bitrate and
tone-maps HDR to SDR, so a 2160p source reaches the player as the same rendition a 1080p source
does, having cost several times the download, the disk it is then seeded from, and a slower
transcode.

**Freeleech first, then internal, then the rest**, with the encode deciding only within each
group. Freeleech costs the account nothing against its ratio, and a private tracker expects a
completed download to keep seeding.

**Rejections are counted, never dropped.** A search that finds a dozen releases and offers none
is a normal outcome, and a menu that simply comes back empty is indistinguishable from a broken
tracker call.

## Autocomplete

`AutocompleteSearch` serves a slash command's suggestion list. A chat surface sends a request on
every character typed and expects an answer within a few seconds, so answering each one against
the tracker is both far past what it permits and slower than the typing.

Four things keep a typed query to roughly one call:

- Nothing under three characters is looked up.
- **A query extending one already answered is filtered locally.** The tracker narrows on the
  words it is given, so a longer query's results are a subset of a shorter one's. This costs no
  call and so does not wait — results already held appear as they are typed past.
- **Anything left waits for typing to stop**, half a second by default. A keystroke arriving
  inside the window supersedes the one before it, which shows what is known rather than asking.
  The debounce is per person: one person mid-word must not suppress another's finished query.
- **One call is in flight at a time**, and a second person queues behind the first for as long
  as the deadline allows rather than being answered with nothing.

A row's label is built to fit the surface's hundred-character limit rather than trimmed to it:
the title and year are kept whole and the description gives way, because two rows differing only
in a cut off description are two rows nobody can choose between. The value behind a row is a
torrent id, since a truncated release name cannot be resolved back to a release.

## Downloading

The torrent client is qBittorrent, driven over its Web API on loopback, where it is configured to
let a local request through without credentials. Nothing here holds a password for it.

**Torrents are added sequentially, with first-and-last-piece priority.** The two are separate and
both are needed: sequential order alone leaves a file that is complete from the beginning and
still will not open, because a container keeps the index a player needs at one end or the other.
Together they make a partial file watchable.

**The info hash is computed from the torrent file, not read from a response.** The add endpoint
answers "Ok." and does not say what it added, and diffing the torrent list before and after
attributes the wrong one whenever two downloads start close together.

**Disk is checked before the tracker is asked.** The alternative spends a tracker call and a
torrent file to then refuse. The torrent is fetched before the client is asked to take it, so a
client that is not running leaves no half-started download behind.

## Reading a download that is still arriving

`ReadableBytesAsync` reports how many bytes of one file are readable from its start **without a
gap**. Contiguous is the only useful measure, because whatever reads the file reads it forwards:
a piece that arrived beyond a missing one is not reachable, and treating overall progress as a
position hands out an offset with a hole behind it.

Sequential downloads fill contiguously by construction, but this asks rather than assumes — a
piece can arrive out of order regardless, as an endgame duplicate or a prioritised first-and-last
piece, and being wrong means reading unwritten file.

## Disk

A completed download keeps seeding, so nothing here is deleted on a schedule and the directory
only grows until somebody removes a film. The budget is measured off the filesystem on every
call rather than accumulated, because a running total drifts silently the moment a film is
deleted by hand.

## Tests

```bash
dotnet test
```

The parser tests run against release names the tracker actually returned.

## Licence

GPL-3.0-or-later.
