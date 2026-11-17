#!/bin/bash
# Sync your work with the team. Double-click to run.
#
# Before running: commit your work in GitHub Desktop, then save and close Wwise.
#
# What it does:
#   1. Downloads teammates' work and merges it with yours.
#   2. Soundbank conflicts are resolved automatically (banks get rebuilt anyway).
#   3. Any other conflict cancels the merge and leaves your work exactly as it was.
#   4. Rebuilds all soundbanks with Wwise and saves them.
#
# After it finishes: press "Push origin" in GitHub Desktop to upload.

export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
export GIT_LFS_FORCE_PROGRESS=1
cd "$(dirname "$0")" || exit 1

# ---------------------------------------------------------------- Settings
BRANCH="${SYNC_BRANCH:-wwise-integration}"
BANKS="Assets/StreamingAssets/Audio/GeneratedSoundBanks"
# Files that tools rebuild on every Mac. Local changes to them are thrown away
# and conflicts in them are resolved automatically.
REBUILT=("$BANKS" "Assets/Wwise/Editor/ProjectData")
WPROJ="TheRoomINeverLeft_WwiseProject/TheRoomINeverLeft_WwiseProject.wproj"
HELP_CONTACT="Hubert"

# ----------------------------------------------------------------- Output
step() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
note() { printf '   %s\n' "$1"; }
finish() {
  echo
  [ -z "${SYNC_NO_PAUSE:-}" ] && read -r -p "Press Return to close this window. " _
  exit "$1"
}
die() {
  printf '\n\033[31mSYNC STOPPED\033[0m\n'
  printf '%s\n' "$@"
  finish 1
}

# ------------------------------------------------------------ Preflight
step "Checking your setup"

command -v git >/dev/null 2>&1 && git lfs version >/dev/null 2>&1 \
  || die "Git or Git LFS is not installed." "Double-click Setup.command first, then run Sync again."

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || die "This file must stay inside the project folder."
cd "$ROOT" || exit 1
GIT_DIR_PATH="$(git rev-parse --absolute-git-dir)"
WWISE_LOG="$GIT_DIR_PATH/sync-wwise.log"

[ -n "$(git config user.name)" ] && [ -n "$(git config user.email)" ] || die \
  "Git does not know your name yet." \
  "In GitHub Desktop open Settings > Git, fill in Name and Email, press Save, then run Sync again."

CURRENT="$(git branch --show-current)"
[ "$CURRENT" = "$BRANCH" ] || die \
  "You are on the branch \"$CURRENT\", but audio work happens on \"$BRANCH\"." \
  "In GitHub Desktop, switch Current Branch to $BRANCH, then run Sync again."

for marker in MERGE_HEAD CHERRY_PICK_HEAD REVERT_HEAD rebase-merge rebase-apply; do
  [ -e "$GIT_DIR_PATH/$marker" ] && die \
    "An earlier merge was left unfinished on this Mac." \
    "Do not commit or pull. Ask $HELP_CONTACT to look at it."
done

if [ -z "${SYNC_SKIP_APP_CHECK:-}" ]; then
  if pgrep -if 'Wwise\.app/Contents/MacOS/|[\\/]Wwise\.exe' >/dev/null 2>&1; then
    die "Wwise is open." "Save your project, quit Wwise, then run Sync again."
  fi
  if pgrep -x Unity >/dev/null 2>&1; then
    note "Unity is open. Save your scene and prefabs in Unity now (File > Save)."
    [ -z "${SYNC_NO_PAUSE:-}" ] && read -r -p "   Press Return once Unity is saved. " _
  fi
fi

