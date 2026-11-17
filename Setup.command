#!/bin/bash
# One-time setup for Sync. Double-click to run. Safe to run again.
# On Windows, double-click Setup.bat instead (it runs this file with Git Bash).
#
# Installs (only what is missing): Homebrew and Git LFS on a Mac.
# On Windows, Setup.bat installs Git for Windows, which already includes Git LFS.
#
# Every run writes a log to Logs/Sync/<date>-setup.log.

export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
cd "$(dirname "$0")" || exit 1

HELP_CONTACT="Hubert"
PROBLEMS=0
KEEP_LOGS=30
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1; COMPUTER="PC";  SYNC_NAME="Sync.bat" ;;
  *)                    IS_WINDOWS="";  COMPUTER="Mac"; SYNC_NAME="Sync.command" ;;
esac

# ---------------------------------------------------------------- Log file
# Logs/ is ignored by git (Unity's .gitignore), so logs are never committed.
LOG_DIR="$PWD/Logs/Sync"
mkdir -p "$LOG_DIR" 2>/dev/null
LOG="$LOG_DIR/$(date +%Y-%m-%d_%H-%M-%S)-setup.log"
LOG_NAME="Logs/Sync/${LOG##*/}"
{ : > "$LOG"; } 2>/dev/null || { LOG=/dev/null; LOG_NAME="(no log: the Logs folder is not writable)"; }
ls -1t "$LOG_DIR"/*-setup.log 2>/dev/null | tail -n +$((KEEP_LOGS + 1)) | while IFS= read -r f; do rm -f -- "$f"; done
OUT_TMP="$(mktemp 2>/dev/null || echo "${TMPDIR:-/tmp}/setup-output.$$")"

# ----------------------------------------------------------------- Output
BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; RESET=$'\033[0m'
NO_COLOR=$'s/\033\\[[0-9;]*m//g'

log()  { printf '%s\n' "$@" >> "$LOG"; }                                     # log only
show() { printf '%s\n' "$@"; printf '%s\n' "$@" | sed "$NO_COLOR" >> "$LOG"; }  # window + log
step() { show "" "$BOLD== $1$RESET"; log "   [$(date +%H:%M:%S)]"; }
ok()   { show "   ${GREEN}OK$RESET  $1"; }
warn() { show "   ${YELLOW}WARNING$RESET  $1"; PROBLEMS=$((PROBLEMS + 1)); }

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
# Run a command whose output the user should see, and log a tidy copy.
shown() {
  local rc
  log "\$ $*"
  "$@" 2>&1 | tee "$OUT_TMP"; rc=${PIPESTATUS[0]}
  [ -n "$(tail -c 1 "$OUT_TMP" 2>/dev/null)" ] && echo   # end an unfinished progress line
  log_output "$OUT_TMP"
  [ "$rc" -eq 0 ] || log "   -> exit code $rc"
  return "$rc"
}

finish() {
  trap - INT HUP TERM
  rm -f -- "$OUT_TMP"
  log "" "Finished $(date '+%Y-%m-%d %H:%M:%S') with exit code $1 and $PROBLEMS warning(s)."
  echo
  echo "A record of this run is saved in $LOG_NAME"
  [ -z "${SETUP_NO_PAUSE:-}" ] && read -r -p "Press Return to close this window. " _
  exit "$1"
}
die() {
  show "" "${RED}SETUP STOPPED$RESET" "$1" "" \
       "Send $HELP_CONTACT the log file named below. (It is selected in the window that just opened.)"
  if [ -z "${SETUP_NO_PAUSE:-}" ] && [ "$LOG" != /dev/null ]; then
    if [ -n "$IS_WINDOWS" ]; then
      MSYS_NO_PATHCONV=1 explorer.exe "/select,$(cygpath -w "$LOG")" >/dev/null 2>&1
    else
      open -R "$LOG" >/dev/null 2>&1
    fi
  fi
  finish 1
}
trap 'log "" "STOPPED EARLY: the window was closed or Ctrl-C was pressed."; rm -f -- "$OUT_TMP"; exit 130' INT HUP TERM

if [ -n "$IS_WINDOWS" ]; then OS_VERSION="$(uname -sr)"; else OS_VERSION="macOS $(sw_vers -productVersion 2>/dev/null), $(uname -m)"; fi
log "Setup started $(date '+%Y-%m-%d %H:%M:%S')" \
    "Computer: $COMPUTER, $OS_VERSION" \
    "Folder:   $PWD"

show "This sets up your $COMPUTER so $SYNC_NAME can work. You only need to do it once."
[ -z "$IS_WINDOWS" ] && show "It may ask for your Mac password."

# ---------------------------------------------------------------- Homebrew
if [ -n "$IS_WINDOWS" ]; then
  step "Git for Windows"
  ok "installed"
else
  step "Homebrew (the installer for the other tools)"
  if command -v brew >/dev/null 2>&1; then
    log "   $(brew --version 2>/dev/null | head -1) at $(command -v brew)"
    ok "already installed"
  else
    show "   Installing Homebrew. This can take 10 to 20 minutes." \
         "   When it asks for a password, type your Mac password (nothing shows as you type) and press Return."
    # Not piped through the log: the installer needs the window to ask for the password.
    log "\$ Homebrew install.sh (output only in the window)"
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    rc=$?
    log "   -> exit code $rc"
    [ "$rc" -eq 0 ] || die "Homebrew did not install."
    export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
    command -v brew >/dev/null 2>&1 || die "Homebrew installed but cannot be found."
    ok "installed"
  fi
fi

# ------------------------------------------------------------------ Tools
step "Git and Git LFS"
command -v git >/dev/null 2>&1 || die "Git is missing. It normally comes with Homebrew's install."
log "   $(git --version) at $(command -v git)"
ok "git"
for tool in git-lfs; do
  if command -v "$tool" >/dev/null 2>&1; then
    log "   $(git lfs version 2>&1 | head -1)"
    ok "$tool already installed"
  else
    [ -n "$IS_WINDOWS" ] && die "$tool is missing. Reinstall Git for Windows from https://git-scm.com and run Setup again."
    show "   Installing $tool..."
    shown brew install "$tool" || die "Could not install $tool."
    ok "$tool installed"
  fi
done
quiet git lfs install || die "Could not switch on Git LFS."
ok "Git LFS switched on"

# Check this copy of the project actually uses LFS, and that the large files
# are real audio rather than small placeholder text files.
if git check-attr filter -- check.wav | grep -q 'lfs$'; then
  PLACEHOLDERS="$(git lfs ls-files 2>/dev/null | awk '$2 == "-"' | wc -l | tr -d ' ')"
  log "   Large files not downloaded yet: $PLACEHOLDERS"
  if [ "$PLACEHOLDERS" -gt 0 ]; then
    show "   $PLACEHOLDERS large file(s) are not downloaded yet. Downloading (this can take a while)..."
    shown git lfs pull || die "Could not download the large files. Check your internet connection and run Setup again."
  fi
  ok "all large files are downloaded"
else
  warn "This copy of the project is from before the team switched to Git LFS. Do not use Sync on it. Ask $HELP_CONTACT how to move your work to a fresh copy."
fi

# -------------------------------------------------------- Project access
step "Project on GitHub"
log "   Branch: $(git branch --show-current 2>/dev/null) at $(git rev-parse --short HEAD 2>/dev/null)" \
    "   Remote: $(git remote get-url origin 2>/dev/null)"
if [ -n "$(git config user.name)" ] && [ -n "$(git config user.email)" ]; then
  ok "your name for saved work is \"$(git config user.name)\""
else
  warn "Git does not know your name yet. In GitHub Desktop open Settings > Git, fill in Name and Email, and press Save."
fi
if quiet git ls-remote --exit-code origin HEAD; then
  ok "can download from the project"
else
  warn "Cannot reach the project on GitHub. Check your internet connection and run Setup again."
fi

# ------------------------------------------------------------------ Wwise
step "Wwise"
WPROJ="$(ls TheRoomINeverLeft_WwiseProject/*.wproj 2>/dev/null | head -1)"
WWISE_VERSION="$(sed -n 's/.*WwiseVersion="v\([0-9.]*\)".*/\1/p' "$WPROJ" 2>/dev/null | head -1)"
CONSOLE=""
if [ -n "$IS_WINDOWS" ]; then
  WWISE_DIRS="C:\\Program Files\\Audiokinetic or C:\\Program Files (x86)\\Audiokinetic"
  CANDIDATES=("/c/Program Files/Audiokinetic/"*/Authoring/x64/Release/bin/WwiseConsole.exe
              "/c/Program Files (x86)/Audiokinetic/"*/Authoring/x64/Release/bin/WwiseConsole.exe)
