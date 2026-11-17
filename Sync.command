#!/bin/bash
# Sync your work with the team. Double-click to run.
# On Windows, double-click Sync.bat instead (it runs this file with Git Bash).
#
# Before running: commit your work in GitHub Desktop, then save and close Wwise.
#
# What it does:
#   1. Downloads teammates' work and merges it with yours.
#   2. Soundbank conflicts are resolved automatically (banks get rebuilt anyway).
#   3. Any other conflict cancels the merge and leaves your committed work exactly as it was.
#   4. Rebuilds all soundbanks with Wwise and saves them.
#
# After it finishes: press "Push origin" in GitHub Desktop to upload.
#
# Every run writes a log to Logs/Sync/<date>-sync.log: everything shown in the
# window, plus the git and Wwise output the window leaves out.

export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
export GIT_LFS_FORCE_PROGRESS=1
cd "$(dirname "$0")" || exit 1

# ---------------------------------------------------------------- Settings
BRANCH="${SYNC_BRANCH:-wwise-integration}"
BANKS="Assets/StreamingAssets/Audio/GeneratedSoundBanks"
# Files that tools rebuild on every computer. Local changes to them are thrown away
# and conflicts in them are resolved automatically.
REBUILT=("$BANKS" "Assets/Wwise/Editor/ProjectData")
WPROJ="TheRoomINeverLeft_WwiseProject/TheRoomINeverLeft_WwiseProject.wproj"
HELP_CONTACT="Hubert"
KEEP_LOGS=30
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1; COMPUTER="PC";  SETUP_NAME="Setup.bat" ;;
  *)                    IS_WINDOWS="";  COMPUTER="Mac"; SETUP_NAME="Setup.command" ;;
esac

# ---------------------------------------------------------------- Log file
# Logs/ is ignored by git (Unity's .gitignore), so logs are never committed.
LOG_DIR="$PWD/Logs/Sync"
mkdir -p "$LOG_DIR" 2>/dev/null
LOG="$LOG_DIR/$(date +%Y-%m-%d_%H-%M-%S)-sync.log"
LOG_NAME="Logs/Sync/${LOG##*/}"
{ : > "$LOG"; } 2>/dev/null || { LOG=/dev/null; LOG_NAME="(no log: the Logs folder is not writable)"; }
ls -1t "$LOG_DIR"/*-sync.log 2>/dev/null | tail -n +$((KEEP_LOGS + 1)) | while IFS= read -r f; do rm -f -- "$f"; done
OUT_TMP="$(mktemp 2>/dev/null || echo "${TMPDIR:-/tmp}/sync-output.$$")"

# ----------------------------------------------------------------- Output
BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'; RESET=$'\033[0m'
NO_COLOR=$'s/\033\\[[0-9;]*m//g'

log()  { printf '%s\n' "$@" >> "$LOG"; }                                     # log only
show() { printf '%s\n' "$@"; printf '%s\n' "$@" | sed "$NO_COLOR" >> "$LOG"; }  # window + log
step() { show "" "$BOLD== $1$RESET"; log "   [$(date +%H:%M:%S)]"; }
note() { show "   $1"; }

# Append command output to the log, keeping only the final state of each progress bar.
log_output() {
  tr '\r' '\n' < "$1" | sed "$NO_COLOR" | awk '
    /[0-9]+% \(/ { key = $0; sub(/[0-9]+% .*/, "", key)
                   if (pending && key != pkey) print last
                   pkey = key; last = $0; pending = 1; next }
    { if (pending) { print last; pending = 0; pkey = "" } if ($0 != "") print }
    END { if (pending) print last }' | sed 's/^/   | /' >> "$LOG"
}
# Run a command whose output belongs only in the log.
quiet() {
  local rc
  log "\$ $*"
  "$@" > "$OUT_TMP" 2>&1; rc=$?
  log_output "$OUT_TMP"
  [ "$rc" -eq 0 ] || log "   -> exit code $rc"
  return "$rc"
}
# Run a command whose output (progress bars) the user should see, and log a tidy copy.
shown() {
  local rc
  log "\$ $*"
  "$@" 2>&1 | tee "$OUT_TMP"; rc=${PIPESTATUS[0]}
  [ -n "$(tail -c 1 "$OUT_TMP" 2>/dev/null)" ] && echo   # end an unfinished progress line
  log_output "$OUT_TMP"
  [ "$rc" -eq 0 ] || log "   -> exit code $rc"
  return "$rc"
}

