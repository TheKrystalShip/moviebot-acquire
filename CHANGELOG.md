# Changelog

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
