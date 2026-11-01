#!/usr/bin/env bash
# Copies Slay the Spire 2 progress from a normal save profile into the matching modded profile.
# Backs up the whole Steam account save folder first, then copies progress.save, prefs.save,
# their .backup files, and the run history from profile<N>/saves to modded/profile<N>/saves.
# Turn off Steam Cloud for the game before running it, or the game replaces the copied files on its next start.
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: copy-vanilla-saves.sh [options]

  -p, --profile N        Profile number to copy: 1, 2, or 3 (default: 1)
  -r, --saves-root DIR   Folder that contains your Steam ID folder
                         (default on Windows: %APPDATA%/SlayTheSpire2/steam,
                          otherwise: ~/.local/share/SlayTheSpire2/steam)
  -s, --steam-id ID      Steam ID folder to use when there is more than one
  -y, --yes              Do not ask for confirmation
  -h, --help             Show this help
EOF
}

die() {
    echo "Error: $*" >&2
    exit 1
}

profile=1
saves_root=""
steam_id=""
assume_yes=0

while [ $# -gt 0 ]; do
    case "$1" in
        -p|--profile) profile="${2:?}"; shift 2 ;;
        -r|--saves-root) saves_root="${2:?}"; shift 2 ;;
        -s|--steam-id) steam_id="${2:?}"; shift 2 ;;
        -y|--yes) assume_yes=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "Unknown option: $1" ;;
    esac
done

case "$profile" in
    1|2|3) ;;
    *) die "Profile must be 1, 2, or 3." ;;
esac

if [ -z "$saves_root" ]; then
    if [ -n "${APPDATA:-}" ]; then
        appdata="$APPDATA"
        if command -v cygpath >/dev/null 2>&1; then
            appdata="$(cygpath -u "$APPDATA")"
        fi
        saves_root="$appdata/SlayTheSpire2/steam"
    else
        saves_root="${XDG_DATA_HOME:-$HOME/.local/share}/SlayTheSpire2/steam"
    fi
fi

if command -v tasklist >/dev/null 2>&1; then
    if tasklist 2>/dev/null | grep -qi 'SlayTheSpire2'; then
        die "Close Slay the Spire 2 before copying saves."
    fi
elif command -v pgrep >/dev/null 2>&1; then
    if pgrep -i 'SlayTheSpire2' >/dev/null 2>&1; then
        die "Close Slay the Spire 2 before copying saves."
    fi
fi

[ -d "$saves_root" ] || die "Save folder not found: $saves_root (use --saves-root)."

if [ -z "$steam_id" ]; then
    accounts=()
    for dir in "$saves_root"/*/; do
        name="$(basename "$dir")"
        case "$name" in
            ''|*[!0-9]*) ;;
            *) accounts+=("$name") ;;
        esac
    done
    [ "${#accounts[@]}" -eq 1 ] || die "Found ${#accounts[@]} Steam ID folders in $saves_root. Run again with --steam-id <id>."
    steam_id="${accounts[0]}"
fi

account_dir="$saves_root/$steam_id"
source_dir="$account_dir/profile$profile/saves"
target_dir="$account_dir/modded/profile$profile/saves"

[ -f "$source_dir/progress.save" ] || die "No progress.save found in $source_dir."

echo "From: $source_dir"
echo "To:   $target_dir"
echo
echo "Steam Cloud for Slay the Spire 2 must be OFF (Steam > right-click the game > Properties > General)."
echo "Otherwise the game replaces the copied files with the Steam Cloud copies on its next start."
echo "Progress, settings, and run history in the modded profile will be replaced."
if [ "$assume_yes" -ne 1 ]; then
    read -r -p "Type yes to continue: " answer
    if [ "$answer" != "yes" ]; then
        echo "Cancelled. Nothing was changed."
        exit 0
    fi
fi

backup_dir="$(dirname "$saves_root")/save-backups/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$backup_dir"
cp -R "$account_dir" "$backup_dir/"
echo "Backup:  $backup_dir"

mkdir -p "$target_dir"
for name in progress.save progress.save.backup prefs.save prefs.save.backup; do
    if [ -f "$source_dir/$name" ]; then
        cp -f "$source_dir/$name" "$target_dir/"
        echo "Copied:  $name"
    fi
done

if [ -d "$source_dir/history" ]; then
    mkdir -p "$target_dir/history"
    cp -Rf "$source_dir/history/." "$target_dir/history/"
    echo "Copied:  history"
fi

echo
echo "Done. Start the game with mods on and check that your unlocks are there."
