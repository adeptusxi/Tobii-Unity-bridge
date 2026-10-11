#!/bin/sh
# Builds the Unity package as a tarball for Package Manager's
# "Add package from tarball". Output: dist/com.talonbridge.gaze-<version>.tgz
#
# The development-only Tests folder is left out.

set -eu

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PKG="$ROOT/Unity/Talon-Unity-bridge/Packages/com.talonbridge.gaze"
OUT="$ROOT/dist"

NAME="$(sed -n 's/^ *"name": *"\(.*\)",*$/\1/p' "$PKG/package.json" | head -n 1)"
VERSION="$(sed -n 's/^ *"version": *"\(.*\)",*$/\1/p' "$PKG/package.json" | head -n 1)"

if [ -z "$NAME" ] || [ -z "$VERSION" ]; then
    echo "could not read name and version from $PKG/package.json" >&2
    exit 1
fi

STAGE="$(mktemp -d "${TMPDIR:-/tmp}/gazepkg.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT

# Package Manager expects the npm layout: everything under package/.
mkdir -p "$STAGE/package" "$OUT"
(cd "$PKG" && tar -cf - \
    --exclude './Tests' \
    --exclude './Tests.meta' \
    --exclude '.DS_Store' \
    --exclude '.claude' \
    .) | (cd "$STAGE/package" && tar -xf -)

TARBALL="$OUT/$NAME-$VERSION.tgz"

# COPYFILE_DISABLE keeps macOS resource-fork files out of the archive.
COPYFILE_DISABLE=1 tar -czf "$TARBALL" -C "$STAGE" package

echo "$TARBALL"
