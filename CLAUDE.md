# CLAUDE.md

Guidance for Claude Code working in this repository.

## What this is

moviebot-acquire finds films on a private tracker and fetches them to disk. It is the acquiring
half of the MovieBot pipeline and a standalone project: it shares no code, no packages and no
deployment with `moviebot`, which sits beside it in this workspace. The seam between them is a
directory on disk — this writes a file into it, MovieBot ingests and streams from it.

## The one idea everything follows from

**The passkey is the account.** It authenticates the tracker's API and the tracker embeds it in
every download URL it hands back, so anything downloaded with it is charged here and a leak is
attributable. Most of the design falls out of that:

- It lives in `TrackerClient` and nowhere else. `DownloadTorrentFileAsync` builds its own URL
  rather than using the `download_link` the tracker echoes into every search row.
- `Release` carries a torrent id, not a link. That is what makes a `Release` safe to log, to put
  in an embed and to hand to a select menu without thinking about it.
- `TrackerTorrent` is the edge of the system. Nothing outside `Tracker/` holds one.

## Commands

```bash
dotnet build moviebot-acquire.slnx -c Release
dotnet test

# against the live tracker: the credential comes from the environment, never a file here
Tracker__Username=... Tracker__Passkey=... \
  src/MovieBot.Acquire.Cli/bin/Release/net10.0/moviebot-acquire search Heat 1995
```

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
- **Rejections are carried, never dropped.** `RankedReleases.EmptyExplanation` is why a search
  that offers nothing can say so. An empty menu is otherwise indistinguishable from a broken
  tracker call.
- **The client paces itself.** The tracker caps how often a member may call it and answers past
  the cap with an error rather than a result.

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

## Disk invariants

- **The budget is measured off the filesystem on every call, never accumulated.** A running
  total drifts the moment somebody deletes a film by hand, and it drifts silently: the first
  sign is a refused download with the disk half empty, or a full volume under a budget that says
  there is room.
- **A completed download keeps seeding.** A private tracker expects it, so nothing is deleted on
  a schedule and the directory only grows until somebody removes a film. The size ceiling on a
  release is a disk budget, not a bandwidth one.
- **The volume's own free space bounds the budget.** Whichever runs out first is the real
  headroom, and the volume is the one that produces a half-written file rather than a refusal.

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

## Conventions

- C# namespaces are `TheKrystalShip.MovieBot.Acquire.*`, matching the GitHub org this publishes
  to and the pipeline it belongs to.
- Present-tense canon in every doc and comment: describe how the thing works now. History belongs
  in the CHANGELOG and in commit messages, never in prose or code comments.
- No emoji anywhere — not in docs, comments, commit messages or CLI output.
- Results go to stdout and everything else to stderr, so a search is pipeable.
- Commit per finished piece of work, including the version bump and CHANGELOG entry, and tag the
  bump `v<version>`.