log_state() {
  git rev-parse --git-dir >/dev/null 2>&1 || return
  log "" "--- Project state ---"
  git status --short --branch 2>&1 | head -40 >> "$LOG"
  log "Recent history:"
  git log --oneline --graph -8 "HEAD" "origin/$BRANCH" -- 2>/dev/null >> "$LOG"
}
finish() {
  trap - INT HUP TERM
  rm -f -- "$OUT_TMP"
  log "" "Finished $(date '+%Y-%m-%d %H:%M:%S') with exit code $1."
  echo
  echo "A record of this run is saved in $LOG_NAME"
  [ -z "${SYNC_NO_PAUSE:-}" ] && read -r -p "Press Return to close this window. " _
  exit "$1"
}
# Stop for something the user can fix themselves.
die() {
  show "" "${RED}SYNC STOPPED$RESET" "$@"
  log_state
  finish 1
}
# Stop for something only HELP_CONTACT can sort out: point the user at the log file.
die_help() {
  show "" "${RED}SYNC STOPPED$RESET" "$@" "" \
       "Send $HELP_CONTACT the log file named below. (It is selected in the window that just opened.)"
  log_state
  if [ -z "${SYNC_NO_PAUSE:-}" ] && [ "$LOG" != /dev/null ]; then
    if [ -n "$IS_WINDOWS" ]; then
      MSYS_NO_PATHCONV=1 explorer.exe "/select,$(cygpath -w "$LOG")" >/dev/null 2>&1
    else
      open -R "$LOG" >/dev/null 2>&1
    fi
  fi
  finish 1
}
trap 'log "" "STOPPED EARLY: the window was closed or Ctrl-C was pressed."; log_state; rm -f -- "$OUT_TMP"; exit 130' INT HUP TERM

log "Sync started $(date '+%Y-%m-%d %H:%M:%S')" \
    "Computer: $COMPUTER, $(uname -srm)" \
    "Folder:   $PWD"

# ------------------------------------------------------------ Preflight
step "Checking your setup"

command -v git >/dev/null 2>&1 && git lfs version >/dev/null 2>&1 \
  || die "Git or Git LFS is not installed." "Double-click $SETUP_NAME first, then run Sync again."

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || die "This file must stay inside the project folder."
cd "$ROOT" || exit 1
GIT_DIR_PATH="$(git rev-parse --absolute-git-dir)"
WWISE_LOG="$GIT_DIR_PATH/sync-wwise.log"
HEAD_AT_START="$(git rev-parse --short HEAD)"
log "Git:      $(git --version), $(git lfs version 2>&1 | head -1)" \
    "Name:     $(git config user.name)" \
    "Branch:   $(git branch --show-current) at $HEAD_AT_START"

[ -n "$(git config user.name)" ] && [ -n "$(git config user.email)" ] || die \
  "Git does not know your name yet." \
  "In GitHub Desktop open Settings > Git, fill in Name and Email, press Save, then run Sync again."

CURRENT="$(git branch --show-current)"
[ "$CURRENT" = "$BRANCH" ] || die \
  "You are on the branch \"$CURRENT\", but audio work happens on \"$BRANCH\"." \
  "In GitHub Desktop, switch Current Branch to $BRANCH, then run Sync again."

for marker in MERGE_HEAD CHERRY_PICK_HEAD REVERT_HEAD rebase-merge rebase-apply; do
  [ -e "$GIT_DIR_PATH/$marker" ] && die_help \
    "An earlier merge was left unfinished on this $COMPUTER ($marker)." \
    "Do not commit or pull."
done

if [ -z "${SYNC_SKIP_APP_CHECK:-}" ]; then
  if [ -n "$IS_WINDOWS" ]; then
    # Double slashes stop Git Bash from turning /NH into a file path.
    PROCS="$(tasklist //NH //FO CSV 2>/dev/null)"
    wwise_open() { printf '%s\n' "$PROCS" | grep -qi '^"Wwise\.exe"'; }
    unity_open() { printf '%s\n' "$PROCS" | grep -qi '^"Unity\.exe"'; }
  else
    wwise_open() { pgrep -if 'Wwise\.app/Contents/MacOS/|[\\/]Wwise\.exe' >/dev/null 2>&1; }
    unity_open() { pgrep -x Unity >/dev/null 2>&1; }
  fi
  if wwise_open; then
    die "Wwise is open." "Save your project, quit Wwise, then run Sync again."
  fi
  if unity_open; then
    note "Unity is open. Save your scene and prefabs in Unity now (File > Save)."
    if [ -z "${SYNC_NO_PAUSE:-}" ]; then
      read -r -p "   Press Return once Unity is saved. " _
      log "   (Return pressed at $(date +%H:%M:%S))"
    fi
  fi
