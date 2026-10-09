# Changelog

## 0.37.0

A discovery sorted by rating reads the five pages of the most voted matches and orders them with
`WeightedRating`, a rating weighted by votes: each average counts in proportion to its votes and the rest
of the weight goes to the vote-weighted mean of the set, with the votes of the set's lower quartile as
the prior. The database's own order by average puts a title a few hundred people rated above the ones
everybody has seen. `DiscoverQuery.WithoutGenreIds` leaves out the titles filed under any of the genres.

## 0.36.0

`TmdbClient` finds as well as describes. `SearchTitlesAsync` searches films and shows by name,
`SearchPeopleAsync` a person by name with what they are known for, `FilmographyAsync` lists every title
a person is credited on once, with everything they did on it and appearances as oneself marked, and
`DiscoverAsync` takes a `DiscoverQuery` of genres, release window, people, keywords, original language,
rating and vote floors and a sort. `SimilarAsync` lists the titles the database recommends and counts as
similar to one, `FindAsync` names the title an IMDb id is, `SearchKeywordsAsync` and `LanguagesAsync`
resolve a keyword and a language, `CardAsync` gives a title's IMDb id, genres, makers and top-billed
actors, and `TraitsAsync` its keyword and person ids. Lists name a title by `ListedTitle`. A description
carries `Keywords`, and `KeywordNames` marks a title the database tags black and white. A request the
database refuses for being one too many is asked again after the wait it names, three times at most.

## 0.35.0

`OpenSubtitlesClient.SearchByEpisodeAsync` finds one episode's subtitles by its show's IMDb id and its
season and episode (`parent_imdb_id`, `season_number`, `episode_number`), and leaves out anything the
index answers for another episode. A show's own id names every episode and special it has, so a search
by it alone offered subtitles from anywhere in the show. `OpenSubtitlesFeature` carries
`ParentImdbId`, `SeasonNumber` and `EpisodeNumber`.

## 0.34.0

`TmdbClient.PersonAsync` answers a person by the database's id for them, with the films and shows they
are credited on (`PersonCredits`, each a `CatalogueTitle` with the part played or the job done), most
voted on first: an appearance as themselves and any on a talk, news or reality show are left out, and a
title acted in and directed is one entry with both. `GenresAsync` lists the database's genres for films
or for shows, and `GenreAsync` a genre's most popular titles with at least `GenreVotesAtLeast` votes,
leaving talk, news and reality out of a genre of shows. Each title is named by its IMDb id, asked for
eight at a time. A description's `CastMember` carries the person's `TmdbId`, and `FilmDetails` and
`ShowDetails` carry `PersonIds`, each director's, writer's and creator's id by name.
`TmdbTrendingPage` and `TmdbTrendingItem` are `TmdbTitlePage` and `TmdbListedTitle`, which the
genre lists and a person's credits are read as too.

## 0.33.0

`TmdbClient.GlanceAsync` answers a film or show by its IMDb id at a glance, in one call: the database's
id and kind, rating, votes, overview, release date and poster path (`TitleGlance`).
`KeptConnections.KeepConnectionsOpen` gives an HTTP client one handler for the life of the process and
HTTP/2 where the server speaks it, pinging every thirty seconds so an idle connection stays open for
half an hour; `ImdbClient` is registered with it, and `TmdbClient` sends its
requests at the client's version so it reaches HTTP/2 too.

## 0.32.0

A requester tag names the account an identity provider knows the person by, a string:
`TorrentTags.Requester(string)` writes it with a comma and the percent sign percent-encoded, and
`ReadRequester` answers the string. `TorrentTags.Mention` is the Discord user the bot mentions when it
announces a film in a channel, written by the bot and read by it alone. `TorrentTags.TellDirect` is
gone: nobody is sent a Discord message for a download.

## 0.31.0

`DiskBudget` is the one account of what is promised on a volume. A running transcode holds a claim in
the `TranscodeLedger`, files under `.transcode-reservations` in the media root written by the
hand-off, and a download's headroom subtracts what each claim on its volume has still to write.
`DiskBudget.ReadVolume` gives a transcode's placement any volume's free space less the claims on it
and, where the downloads share it, the downloads still arriving; `Volumes.MountOf` names the volume a
path is on. A ledger that cannot be read or written is a logged warning and counts as no claims.

## 0.30.0

`TmdbClient.TrendingFilmsAsync` and `TrendingShowsAsync` answer the film database's trending films and
shows this week, most first, each a `TrendingTitle` named by its IMDb id with its title, year, rating
and poster path. An entry the database holds no IMDb id for, and an adult one, is left out.

## 0.29.0

A show's description carries the episode airing next and the one aired last
(`ShowDetails.NextEpisodeToAir`, `LastEpisodeToAir`, each an `AiringEpisode` with its season, number,
name and air date), read from the film database's `next_episode_to_air` and `last_episode_to_air`.

## 0.28.0

A watch tag names the episode a room waits on, `watch:<room>:s05e03`, since one torrent holds a
season and another episode of it may be watchable already: `TorrentTags.Watch(room, episode)` writes
it and `ReadWatch` answers a `WatchTag` with the room and the episode. Picked episodes are fetched
first-first: the earliest at the client's highest file priority, the next at its second, the rest at
normal in the torrent's order.

