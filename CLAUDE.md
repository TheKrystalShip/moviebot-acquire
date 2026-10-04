# CLAUDE.md

Guidance for Claude Code working in this repository.

## What this is

moviebot-acquire finds films and shows on a private tracker and fetches them to disk. It is the acquiring
half of the MovieBot pipeline: this reaches the tracker, the torrent client and the title index,
and `moviebot` — the checkout beside it in this workspace — takes what lands on disk and makes it
watchable.

**It is a library first, and `moviebot` compiles against this working copy.**
`MovieBot.Api`, `MovieBot.Bot` and `MovieBot.Handoff` each hold a `ProjectReference` on
`src/MovieBot.Acquire` by relative path, so a change to a public type here is a change to that
build in the same pass, and an edit that compiles in this solution can still break the one next
door. Build both after touching anything public. `moviebot-acquire.slnx` covers the library, the
CLI and the tests; the consumers are outside it.

The `moviebot-acquire` CLI is this repository's own surface onto the same library, and the way
to exercise the tracker without a Discord client in front of it.

## The one idea everything follows from

**The passkey is the account.** It authenticates the tracker's API and the tracker embeds it in
every download URL it hands back, so anything downloaded with it is charged here and a leak is
attributable. Most of the design falls out of that:

- It lives in `TrackerClient` and nowhere else. `DownloadTorrentFileAsync` builds its own URL
  rather than using the `download_link` the tracker echoes into every search row.
- `Release` carries a torrent id, not a link. That is what makes a `Release` safe to log, to put
  in an embed and to hand to a select menu without thinking about it.
- `TrackerTorrent` is the edge of the system. Nothing outside `Tracker/` holds one.
- **Which tracker this is lives in the host's configuration, never in the repository.**
  `Tracker:BaseUrl`, `Selection:AllowedCategories` and `Selection:AllowedShowCategories` have no
  defaults and nothing here names the
  site: the address and the tracker's own category names are set beside the account, in the same
  environment file. A name, a domain, a URL or a category spelling of it appearing in code, a test,
  a doc or a commit message is a leak of the same kind as the passkey; tests use `tracker.invalid`
  and generic categories such as `Movies HD`.

## Commands

```bash
dotnet build moviebot-acquire.slnx -c Release
dotnet test

# the consumer, after touching anything public
dotnet build ../moviebot/moviebot.slnx -c Release

# against the live tracker: the credential comes from the environment, never a file here
Tracker__BaseUrl=... Tracker__Username=... Tracker__Passkey=... \
  src/MovieBot.Acquire.Cli/bin/Release/net10.0/moviebot-acquire search Heat 1995

scripts/package-release.sh        # the release archive, as the release workflow builds it
```

A tag `v<version>` matching the newest CHANGELOG heading publishes the CLI as a GitHub release.
**MovieBot's CI and releases check out this repository's `main`**, so a push here that changes a
public type is a red build there until MovieBot's half is pushed too: push the two together.

The CLI's verbs: `search <title>` and `imdb <tt…>` rank what the tracker holds, `show <tt|name>`
lists what it offers for a show season by season, `film <id|name>`
asks the title index what a film is called, `get <title> [--pick N]` starts a download,
`downloads` reports what is running and `budget` reports the download directory against its
ceiling.

This library's code runs on this host inside MovieBot's three services: publishing them carries
`TheKrystalShip.MovieBot.Acquire.dll` into `/opt/moviebot/{api,bot,handoff}`. A change here
reaches the running system when those are published, and `moviebot/deploy/CLAUDE.md` holds that
procedure.

## Search invariants

These are measured against what the tracker actually returns. Changing one means re-measuring.

- **There is no scraping and there must not be.** The tracker publishes a member API. A login,
  a cookie jar or an HTML parser appearing in here means a wrong turn was taken: the pages are a
  heavily modified Ocelot and would break on any restyle, and hammering them is what gets an
  account banned.
- **The whole typed phrase goes to the tracker.** It narrows on a year itself, and a bare common
  word comes back against a server-side cap — `heat` returns a capped hundred rows where
  `heat 1995` returns ten.
- **The year is applied again locally.** The tracker matches words against a release name, not
  films. A release whose own year disagrees is a different film; one carrying no year is not a
  disagreement and stays.
- **A four-digit number is a year only if a film could have been released in it and the title
  survives without it.** That is what keeps `Blade Runner 2049`, `1917` and `2012` whole.
- **Titles are compared by equality after stripping trailing edition words, never containment.**
  Containment reads `Dead Heat` as `Heat` and `Dune Part Two` as `Dune`. `EditionWords` holds no
  word that could order or number a film, so `part`, `two` and `chapter` stay.
- **`UHD` is not a resolution.** `1080p.UHD.BluRay` is a 1080p encode of a UHD disc, and reading
  it as 2160p ranks it above the genuine 2160p releases beside it.
