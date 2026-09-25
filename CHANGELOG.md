# Changelog

## 0.19.0

Releases. Pushing a tag `v<version>` that matches the newest CHANGELOG entry builds the
`moviebot-acquire` CLI as one self-contained linux-x64 executable and publishes it, with its
settings file and a checksum, as `moviebot-acquire-linux-x64.tar.gz` on a GitHub release.
`scripts/package-release.sh` builds the same archive locally. CI builds and tests every push to
`main` and every pull request.

## 0.18.0

The tracker's category names are host configuration too. `Selection:AllowedCategories` has no
default, since the names are the tracker's own taxonomy, and `AddAcquire` validates it at startup:
an empty list filters nothing, so a host that forgot it would offer soundtracks beside the films
rather than fail. The tests use generic categories.

**Operational:** a host sets `Selection__AllowedCategories__0`, `__1` and so on in its environment
file before running this version.

## 0.17.0

Which tracker this is lives in the host's configuration, never in the repository. The client, its
wire shapes and its options are `TrackerClient`, `TrackerTorrent` and `TrackerOptions` in
`TheKrystalShip.MovieBot.Acquire.Tracker`, bound from the `Tracker` section. `Tracker:BaseUrl` is
the site's root and has no default; the API and the torrent download are both built under it, and a
client without it is not configured.

**Operational:** a host sets `Tracker__BaseUrl`, `Tracker__Username` and `Tracker__Passkey` in its
environment file before running this version.

## 0.16.2

A subtitle search survives a row the index left incomplete.

OpenSubtitles writes `null` for an uploader flag that was never set, and the wire shapes read those
flags as plain `bool`. A search is one document, so a single row with a null `from_trusted` failed
the whole response: a film got no subtitles at all because one stranger's upload said nothing about
itself, and the refusal reached the API as an unhandled exception rather than an empty list.

Every scalar on those shapes is nullable now, including the ones the service documents as always
present, and what each absence means is decided once where they are mapped: an unset flag is the
flag not set, an undeclared frame rate is zero, an undeclared disc count is one. A row naming no
file to fetch is still no candidate, tested positively because a nullable id is not less than one.

## 0.16.1

A person is not a film. The title index answers a search with people beside titles, and a person
carries no kind, which `ImdbTitle.IsFeature` read as a film: "Pirates of the Caribbean" listed Rex
Smith, an actor in The Pirates of Penzance, among the films. A missing kind now counts as a film only
under a title id.

## 0.16.0

A search names the film before it asks the tracker for it.

`IReleaseSearch.ByTextAsync` takes whatever somebody typed, identifies the film in the title
index, and asks the tracker for that film by id. The tracker names a film the way the country
that made it named it, so the English name of a film shot elsewhere matches no release at all:
"The Furious" finds nothing while `tt33311069` finds nine rows named `Huo.zhe.yan`. An id is the
one thing the two agree on however the film is spelled. Text carrying an id — a pasted link —
is a film already named and takes the same path without a search. Where the index cannot name a
film the words are used as before.

Naming the film first is also what makes a half-typed title work: the index answers a prefix,
being what a search box reads, where matching a release's own words cannot.

`RankedReleases.Film` carries the identified film so a row can show it beside the name the
release carries, and `AutocompleteSearch.SuggestAsync` returns `Suggestions` — the rows, and the
sentence saying why there are none.

## 0.15.0

A retention rule, the clock it runs on, and the tag that exempts a film from it.

`DownloadStatus.Seeded` is the client's own seeding time: it counts only while the client runs
and the torrent is active, and it survives restarts in the client's resume data, which makes it
the same clock a private tracker credits. `Retention` says whether a download has seeded for the
configured window and how long it has left, and never says yes below a floor of the tracker's
minimum plus a margin; `RetentionOptions` refuses a window under that floor at startup.
`TorrentTags.Keep` and `TorrentTags.Keeper` mark a film somebody wants to stay, and
`AcquisitionService.RemoveAsync` takes a download out of the client and off the disk through it.

## 0.14.0

Two more notes a torrent carries: the room a film is to play in, and the id it went under in the
library.

`TorrentTags.Room` names a voice channel. Somebody who picks a film that is not here yet has asked
to watch it, and the download is only the means, so the room rides on the torrent and whichever
pass sees the film become watchable loads it there. `TorrentTags.Library` is written by the process
that names the film in the library, so a surface opening it holds the fact rather than a second
parse of the release name.

## 0.13.0

An IMDb id is read out of whatever somebody pasted, and the catalogue suggests films as a person
types.

`ImdbId.FromText` takes the `tt` form from anywhere in a piece of text: the film's page, the mobile
site, a share link with tracking on the end, or the id on its own. A bare number is left alone,
because a bare number typed into a film search is a title.

