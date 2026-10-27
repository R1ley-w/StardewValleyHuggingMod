#!/bin/bash
# Hugging Mod installer for macOS.
# Finds your Stardew Valley Mods folder, downloads the latest release from GitHub,
# and installs it (keeping your config.json if you're updating).

set -euo pipefail

REPO="R1ley-w/StardewValleyHuggingMod"
MOD_FOLDER="Hug"

say()  { printf '%s\n' "$*"; }
fail() { say ""; say "❌ $*"; finish 1; }
finish() {
    say ""
    # Keep the Terminal window open when double-clicked.
    if [ -t 0 ]; then read -r -p "Press Enter to close..." _ || true; fi
    exit "${1:-0}"
}

say "=== Hugging Mod installer ==="
say ""

# ---------- 1. Find the game folder ----------
STEAM_ROOT="$HOME/Library/Application Support/Steam"
candidates=(
    "$STEAM_ROOT/steamapps/common/Stardew Valley/Contents/MacOS"
    "/Applications/Stardew Valley.app/Contents/MacOS"
    "$HOME/Applications/Stardew Valley.app/Contents/MacOS"
)

# Extra Steam library folders (e.g. games installed on an external drive).
VDF="$STEAM_ROOT/steamapps/libraryfolders.vdf"
if [ -f "$VDF" ]; then
    while IFS= read -r lib; do
        candidates+=("$lib/steamapps/common/Stardew Valley/Contents/MacOS")
    done < <(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$VDF")
fi

is_game_dir() {
    [ -f "$1/Stardew Valley.dll" ] || [ -f "$1/StardewValley.dll" ] || [ -f "$1/StardewModdingAPI.dll" ]
}

GAME_DIR=""
for dir in "${candidates[@]}"; do
    if is_game_dir "$dir"; then GAME_DIR="$dir"; break; fi
done

# Not found automatically: let the player pick it.
if [ -z "$GAME_DIR" ]; then
    say "Couldn't find Stardew Valley automatically."
    say "A window will open. Pick your 'Stardew Valley.app' (or the 'Stardew Valley' folder in Steam)."
    picked=$(osascript -e 'POSIX path of (choose file of type {"app", "public.folder"} with prompt "Select Stardew Valley.app or its game folder")' 2>/dev/null || true)
    picked="${picked%/}"
    [ -n "$picked" ] || fail "No folder selected. Nothing was installed."
    for dir in "$picked/Contents/MacOS" "$picked"; do
        if is_game_dir "$dir"; then GAME_DIR="$dir"; break; fi
    done
    [ -n "$GAME_DIR" ] || fail "That doesn't look like a Stardew Valley install: $picked"
fi

say "✅ Found Stardew Valley: $GAME_DIR"

if [ ! -f "$GAME_DIR/StardewModdingAPI.dll" ] && [ ! -f "$GAME_DIR/StardewModdingAPI" ]; then
    say "⚠️  SMAPI doesn't seem to be installed. The mod needs it to run."
    say "   Get it from https://smapi.io after this finishes."
fi

MODS_DIR="$GAME_DIR/Mods"
mkdir -p "$MODS_DIR"

# ---------- 2. Find the latest release ----------
say ""
say "Checking GitHub for the latest version..."
API_JSON=$(curl -fsSL -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/$REPO/releases/latest") \
    || fail "Couldn't reach GitHub. Check your internet connection and try again."

TAG=$(printf '%s' "$API_JSON" | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)
ZIP_URL=$(printf '%s' "$API_JSON" | grep -o '"browser_download_url"[[:space:]]*:[[:space:]]*"[^"]*\.zip"' \
    | sed 's/.*"\(https[^"]*\)"/\1/' | head -n1)

[ -n "$ZIP_URL" ] || fail "The latest release ($TAG) has no .zip file attached."
say "Latest version: ${TAG:-unknown}"

# ---------- 3. Download and unpack ----------
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

say "Downloading..."
curl -fL --progress-bar -o "$TMP/mod.zip" "$ZIP_URL" || fail "Download failed."
unzip -q "$TMP/mod.zip" -d "$TMP/unzipped" || fail "The downloaded file isn't a valid zip."

SRC=$(dirname "$(find "$TMP/unzipped" -name manifest.json -not -path '*/__MACOSX/*' | head -n1)")
[ -f "$SRC/manifest.json" ] || fail "Couldn't find the mod inside the download."

# ---------- 4. Remove old versions (keep the player's config) ----------
MOD_ID=$(sed -n 's/.*"UniqueID"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$SRC/manifest.json" | head -n1)
[ -n "$MOD_ID" ] || fail "The downloaded mod has no UniqueID in its manifest."

DEST="$MODS_DIR/$MOD_FOLDER"
# Prefer the config from the standard folder, otherwise take it from any old copy.
[ -f "$DEST/config.json" ] && cp "$DEST/config.json" "$TMP/config.json"

removed=0
while IFS= read -r -d '' manifest; do
    old_dir=$(dirname "$manifest")
    # Safety: only ever delete folders inside Mods, never Mods itself.
    case "$old_dir" in "$MODS_DIR"/?*) ;; *) continue ;; esac
    if grep -qi "\"UniqueID\"[[:space:]]*:[[:space:]]*\"$MOD_ID\"" "$manifest"; then
        if [ ! -f "$TMP/config.json" ] && [ -f "$old_dir/config.json" ]; then
            cp "$old_dir/config.json" "$TMP/config.json"
        fi
        old_ver=$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$manifest" | head -n1)
        say "Removing old version ${old_ver:-?}: ${old_dir#"$MODS_DIR"/}"
        rm -rf "$old_dir"
        removed=$((removed + 1))
    fi
done < <(find "$MODS_DIR" -mindepth 2 -maxdepth 4 -name manifest.json -print0 2>/dev/null)

[ "$removed" -gt 0 ] || say "No old versions found."
[ -f "$TMP/config.json" ] && say "Keeping your existing settings (config.json)."

# ---------- 5. Install ----------
rm -rf "$DEST"
cp -R "$SRC" "$DEST"
[ -f "$TMP/config.json" ] && cp "$TMP/config.json" "$DEST/config.json"
xattr -dr com.apple.quarantine "$DEST" 2>/dev/null || true

say ""
say "🎉 Installed Hugging Mod ${TAG:-} to:"
say "   $DEST"
say ""
say "Launch the game through SMAPI to play. Press H next to another player to hug!"
finish 0