- **Whole-token matching, never substring.** A substring test reads `3D` out of a group name and
  `WEB` out of a title, and both change how a release ranks.
- **Hi10P ranks last, ahead of the tracker's flags, and is never rejected.** No GPU decodes 10-bit
  H.264, so its transcode runs on the processor at under half the usual pace; it still plays, so
  it is offered when it is all there is. 10-bit HEVC decodes on the GPU and is not Hi10P.
- **Rejections are carried, never dropped.** `RankedReleases.EmptyExplanation` is why a search
  that offers nothing can say so. An empty menu is otherwise indistinguishable from a broken
  tracker call.
- **The client paces itself.** The tracker caps how often a member may call it and answers past
  the cap with an error rather than a result.

### Shows

- **A show is searched by its own IMDb id, and that is one call.** The tracker files every season
  and every episode under the show's id, never an episode's.
- **A release is a show's when its name carries a season marker** (`EpisodeMarker`: `S02`,
  `S02E04`, `S02E04E05`, `S01-S03`), whatever category it was filed under. A film search rejects
  one and a show search accepts nothing else, and each has its own category allow-list.
- **A show's title ends at its marker.** An episode's own name follows it and carries no quality
  marker to stop at, so without the marker `Slow Horses S06E02 Daddy Issues` is the title.
- **A show is judged per episode.** A season pack's total says how long the season is, not how
  heavy the encode: a 1080p pack is 20–44 GiB, an episode 2–5.
- **Shows prefer 720p.** A season is many hours; the ladder serves 720p to most screens anyway,
  and a person may still pick 1080p.
- **Finished seasons are packs, airing ones are single episodes**, both measured. A pack is offered
  under every season it covers and an episode under its episode.

## Autocomplete invariants

- **A tracker call is never made per keystroke.** The surface sends a request per character. The
  order is: exact answer held, then narrowed from a shorter answer held, then the debounce, then
  the tracker.
- **Narrowing does not wait for the debounce.** It costs no call, and an answer already held is
  better shown at once.
- **The debounce is keyed per person.** Shared, a room where somebody is mid-word answers
  everybody else with nothing.
- **The deadline covers queueing as well as the call.** `TrackerOptions.MinimumRequestIntervalMs`
  is charged to whoever is queued behind a call, so a pacing floor above the autocomplete
  deadline answers a second person searching with nothing every time. Raising one means checking
  the other.
- **A failing tracker surfaces as an empty list, never as a broken command.** The next keystroke
  corrects it.
- **Labels are built to fit, not trimmed to fit.** The surface rejects a label over its limit
  outright, and the identifying half is what must survive.

## Subtitle invariants

- **Searching is free; downloading is not.** Only a download spends the day's allowance, so
  everything that can be judged from a search result is judged before one is spent.
- **Every check has three answers, not two.** Most uploads declare no frame rate and almost none
  are indexed against a particular file. Treating absent evidence as a failure would condemn
  nearly every subtitle that exists, so unknown is its own answer and never counts against a
  candidate.
- **A measurement outranks anything an uploader typed.** Where a film already carries a subtitle
  that fits it, a candidate is measured against it: each cue votes for its distance to every
  nearby reference cue and the winning offset is the answer. The consensus threshold is measured,
  not chosen — another English subtitle for the same film reaches 20 per cent, that film's
  Romanian track 13, and its commentary track under 3.
- **A subtitle whose timing stretches has no offset, and none is reported.** No single shift fits
  one, so refusing to align is the honest answer and the frame-rate check is what names the cause.
- **Character encoding is not a fitness check.** It says nothing about whether a subtitle matches
  the film, and it is repaired on the way in. Showing it as a warning steers people away from
  subtitles that are perfectly good.
- **Query strings are built sorted and without default values.** The API answers anything else
  with a redirect to the canonical spelling, so an unnormalised query yields HTML where JSON was
  expected — and only on some calls, which reads as an intermittent fault rather than a mistake.
- **A short answer to a download link is a message, not a subtitle.** Being throttled arrives as a
  line of plain text with a success status, and a client that does not check writes it to disk as
  a valid, tiny subtitle file.

## Title index invariants

The catalogue is asked what a film is called and what its poster looks like. `ImdbClient` is the
only thing that talks to it.

- **This is not scraping, and the distinction is the same one the tracker's rule makes.** The
  index that answers the site's own search box is a JSON endpoint and is read as one. The title
  pages are not: they answer anything that is not a browser with an empty `202`, and parsing them
  would be exactly the wrong turn the tracker rule describes.
- **The index needs no key, and names a film.** What it holds — the name, the year, the top billing
  and the poster — is what a message about a film shows, and what the ingest names a film by.
  Everything a page about a film shows beyond that comes from the film database below.