## 0.27.1

The CLI's `search` and `imdb` list every release they turn down, with the reason and the category the
tracker filed it under, on stderr: a film offered nothing because of its category is settled in the
host's category list, and this is where the category is read.

## 0.27.0

`TorrentTags.Watch` names the site room a download is to play in once it can be watched, the site's
counterpart of `room:`; `ReadWatch` reads it back and `IsRoomId` says which room ids a tag can carry
(1 to 64 letters, digits, `-` or `_`).

## 0.26.0

`TorrentTags.Tell` marks a download whose requester is owed news once it can be watched, or once it
could not be made watchable; `TorrentTags.TellDirect` beside it says no channel will announce it, so
the requester is told directly. Whoever tells them takes both off.

## 0.25.0

Shows. A release whose name carries a season marker (`EpisodeMarker`: `S02`, `S02E04`,
`S02E04E05`, `S01-S03`) is a show's: `Release.Episodic`, `IsEpisodic`, `EpisodeCount` and
`SizePerEpisode` describe it, and its title ends at the marker. `IShowSearch.ByShowAsync` searches
by the show's IMDb id and `ReleaseRanker.RankShow` offers `ShowReleases` season by season, packs
and single episodes each best first; `ShowByTextAsync` names the show in the title index first.
Shows have their own category allow-list (`Selection:AllowedShowCategories`, host configuration;
empty offers no show), a per-episode ceiling (`MaximumEpisodeSizeGiB`, default 8) and prefer 720p
(`PreferredShowResolution`). A film search rejects an episodic release as `NotAFilm`.

`AcquisitionService.StartAsync` takes the episodes wanted: the torrent is added stopped, every
other file is switched off, and it is started; picking from a torrent already held switches the new
files on. `EpisodeFiles` maps a torrent's files to episodes, `Bencode.Files` reads a torrent's file
list, and `FilesAsync` reports each file's priority and progress. `ReadableBytesAsync` counts from
the file's own first piece, so an episode after one never picked is readable.

`ImdbTitle.IsSeries`; `ImdbClient.SuggestShowsAsync` and `SuggestTitlesAsync`. `TmdbClient.ShowAsync`
and `SeasonAsync` describe a show and a season's episodes (`ShowDetails`, `SeasonDetails`,
`EpisodeDetails`). The CLI gains `show <tt|name>`.

## 0.24.0

A keep is no longer a torrent tag: `TorrentTags` describes the download alone, and how long a film
stays in the library, a keep among it, belongs to whoever owns the library. `Retention` answers the
tracker's clock for the source only.

## 0.23.0

`TmdbClient` describes a film from The Movie Database by its IMDb id: credits, certificate in a
configured country, genres, overview, tagline, languages, countries, TMDB's rating, and the paths of
its poster, backdrop and cast portraits, which `ImageAsync` fetches at a given width. It is
configured by `TmdbOptions` (`Tmdb:ReadAccessToken`, a credential set in the environment; without
one nothing is asked). A refused or unreachable database throws, so a caller keeps what it has.

## 0.22.0

`SubtitleCandidate.Rating` carries OpenSubtitles' rating out of ten, read from the search's
`ratings` and `votes`; a subtitle nobody has voted on has none.

## 0.21.0

The disk budget measures free space on the volume the download directory is on. It asked the
path's root, which on Unix is always `/`, so a download directory on its own mount was bounded by
the system disk's free space instead of its own. The disk budget keeps `Download:ReservedGiB` (default 30) free on the download volume once a
download lands, so the transcode written beside it on a shared volume has room to finish; a
download that fits only by eating into it is refused. The budget's state is judged against
whichever limit binds, so a volume with nothing left past the reserve reads as full, and one closer
to it than the warning margin reads as a warning, even under a ceiling set above the volume's size.
`DiskBudget.Judge` makes the decision on a reading already taken. A release whose name marks it as
10-bit H.264 (`Hi10P`, or a 10-bit marker beside an H.264 one) carries `IsHighBitDepthAvc`, shows
`Hi10P` in its summary, and ranks below every other candidate ahead of the tracker's flags: no GPU
decodes it, so its transcode runs on the processor. It is still offered when it is all there is.

## 0.20.0

`MovieBotSettings` locates and layers `moviebot.settings.json`, the settings file every MovieBot
program shares: the copy shipped beside the program holds the defaults, and the first
`moviebot/moviebot.settings.json` under `$XDG_CONFIG_HOME` (default `~/.config`) or `$XDG_CONFIG_DIRS`
(default `/etc/xdg`) overrides it. Both sit underneath every source a host adds, so the environment
overrides single keys. `Describe()` names the files read, for a startup log line. The CLI reads the
same file, so run as the services' account it sees their download root, torrent client and
selection policy; its own `appsettings.json` is gone and its quiet log levels are set in code. The
XDG user-directories lookup for the download root resolves `$XDG_CONFIG_HOME` through the same rule.

## 0.19.1

A blank tracker category is refused at startup, like a missing one. An env file line left as
`Selection__AllowedCategories__0=` binds to an empty name that matches no release, so a host holding
one started normally and found nothing on the tracker without saying why; `AddAcquire` now names the
empty entry and refuses to start.

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