fi

# Find the Wwise command-line tool matching this project's Wwise version.
CONSOLE="${SYNC_WWISE_CONSOLE:-}"
if [ -z "$CONSOLE" ]; then
  WWISE_VERSION="$(sed -n 's/.*WwiseVersion="v\([0-9.]*\)".*/\1/p' "$WPROJ" | head -1)"
  if [ -n "$IS_WINDOWS" ]; then
    WWISE_DIRS="C:\\Audiokinetic"
    CANDIDATES=("/c/Audiokinetic/"*/Authoring/x64/Release/bin/WwiseConsole.exe
                "/c/Audiokinetic/"*/Authoring/x64/Release/bin/WwiseConsole.exe)
  else
    WWISE_DIRS="/Applications/Audiokinetic"
    CANDIDATES=(/Applications/Audiokinetic/*/Wwise.app/Contents/Tools/WwiseConsole.sh)
  fi
  for c in "${CANDIDATES[@]}"; do
    [ -f "$c" ] || continue
    case "$c" in *"$WWISE_VERSION"*) CONSOLE="$c"; break ;; esac
  done
  [ -n "$CONSOLE" ] || die \
    "Wwise $WWISE_VERSION was not found in $WWISE_DIRS." \
    "Install that exact version with the Audiokinetic Launcher, then run Sync again."
fi
log "Wwise:    $CONSOLE"

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

# -------------------------------------------------------- Merge + rebuild
resolve_bank_conflicts() {
  # Take the teammate's version of every conflicted rebuilt file, or delete
  # the file if they deleted it. Which side wins does not matter: they are rebuilt.
  local p
  while IFS= read -r -d '' p; do
    if git cat-file -e ":3:$p" 2>/dev/null; then
      log "   theirs:  $p"
      git checkout --theirs -- "$p" >> "$LOG" 2>&1 && git add -- "$p" >> "$LOG" 2>&1 || return 1
    else
      log "   deleted: $p"
      git rm -q -f --cached -- "$p" >> "$LOG" 2>&1
      rm -f -- "$p"
    fi
  done < <(git diff --name-only --diff-filter=U -z -- "${REBUILT[@]}")
}

generate_banks() {
  local rc
  log "\$ $CONSOLE generate-soundbank $WPROJ"
  : > "$WWISE_LOG"
  if [ -n "$IS_WINDOWS" ]; then
    "$CONSOLE" generate-soundbank "$(cygpath -w "$ROOT/$WPROJ")" >"$WWISE_LOG" 2>&1
  else
    /bin/sh "$CONSOLE" generate-soundbank "$ROOT/$WPROJ" >"$WWISE_LOG" 2>&1
  fi
  rc=$?
  log_output "$WWISE_LOG"
  log "   -> exit code $rc"
  if grep -q "Process completed successfully" "$WWISE_LOG"; then
    return 0
  elif grep -q "Process completed with warning" "$WWISE_LOG"; then
    note "Wwise finished with warnings. They are in the log in case $HELP_CONTACT asks."
    return 0
  fi
  return 1
}

PULLED=0
# What to tell the user when a merge is cancelled. Replaced below if soundbanks get cleared.
UNTOUCHED="Nothing was changed on your $COMPUTER. Your work is safe and still committed."
step "Downloading teammates' work"
shown git fetch --progress origin "$BRANCH" || die \
  "Could not reach GitHub." \
  "Check your internet connection, then run Sync again."
REMOTE="origin/$BRANCH"
log "   $REMOTE is at $(git rev-parse --short "$REMOTE")"

INCOMING="$(git rev-list --count "HEAD..$REMOTE")"
log "   Incoming commits:" "$(git log --format='     %h %an: %s' "HEAD..$REMOTE")"
if [ "$INCOMING" -eq 0 ]; then
  note "Nothing new from teammates."
else
  note "$INCOMING new save(s) from teammates. Downloading their audio files..."
  # Download the large files now, with progress, so the merge below is quick.
  shown git lfs fetch origin "$REMOTE" || die \
    "Could not download your teammates' audio files." \
    "Nothing was changed on your $COMPUTER. Check your internet connection, then run Sync again."

  # Leftover changes to rebuilt files would block the merge. They are rebuilt below.
  if [ -n "$(git status --porcelain -- "${REBUILT[@]}")" ]; then
    note "Clearing old soundbank files (they are rebuilt below)..."
    git status --porcelain -- "${REBUILT[@]}" | sed 's/^/   cleared: /' >> "$LOG"
    for r in "${REBUILT[@]}"; do
      if [ -n "$(git ls-files -- "$r" | head -1)" ]; then   # skip folders git does not track
        quiet git reset -q HEAD -- "$r"
        quiet git checkout -q HEAD -- "$r"
      fi
      quiet git clean -fdq -- "$r"
    done
    UNTOUCHED="Your work is safe and still committed."$'\n'"Only soundbanks you built but had not committed were reset. Press Generate All in Wwise to get them back."
  fi

  note "Merging with yours..."
  BEFORE="$(git rev-parse HEAD)"
  if ! quiet git merge --no-edit -X no-renames "$REMOTE"; then
    MERGE_OUTPUT="$(sed 's/^/   /' "$OUT_TMP")"
    OTHER="$(git diff --name-only --diff-filter=U -- "${REAL_WORK[@]}")"
    BANK_CONFLICTS="$(git diff --name-only --diff-filter=U -- "${REBUILT[@]}" | wc -l | tr -d ' ')"
    if [ -n "$OTHER" ]; then
      log "   Conflicts: $BANK_CONFLICTS soundbank file(s), plus these other files:" "$(printf '%s\n' "$OTHER" | sed 's/^/     /')"
    else
      log "   Conflicts: $BANK_CONFLICTS soundbank file(s), no other files."
    fi
    if [ -n "$OTHER" ] || [ "$BANK_CONFLICTS" -eq 0 ]; then
      quiet git merge --abort || quiet git reset -q --merge "$BEFORE"
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
            "$UNTOUCHED" \
            "Do not press Pull in GitHub Desktop." \
            "" \
            "Ask in the group chat what ${NAMES:-your teammate} changed in the file(s) above," \
            "and discuss together how to resolve the conflict."
      fi
      die_help "The merge could not start:" "$MERGE_OUTPUT" "" "$UNTOUCHED"
    fi
    note "Resolving $BANK_CONFLICTS soundbank conflict(s) automatically..."
    resolve_bank_conflicts && quiet git commit -q --no-edit || {
      quiet git merge --abort || quiet git reset -q --merge "$BEFORE"
      die_help "Could not finish the merge." "$UNTOUCHED"
    }
  fi
  PULLED=$((PULLED + INCOMING))
  log "   HEAD is now $(git rev-parse --short HEAD)"
  note "Merged."
fi

step "Rebuilding soundbanks with Wwise (this can take a few minutes)"
if ! generate_banks; then
  die_help "Wwise could not rebuild the soundbanks." \
      "Your work and your teammates' work are merged and safe on this $COMPUTER. Do not push yet." \
      "Open Wwise, press Generate All, and if that works run Sync again."
fi
git add -A -- "$BANKS"
if git diff --cached --quiet -- "$BANKS"; then
  note "Soundbanks were already up to date."
else
  CHANGED="$(git diff --cached --name-only -- "$BANKS" | wc -l | tr -d ' ')"
  git diff --cached --name-status -- "$BANKS" | sed 's/^/   rebuilt: /' >> "$LOG"
  quiet git commit -q -m "Regenerate soundbanks" || die_help "Could not save the rebuilt soundbanks."
  note "Saved $CHANGED rebuilt soundbank file(s)."
fi

OUTGOING="$(git rev-list --count "$REMOTE..HEAD")"
log "" "Commits waiting to be pushed:" "$(git log --format='   %h %an: %s' "$REMOTE..HEAD")"
log_state

show "" "${GREEN}SYNC COMPLETE$RESET"
[ "$PULLED" -gt 0 ] && show "You now have your teammates' latest work. Unity will take a moment to reload it."
if [ "$OUTGOING" -gt 0 ]; then
  show "" "${BOLD}Last step: in GitHub Desktop, press \"Push origin\" now.$RESET" \
       "If the button says \"Pull origin\" instead, a teammate uploaded in the meantime." \
       "Do not press it. Run Sync again, then push."
else
  show "Nothing to upload."
fi
show "You can open Wwise again."
finish 0