- **A poster is asked for at the size it will be seen.** The originals are a few thousand pixels
  tall; the image host takes the width in the file name, and asking for it is the difference
  between a poster that renders in a message and one that times out on the way.
- **The index is ordered by what people are searching for today**, which puts a sequel above the
  film it follows. An exact name beats that ordering, and a year beats everything: it is the whole
  of what separates a remake from what it remade.
- **A search that cannot be sure answers with nothing.** A film named wrongly is worse than one
  not named at all, because everything downstream believes it.
- **A surface asking for a film is never handed a show, and the reverse.** `SuggestAsync` offers
  features, `SuggestShowsAsync` series and miniseries, `SuggestTitlesAsync` both; an episode is never
  offered, since a show is fetched by its seasons. A film search identifies its film among features
  only, so a series sharing the name does not send it to the series' id.

## Film database invariants

`TmdbClient` describes a film or a show from The Movie Database: credits, certificate, genres,
overview, languages, countries, a rating and its artwork, and for a show its seasons and each
season's episodes with their stills. It is the only thing that talks to it.

- **A film or a show is found by its IMDb id.** That is how everything in MovieBot names one, and
  the database maps it, so nothing downstream has to learn a second id. A season is asked for by
  the show's database id, which the show's description carries. The week's trending films and shows
  (`TrendingFilmsAsync`, `TrendingShowsAsync`) come back named by the database's own id, so each
  entry's IMDb id is asked for in turn; an entry without one, or an adult one, is left out.
- **The token is a credential and lives in the environment** (`Tmdb__ReadAccessToken`). Without one
  nothing is asked and a film is described by the index alone.
- **Nothing reaches the database but the API, and it asks once.** The client hands out image paths,
  not URLs: the caller fetches each image once, keeps it and the description on disk, and serves its
  own copies. Nobody watching ever talks to the database directly.
- **A refused or unreachable database is an error, not a missing film.** A caller holding an older
  description keeps it; only "no film under this id" is an answer.
- **A rating with no votes behind it is a placeholder**, and is no rating.
- **The database's terms are free personal use with attribution.** Every surface that shows what it
  served credits it; the client cannot, so it is the surfaces' rule.

## Disk invariants

- **The budget is measured off the filesystem on every call, never accumulated.** A running
  total drifts the moment somebody deletes a film by hand, and it drifts silently: the first
  sign is a refused download with the disk half empty, or a full volume under a budget that says
  there is room.
- **A completed download keeps seeding until retention lets it go.** A private tracker expects
  the seeding, and the retention rule is what decides when enough of it has been done. The size
  ceiling on a release is a disk budget, not a bandwidth one.
- **The volume's own free space bounds the budget.** Whichever runs out first is the real
  headroom, and the volume is the one that produces a half-written file rather than a refusal.
  The state is judged against that same binding limit, so a ceiling set above the volume's size
  still warns and still reads full.
- **A download is not the last thing written for a film.** Its transcode lands while the source
  seeds, on the same volume wherever the media root shares it, so the volume keeps
  `Download:ReservedGiB` free after a download lands. The transcode holds no claim of its own until
  its ingest starts, partway into the download, and a host with no cold disk has nowhere else to put
  it, so the reserve is what a film just admitted is given in the meantime. A download that fits
  only by eating into it is refused before the tracker is asked, instead of a transcode stopping
  partway in front of a room.
- **`DiskBudget` is the one account of what is promised on a volume.** A running transcode holds a
  claim in the `TranscodeLedger` (files under `.transcode-reservations` in the media root, written
  by the hand-off and read by every process that starts a download; the root is `Media:Root`, or
  `Handoff:MediaRoot` where that is blank, which name one directory). The ledger is under the media
  root because its one writer can always write there, and the dot keeps every listing of titles from
  taking it for one. A ledger that cannot be read or written never blocks a download or a transcode:
  a missing directory is an empty ledger, and an unreadable one, an unconfigured media root or a
  claim that cannot be written is a logged warning and counts as no claims. A claim counts for what
  the transcode has still to write: its size less what its title directory already holds, because
  the written part is already gone from the free space. A download's headroom subtracts the claims on
  its volume, and `ReadVolume` gives a transcode's placement the free space of any volume less the
  claims on it and, where the downloads share it, the downloads still arriving (`PendingBytes`).
  Which volume a path is on is its longest mount point (`Volumes.MountOf`), so a media root on the
  download volume is judged against the downloads and one on another volume is not.

## Retention invariants

A download stays for a window of seeding and its torrent and files are then let go. `Retention`
answers whether a download is due and how long it has left; who removes it, running transcodes and
what a room holds are the callers' business. How long the transcoded film stays in the library is
the library owner's clock and not this one.

