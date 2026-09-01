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

## Conventions

- C# namespaces are `TheKrystalShip.MovieBot.Acquire.*`, matching the GitHub org this publishes
  to and the pipeline it belongs to.
- Present-tense canon in every doc and comment: describe how the thing works now. History belongs
  in the CHANGELOG and in commit messages, never in prose or code comments.
- No emoji anywhere — not in docs, comments, commit messages or CLI output.
- Results go to stdout and everything else to stderr, so a search is pipeable.
- Commit per finished piece of work, including the version bump and CHANGELOG entry, and tag the
  bump `v<version>`.
