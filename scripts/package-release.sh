#!/usr/bin/env bash
# Builds the release archive: the moviebot-acquire CLI as one self-contained linux-x64 executable,
# beside its settings file.
#
#   scripts/package-release.sh [output-dir]
#
# The version is the newest entry in CHANGELOG.md.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="$(realpath -m "${1:-$root/artifacts}")"
version="$("$root/scripts/version.sh")"
archive="moviebot-acquire-linux-x64.tar.gz"
name="moviebot-acquire-$version"
stage="$out/$name"

rm -rf "${stage:?}" "${out:?}/$archive" "$out/$archive.sha256"
mkdir -p "$stage"

dotnet publish "$root/src/MovieBot.Acquire.Cli" -c Release -r linux-x64 --nologo \
    --self-contained -p:PublishSingleFile=true -o "$stage"
find "$stage" -name '*.pdb' -delete

cp "$root/LICENSE" "$root/README.md" "$stage/"
printf '%s\n' "$version" > "$stage/VERSION"

tar -C "$out" -czf "$out/$archive" "$name"
(cd "$out" && sha256sum "$archive" > "$archive.sha256")
echo "$out/$archive"
