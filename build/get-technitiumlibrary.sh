#!/usr/bin/env bash
#
# Provisions the TechnitiumLibrary sibling clone that DnsServerCore's
# HintPaths expect (..\..\TechnitiumLibrary\bin\*.dll relative to
# DnsServerCore/, i.e. a sibling of this repository's root).
#
# Idempotent: an existing clone already at the pinned tag is left alone
# (just re-built, since a Release build is itself idempotent); a clone at
# the wrong tag is a hard failure with instructions, never a silent
# reset.
#
# Usage: build/get-technitiumlibrary.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION_FILE="$REPO_ROOT/build/technitiumlibrary.version"

if [[ ! -f "$VERSION_FILE" ]]; then
    echo "error: pin file not found: $VERSION_FILE" >&2
    exit 1
fi

PINNED_TAG="$(tr -d '[:space:]' < "$VERSION_FILE")"

if [[ -z "$PINNED_TAG" ]]; then
    echo "error: $VERSION_FILE is empty; expected a single TechnitiumLibrary tag" >&2
    exit 1
fi

# Sibling of the repo root's parent, matching DnsServerCore's HintPaths
# (..\..\TechnitiumLibrary\bin\*.dll relative to DnsServerCore/).
CLONE_DIR="$(cd "$REPO_ROOT/.." && pwd)/TechnitiumLibrary"
REMOTE_URL="https://github.com/TechnitiumSoftware/TechnitiumLibrary.git"

if [[ -d "$CLONE_DIR/.git" ]]; then
    CURRENT_TAG="$(git -C "$CLONE_DIR" describe --tags --exact-match 2>/dev/null || true)"

    if [[ "$CURRENT_TAG" != "$PINNED_TAG" ]]; then
        echo "error: existing clone at $CLONE_DIR is not at pinned tag '$PINNED_TAG' (found: '${CURRENT_TAG:-<none/detached>}')." >&2
        echo "       Remove it and re-run this script to reprovision, e.g.:" >&2
        echo "         rm -rf '$CLONE_DIR' && '$0'" >&2
        exit 1
    fi

    echo "TechnitiumLibrary already cloned at $CLONE_DIR (tag $PINNED_TAG); skipping clone."
elif [[ -e "$CLONE_DIR" ]]; then
    echo "error: $CLONE_DIR exists and is not a git clone; refusing to overwrite." >&2
    exit 1
else
    echo "Cloning TechnitiumLibrary (tag $PINNED_TAG) into $CLONE_DIR..."
    git clone --branch "$PINNED_TAG" --depth 1 "$REMOTE_URL" "$CLONE_DIR"
fi

# Projects needed to produce the five DLLs DnsServerCore references:
# TechnitiumLibrary.dll, .ByteTree.dll, .IO.dll, .Net.dll, .Security.OTP.dll.
# Each project's Release OutputPath is ..\bin\ (relative to the project
# folder), so building these five in any order lands every DLL in
# TechnitiumLibrary/bin/ alongside each other.
PROJECTS=(
    TechnitiumLibrary
    TechnitiumLibrary.ByteTree
    TechnitiumLibrary.IO
    TechnitiumLibrary.Net
    TechnitiumLibrary.Security.OTP
)

for project in "${PROJECTS[@]}"; do
    echo "Building $project (Release)..."
    dotnet build "$CLONE_DIR/$project/$project.csproj" -c Release
done

BIN_DIR="$CLONE_DIR/bin"
REQUIRED_DLLS=(
    TechnitiumLibrary.dll
    TechnitiumLibrary.ByteTree.dll
    TechnitiumLibrary.IO.dll
    TechnitiumLibrary.Net.dll
    TechnitiumLibrary.Security.OTP.dll
)

for dll in "${REQUIRED_DLLS[@]}"; do
    if [[ ! -f "$BIN_DIR/$dll" ]]; then
        echo "error: expected $BIN_DIR/$dll to exist after build but it does not." >&2
        exit 1
    fi
done

echo "TechnitiumLibrary provisioned at $CLONE_DIR (tag $PINNED_TAG); all five DLLs present in $BIN_DIR."