- **The clock is the client's seeding time, never the calendar.** The tracker credits seeding by
  the announces it receives, which happen only while the client runs and the torrent is active,
  and the client's own `seeding_time` counts the same stretches and persists in its resume data.
  A week is a week of seeding: hours the machine was off count nowhere.
- **The client's figure can read low, never high.** It is reported as of the torrent's last
  status refresh, and a torrent with no traffic at all may go unrefreshed for a while; an
  announce or a peer brings it up to date. Low delays a prune and never hastens one, which is
  the direction a proxy for a penalised minimum has to err in.
- **The floor is the tracker's minimum plus a margin, and nothing goes under it.** The client's
  clock runs ahead of the tracker's by whatever announces did not land, always in the direction
  of the tracker crediting less. The margin absorbs that; the window sits days above the floor
  so the two thresholds cannot meet.
- **The rule holds the floor on its own, and the options refuse to be set under it.** A window
  shorter than the floor would either be ignored, which makes it a lie, or honoured, which earns
  the penalty the floor exists to avoid. The options refuse to start that way and the rule refuses
  again on every answer, for a process that got past validation.
- **The tracker's own figure is not available.** Its API offers two actions, both searches, and
  the snatchlist that shows the remaining time is a page behind the login cookie. The client's
  clock is the honest proxy, used with the margin above, and it is never presented as the
  tracker's number.

## Download invariants

- **The info hash is computed from the torrent file.** The client's add endpoint answers "Ok."
  and names nothing. `Bencode` locates the info dictionary's raw bytes rather than parsing and
  re-encoding it: a re-encoding has to reproduce the original byte for byte, and any disagreement
  yields a hash matching nothing.
- **Sequential order and first-and-last-piece priority are both set.** Either alone is not
  enough — sequential gives a file complete from the beginning that still will not open, because
  the index a player needs sits at one end or the other.
- **An unrecognised client state reads as starting, never as failed.** The client has states this
  does not know, and calling one a failure is a lie about a download that is fine.
- **The client's ETA placeholder is not a duration.** It reports one rather than nothing when it
  cannot estimate, and showing it produces a film arriving in a hundred years.
- **Disk is checked before the tracker is asked, and the torrent fetched before the client is
  asked to take it.** Each ordering exists so a refusal costs nothing and leaves nothing behind.
- **Picked episodes are the only files fetched.** The torrent is added stopped, every file not
  picked gets priority 0, and it is started: a file switched off after starting may already have
  pieces on disk. Files are matched to the client's list by path, never by index, and a torrent
  that fails partway is deleted rather than left stopped. The tracker accounts for the torrent,
  not the files fetched from it, so retention treats a partly fetched pack like any other.
- **Picked episodes arrive first-first.** The earliest picked is set to the client's highest file
  priority and the next to its second; the rest stay at normal and follow in the torrent's order,
  which sequential downloading keeps. The client has these three levels for a file and no more, so
  an exact ladder is not something to build.
- **Which file is which episode is `EpisodeFiles`, and nowhere else.** The download picks files
  with it and the hand-off ingests them with it; two readings would fetch an episode the other
  never ingests.
- **A file is readable from its own first piece.** A file before it in the torrent may never have
  been picked, so counting from the torrent's start would hold every later episode unreadable.

## Tags

More than one process acts on a download: one starts it, another turns it into something
watchable, a third announces it. A download outlives all of them, and the torrent client already
persists it, so the state of that work is kept on the torrent rather than in any of them.

- **The vocabulary is `TorrentTags`, and nowhere else.** Two processes spelling the same idea
  differently does not throw: the tag is never seen and the work silently never happens.
- **A tag is cleared by whoever acted on it**, which is what stops the same work being done on
  every pass.
- **A failure gets its own tag rather than leaving the work tag alone.** Left alone, a failure is
  indistinguishable from work still in progress, and whoever is waiting is told nothing at all.
- **Tags describe the download, never the film in the library.** A torrent is let go of after its
  seeding window and the film outlives it, so anything about how long the film stays, a keep
  among it, is held by whoever owns the library and not on a torrent that will not be there.

## Conventions

- C# namespaces are `TheKrystalShip.MovieBot.Acquire.*`, matching the GitHub org this publishes
  to and the pipeline it belongs to.
- Present-tense canon in every doc and comment: describe how the thing works now. History belongs
  in the CHANGELOG and in commit messages, never in prose or code comments.
- No emoji anywhere — not in docs, comments, commit messages or CLI output.
- Results go to stdout and everything else to stderr, so a search is pipeable.
- Commit per finished piece of work, including the version bump and CHANGELOG entry, and tag the
  bump `v<version>`.
- **A change that spans both repositories is one commit in each**, describing that repository's
  half. Address git with `git -C <repo>`: the two checkouts sit side by side, and a `cd` applies
  to every command after it in the same shell, so a commit or a tag meant for one lands in the
  other.
