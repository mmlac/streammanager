#!/usr/bin/env bash
# publish.sh — Produce self-contained single-file artifacts for StreamManager.
#
# Usage:
#   scripts/publish.sh [--rid <rid>] [--verify] [--clean]
#
# With no --rid, publishes all configured RIDs (win-x64, osx-arm64, osx-x64).
# Artifacts land under artifacts/<rid>/.
#
# --verify runs post-publish sanity checks: artifact shape, macOS Info.plist
# version matches VERSION, no config.json / *.log leaked from the dev machine.
#
# This script is CI-ready: it prints progress, exits nonzero on failure, and
# produces artifacts in a known layout.

set -euo pipefail

RIDS_DEFAULT=(win-x64 osx-arm64 osx-x64)
VERIFY=0
CLEAN=0
RIDS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid)    RIDS+=("$2"); shift 2 ;;
        --verify) VERIFY=1; shift ;;
        --clean)  CLEAN=1; shift ;;
        -h|--help)
            sed -n '2,15p' "$0"; exit 0 ;;
        *)
            echo "unknown flag: $1" >&2; exit 2 ;;
    esac
done

if [[ ${#RIDS[@]} -eq 0 ]]; then
    RIDS=("${RIDS_DEFAULT[@]}")
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

VERSION_FILE="$REPO_ROOT/VERSION"
if [[ ! -f "$VERSION_FILE" ]]; then
    echo "VERSION file not found at $VERSION_FILE" >&2
    exit 1
fi
VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"

APP_PROJECT="src/StreamManager.App/StreamManager.App.csproj"
ICON_SOURCE="$REPO_ROOT/streammanager.png"
ICON_DIR="$REPO_ROOT/build/icons"
ICON_ICO="$ICON_DIR/streammanager.ico"
ICON_ICNS="$ICON_DIR/streammanager.icns"
PLIST_TEMPLATE="$REPO_ROOT/build/mac/Info.plist.template"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"

if [[ ! -f "$ICON_SOURCE" ]]; then
    echo "Source icon not found: $ICON_SOURCE" >&2
    exit 1
fi

log() { printf '[publish] %s\n' "$*"; }

bundle_macos_app() {
    local rid_dir="$1"
    local rid="$2"
    local app_dir="$rid_dir/StreamManager.app"
    local contents="$app_dir/Contents"

    log "  assembling StreamManager.app for $rid"
    rm -rf "$app_dir"
    mkdir -p "$contents/MacOS" "$contents/Resources"

    # Move everything dotnet publish emitted into Contents/MacOS (except the
    # future .app dir itself). Single-file publish typically emits just
    # `StreamManager` plus `createdump`; we don't filter so future platform
    # assets are picked up automatically.
    shopt -s dotglob nullglob
    for f in "$rid_dir"/*; do
        [[ "$f" == "$app_dir" ]] && continue
        mv "$f" "$contents/MacOS/"
    done
    shopt -u dotglob nullglob

    # Belt-and-suspenders: publish usually sets +x on the binary, but make sure.
    chmod +x "$contents/MacOS/StreamManager"

    cp "$ICON_ICNS" "$contents/Resources/streammanager.icns"
    sed "s/{{VERSION}}/$VERSION/g" "$PLIST_TEMPLATE" > "$contents/Info.plist"
    # 8-byte magic Finder uses to recognise the bundle type.
    printf 'APPL????' > "$contents/PkgInfo"
}

if [[ $CLEAN -eq 1 ]]; then
    log "cleaning artifacts/"
    rm -rf "$ARTIFACTS_DIR"
fi

log "generating app icons from $ICON_SOURCE (version $VERSION)"
dotnet build "$REPO_ROOT/build/IconGen/IconGen.csproj" -c Release --nologo -v quiet > /dev/null
ICONGEN_DLL="$REPO_ROOT/build/IconGen/bin/Release/net10.0/IconGen.dll"
dotnet "$ICONGEN_DLL" "$ICON_SOURCE" "$ICON_ICO" "$ICON_ICNS"

for rid in "${RIDS[@]}"; do
    out="$ARTIFACTS_DIR/$rid"
    log "publishing $rid → $out"
    rm -rf "$out"
    mkdir -p "$out"
    dotnet publish "$APP_PROJECT" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -o "$out" \
        -p:Version="$VERSION" \
        -p:InformationalVersion="$VERSION" \
        --nologo -v minimal

    # Native-lib PDBs (libSkiaSharp.pdb, libHarfBuzzSharp.pdb) get copied
    # alongside the win-x64 native DLLs and are ~100MB of dead weight for end
    # users. CopyDebugSymbolFilesFromPackages doesn't catch these because they
    # live under runtimes/<rid>/native/, not lib/. Strip them here.
    find "$out" -name '*.pdb' -delete

    case "$rid" in
        osx-*) bundle_macos_app "$out" "$rid" ;;
    esac
done

if [[ $VERIFY -eq 1 ]]; then
    log "verifying artifacts"
    fail=0

    # Scan every artifact tree for forbidden files. These would indicate a dev
    # machine's runtime state leaked into the publish output.
    leaks=$(find "$ARTIFACTS_DIR" \( -name 'config.json' -o -name '*.log' \) -print || true)
    if [[ -n "$leaks" ]]; then
        echo "ERROR: forbidden runtime files found in artifacts:" >&2
        echo "$leaks" >&2
        fail=1
    fi

    for rid in "${RIDS[@]}"; do
        out="$ARTIFACTS_DIR/$rid"
        case "$rid" in
            win-*)
                exe="$out/StreamManager.exe"
                if [[ ! -s "$exe" ]]; then
                    echo "ERROR: missing or empty $exe" >&2
                    fail=1
                else
                    # PE files start with 'MZ'. Read-only check, no `file` needed.
                    magic=$(head -c 2 "$exe")
                    if [[ "$magic" != "MZ" ]]; then
                        echo "ERROR: $exe does not have PE32 'MZ' magic" >&2
                        fail=1
                    else
                        log "  OK $rid: $(basename "$exe") is PE32"
                    fi
                fi
                ;;
            osx-*)
                app="$out/StreamManager.app"
                plist="$app/Contents/Info.plist"
                bin="$app/Contents/MacOS/StreamManager"
                icns="$app/Contents/Resources/streammanager.icns"
                if [[ ! -d "$app" ]]; then
                    echo "ERROR: missing $app" >&2; fail=1; continue
                fi
                if [[ ! -s "$bin" ]]; then
                    echo "ERROR: missing or empty $bin" >&2; fail=1
                fi
                if [[ ! -s "$icns" ]]; then
                    echo "ERROR: missing or empty $icns" >&2; fail=1
                fi
                if ! grep -q "<string>$VERSION</string>" "$plist"; then
                    echo "ERROR: $plist does not contain version $VERSION" >&2
                    fail=1
                else
                    log "  OK $rid: Info.plist version=$VERSION, .icns present"
                fi
                ;;
        esac
    done

    if [[ $fail -ne 0 ]]; then
        echo "verify failed" >&2
        exit 1
    fi
    log "verify OK"
fi

log "done. artifacts in $ARTIFACTS_DIR/"