`ImdbClient.SuggestAsync` answers a surface that suggests while somebody types, with the features
the index offers in its own order. Text carrying an id answers with the film the id names rather
than a search for the text of a link, which would find nothing.

## 0.12.0

A tag for a film being watchable, separate from a film owing a transcode.

One tag was answering both questions. It was cleared the moment a film became playable — seconds
into a transcode, so the announcement could go out — which left nothing to say that the hour of
work after that point was still owed. A process that stopped anywhere in that hour abandoned the
film: marked failed, never picked up again, and the log line claiming it stayed owed was untrue.

`NeedsIngest` now means what it says and is cleared when the transcode actually finishes.
`Watchable` is what a surface waits for before telling anybody a film is ready.

## 0.11.0

What a film is called, and what it looks like.

A release name is what a film arrives as and is not what it is called: it carries the year among
the resolution and the codec, keeps whichever edition words the packer felt like, loses every
apostrophe, and says nothing about who is in it or what its poster looks like. `ImdbClient` asks
the catalogue instead — by id where the tracker gave one, by name and year where it did not — and
answers with the name, the year, the top billing and the poster.

It reads the index that answers the catalogue's own search box, which is a JSON endpoint and not a
page. The title pages answer anything that is not a browser with an empty 202, so the only other
way in is behind a key, and a key would buy a plot and a rating and nothing else that is wanted.

A name and a year written together is now one thing, `Release.Display`, rather than three: written
out separately in each place a film is shown, it is how the message that starts a download and the
message that says it is ready end up naming the same film differently.

## 0.10.0

A tag naming which film a download is, as the tracker identified it.

The tracker states the IMDb id outright on every row it returns. Recording it at the moment a
download starts is the only chance to keep a fact: everything downstream would otherwise have to
re-derive the film from a release name, which is a guess.

## 0.9.0

A client for the subtitle index, and the judgement needed to spend its allowance well.

Searching is free and downloading is not, so everything that can be decided before a download is
decided first: whether a subtitle was indexed against this exact file, whether its frame rate
matches, and whether it came off the same kind of source. Each answer is one of three rather than
two, because most uploads declare no frame rate and almost none are indexed against a particular
file, and reading that silence as a fault would condemn nearly every subtitle there is.

The strongest check is not a comparison of what an uploader typed but a measurement. Where a film
already carries a subtitle known to fit it, a candidate's cue timings are measured against it and
the offset between them is found by consensus. A subtitle whose timing stretches rather than
shifts has no single offset that fits, so none is reported: failing to align is the useful answer.

Character encoding is deliberately not among the checks. It says nothing about whether a subtitle
fits the film and it is repaired on the way in, so showing it would steer people away from
subtitles that are good.

### Fixed

- A DVDRip release parses as a DVD source. Its resolution was read from that name but its source
  was not, so every DVDRip came through as an unknown source and ranked as though nothing were
  known about it.

## 0.8.0

The seed count a download is connected to, and a tag naming the message that shows its progress.

The seed count is carried because it is the answer to the only question a slow download raises: a
download sitting at a third for ten minutes is either working or abandoned, and nothing else
tells the two apart.

## 0.6.0

How much of a file in a download can be read from its start without a gap, for anything reading
it while it is still arriving. The file is its full length from the moment it is created, so its
size says nothing about how much of it is real.

`get` tags what it starts for ingest, so a film fetched from the shell takes the same path as one
asked for in chat.

## 0.5.0

The tag vocabulary processes use to leave notes on a torrent for each other, and the ability to
add one as well as remove it.

It lives in this library because more than one process acts on the same download, and two of them
spelling the same idea differently fails silently: nothing throws, the tag is simply never seen
and the work never happens.

## 0.4.0

Torrents carry tags, so a surface that announces a finished download can keep its note on the
torrent rather than in its own memory, and survive a restart mid-download.

## 0.3.0

Downloading: the torrent client over its Web API, the acquisition path from a chosen release to a
started download, and `get` and `downloads` on the CLI.

Torrents are added with sequential order and first-and-last-piece priority, so a partial file is
watchable from the beginning rather than only once it finishes.

## 0.2.0

Autocomplete suggestions for a slash command: a debounced, keystroke-safe layer over the search,
and labels built to fit a chat surface's length limit.

The pacing floor between tracker calls drops to half a second. It is charged to whoever is queued
behind a call, and above the autocomplete deadline it answers a second person searching with
nothing every time.

## 0.1.0

Tracker search: the member API client, release-name parsing, the selection policy and the
ranker, the disk budget, and the `moviebot-acquire` CLI over them.

The tracker's member API replaces the scraping this would otherwise need, so there is no login
or HTML parsing anywhere in the project.