# Find the Wwise command-line tool matching this project's Wwise version.
CONSOLE="${SYNC_WWISE_CONSOLE:-}"
if [ -z "$CONSOLE" ]; then
  WWISE_VERSION="$(sed -n 's/.*WwiseVersion="v\([0-9.]*\)".*/\1/p' "$WPROJ" | head -1)"
  for c in /Applications/Audiokinetic/*/Wwise.app/Contents/Tools/WwiseConsole.sh; do
    [ -f "$c" ] || continue
    case "$c" in *"$WWISE_VERSION"*) CONSOLE="$c"; break ;; esac
  done
  [ -n "$CONSOLE" ] || die \
    "Wwise $WWISE_VERSION was not found in /Applications/Audiokinetic." \
    "Install that exact version with the Audiokinetic Launcher, then run Sync again."
fi

# Everything except the rebuilt files must already be committed.
REAL_WORK=(.)
for r in "${REBUILT[@]}"; do REAL_WORK+=(":(exclude)$r"); done
DIRTY="$(git status --porcelain -- "${REAL_WORK[@]}")"
if [ -n "$DIRTY" ]; then
  COUNT="$(printf '%s\n' "$DIRTY" | wc -l | tr -d ' ')"
  die "You have $COUNT change(s) that are not committed yet:" \
      "$(printf '%s\n' "$DIRTY" | head -15 | sed 's/^...\(.*\)/   \1/')" \
      "" \
      "Commit them in GitHub Desktop first (do not press Push or Pull), then run Sync again."
fi
note "Ready."

# Leftover changes to rebuilt files would block the merge. They are rebuilt below.
if [ -n "$(git status --porcelain -- "${REBUILT[@]}")" ]; then
  step "Clearing old soundbank files (they are rebuilt below)"
  for r in "${REBUILT[@]}"; do
    git reset -q HEAD -- "$r" 2>/dev/null
    git checkout -q HEAD -- "$r" 2>/dev/null
    git clean -fdq -- "$r"
  done
  note "Done."
fi

# -------------------------------------------------------- Merge + rebuild
resolve_bank_conflicts() {
  # Take the teammate's version of every conflicted rebuilt file, or delete
  # the file if they deleted it. Which side wins does not matter: they are rebuilt.
  local p
  while IFS= read -r -d '' p; do
    if git cat-file -e ":3:$p" 2>/dev/null; then
      git checkout --theirs -- "$p" && git add -- "$p" || return 1
    else
      git rm -q -f --cached -- "$p" >/dev/null 2>&1
      rm -f -- "$p"
    fi
  done < <(git diff --name-only --diff-filter=U -z -- "${REBUILT[@]}")
}

generate_banks() {
  : > "$WWISE_LOG"
  /bin/sh "$CONSOLE" generate-soundbank "$ROOT/$WPROJ" >"$WWISE_LOG" 2>&1
  if grep -q "Process completed successfully" "$WWISE_LOG"; then
    return 0
  elif grep -q "Process completed with warning" "$WWISE_LOG"; then
    note "Wwise finished with warnings. The details are saved in case $HELP_CONTACT asks."
    return 0
  fi
  return 1
}

PULLED=0
step "Downloading teammates' work"
git fetch --progress origin "$BRANCH" || die \
  "Could not reach GitHub." \
  "Check your internet connection, then run Sync again."
REMOTE="origin/$BRANCH"

INCOMING="$(git rev-list --count "HEAD..$REMOTE")"
if [ "$INCOMING" -eq 0 ]; then
  note "Nothing new from teammates."
else
  note "$INCOMING new save(s) from teammates. Merging with yours..."
  BEFORE="$(git rev-parse HEAD)"
  if ! git merge --no-edit -X no-renames "$REMOTE" >/dev/null 2>"$GIT_DIR_PATH/sync-merge.log"; then
    OTHER="$(git diff --name-only --diff-filter=U -- "${REAL_WORK[@]}")"
    BANK_CONFLICTS="$(git diff --name-only --diff-filter=U -- "${REBUILT[@]}" | wc -l | tr -d ' ')"
    if [ -n "$OTHER" ] || [ "$BANK_CONFLICTS" -eq 0 ]; then
      git merge --abort 2>/dev/null || git reset -q --merge "$BEFORE"
      if [ -n "$OTHER" ]; then
        # Name the teammate(s) whose uploaded work touched each conflicting file.
        BASE="$(git merge-base HEAD "$REMOTE")"
        LIST=""; NAMES=""
        while IFS= read -r f; do
          who="$(git log --no-merges --format=%an "$BASE..$REMOTE" -- "$f" | sort -u)"
          NAMES="$NAMES$who"$'\n'
          who="$(printf '%s' "$who" | paste -sd, - | sed 's/,/, /g')"
          LIST="$LIST   $f${who:+  (also changed by $who)}"$'\n'
        done <<< "$OTHER"
        NAMES="$(printf '%s' "$NAMES" | sed '/^$/d' | sort -u | paste -sd, - | sed -e 's/,/, /g' -e 's/\(.*\), /\1 and /')"
        die "You and a teammate changed the same file(s):" \
            "${LIST%$'\n'}" \
            "" \
            "Nothing was changed on your Mac. Your work is safe and still committed." \
            "Do not press Pull in GitHub Desktop." \
            "" \
            "Ask in the group chat what ${NAMES:-your teammate} changed in the file(s) above," \
            "and discuss together how to resolve the conflict."
      fi
      die "The merge could not start:" "$(sed 's/^/   /' "$GIT_DIR_PATH/sync-merge.log")" \
          "" "Nothing was changed on your Mac. Send a screenshot to $HELP_CONTACT."
    fi
    note "Resolving $BANK_CONFLICTS soundbank conflict(s) automatically..."
    resolve_bank_conflicts && git commit -q --no-edit || {
      git merge --abort 2>/dev/null || git reset -q --merge "$BEFORE"
      die "Could not finish the merge. Nothing was changed on your Mac." \
          "Send a screenshot to $HELP_CONTACT."
    }
  fi
  PULLED=$((PULLED + INCOMING))
  note "Merged."
fi

step "Rebuilding soundbanks with Wwise (this can take a few minutes)"
if ! generate_banks; then
  die "Wwise could not rebuild the soundbanks." \
      "Your work and your teammates' work are merged and safe on this Mac. Do not push yet." \
      "Open Wwise, press Generate All, and if that works run Sync again." \
      "If it does not, send $HELP_CONTACT this file: $WWISE_LOG"
fi
git add -A -- "$BANKS"
if git diff --cached --quiet -- "$BANKS"; then
  note "Soundbanks were already up to date."
else
  CHANGED="$(git diff --cached --name-only -- "$BANKS" | wc -l | tr -d ' ')"
  git commit -q -m "Regenerate soundbanks" || die "Could not save the rebuilt soundbanks. Send a screenshot to $HELP_CONTACT."
  note "Saved $CHANGED rebuilt soundbank file(s)."
fi

OUTGOING="$(git rev-list --count "$REMOTE..HEAD")"

printf '\n\033[32mSYNC COMPLETE\033[0m\n'
[ "$PULLED" -gt 0 ] && echo "You now have your teammates' latest work. Unity will take a moment to reload it."
if [ "$OUTGOING" -gt 0 ]; then
  echo
  printf '\033[1mLast step: in GitHub Desktop, press "Push origin" now.\033[0m\n'
  echo "If the button says \"Pull origin\" instead, a teammate uploaded in the meantime."
  echo "Do not press it. Run Sync again, then push."
else
  echo "Nothing to upload."
fi
echo "You can open Wwise again."
finish 0