else
  WWISE_DIRS="/Applications/Audiokinetic"
  CANDIDATES=(/Applications/Audiokinetic/*/Wwise.app/Contents/Tools/WwiseConsole.sh)
fi
log "   Project needs Wwise $WWISE_VERSION. Installed:"
for c in "${CANDIDATES[@]}"; do
  [ -f "$c" ] || continue
  log "     $c"
  case "$c" in *"$WWISE_VERSION"*) [ -z "$CONSOLE" ] && CONSOLE="$c" ;; esac
done
if [ -n "$CONSOLE" ]; then
  ok "Wwise $WWISE_VERSION found"
else
  warn "Wwise $WWISE_VERSION was not found in $WWISE_DIRS. Install that exact version with the Audiokinetic Launcher."
fi
if [ -n "$IS_WINDOWS" ]; then
  # On Windows, Wwise plugins live inside the Wwise install itself.
  STEAM_AUDIO="${CONSOLE%/*}/Plugins/SteamAudioWwise.dll"
else
  STEAM_AUDIO="$(ls "/Library/Application Support/Audiokinetic/"*"$WWISE_VERSION"*/Authoring/x64/Release/bin/Plugins/SteamAudioWwise.dll 2>/dev/null | head -1)"
fi
log "   Steam Audio plugin: ${STEAM_AUDIO:-(not found)}"
if [ -f "$STEAM_AUDIO" ]; then
  ok "Steam Audio plugin found"
else
  warn "The Steam Audio plugin for Wwise was not found. Without it your soundbanks will come out different from everyone else's. Ask $HELP_CONTACT how to install it."
fi

# ------------------------------------------------------------------- Done
show ""
if [ "$PROBLEMS" -eq 0 ]; then
  show "${GREEN}Setup complete.$RESET From now on, use $SYNC_NAME."
else
  show "${YELLOW}Setup finished with $PROBLEMS warning(s) above.$RESET Fix those before using $SYNC_NAME."
fi
finish 0
