#!/bin/bash
# One-time setup for Sync. Double-click to run. Safe to run again.
# On Windows, double-click Setup.bat instead (it runs this file with Git Bash).
#
# Installs (only what is missing): Homebrew and Git LFS on a Mac.
# On Windows, Setup.bat installs Git for Windows, which already includes Git LFS.

export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
cd "$(dirname "$0")" || exit 1

HELP_CONTACT="Hubert"
PROBLEMS=0
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1; COMPUTER="PC";  SYNC_NAME="Sync.bat" ;;
  *)                    IS_WINDOWS="";  COMPUTER="Mac"; SYNC_NAME="Sync.command" ;;
esac

step() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
ok()   { printf '   \033[32mOK\033[0m  %s\n' "$1"; }
warn() { printf '   \033[33mWARNING\033[0m  %s\n' "$1"; PROBLEMS=$((PROBLEMS + 1)); }
finish() {
  echo
  [ -z "${SETUP_NO_PAUSE:-}" ] && read -r -p "Press Return to close this window. " _
  exit "$1"
}
die() {
  printf '\n\033[31mSETUP STOPPED\033[0m\n%s\n' "$1"
  echo "Send a screenshot of this window to $HELP_CONTACT."
  finish 1
}

echo "This sets up your $COMPUTER so $SYNC_NAME can work. You only need to do it once."
[ -z "$IS_WINDOWS" ] && echo "It may ask for your Mac password."

# ---------------------------------------------------------------- Homebrew
if [ -n "$IS_WINDOWS" ]; then
  step "Git for Windows"
  ok "installed"
else
  step "Homebrew (the installer for the other tools)"
  if command -v brew >/dev/null 2>&1; then
    ok "already installed"
  else
    echo "   Installing Homebrew. This can take 10 to 20 minutes."
    echo "   When it asks for a password, type your Mac password (nothing shows as you type) and press Return."
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)" \
      || die "Homebrew did not install."
    export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
    command -v brew >/dev/null 2>&1 || die "Homebrew installed but cannot be found."
    ok "installed"
  fi
fi

# ------------------------------------------------------------------ Tools
step "Git and Git LFS"
command -v git >/dev/null 2>&1 || die "Git is missing. It normally comes with Homebrew's install."
ok "git"
for tool in git-lfs; do
  if command -v "$tool" >/dev/null 2>&1; then
    ok "$tool already installed"
  else
    [ -n "$IS_WINDOWS" ] && die "$tool is missing. Reinstall Git for Windows from https://git-scm.com and run Setup again."
    echo "   Installing $tool..."
    brew install "$tool" || die "Could not install $tool."
    ok "$tool installed"
  fi
done
git lfs install >/dev/null 2>&1 || die "Could not switch on Git LFS."
ok "Git LFS switched on"

# Check this copy of the project actually uses LFS, and that the large files
# are real audio rather than small placeholder text files.
if git check-attr filter -- check.wav | grep -q 'lfs$'; then
  PLACEHOLDERS="$(git lfs ls-files 2>/dev/null | awk '$2 == "-"' | wc -l | tr -d ' ')"
  if [ "$PLACEHOLDERS" -gt 0 ]; then
    echo "   $PLACEHOLDERS large file(s) are not downloaded yet. Downloading (this can take a while)..."
    git lfs pull || die "Could not download the large files. Check your internet connection and run Setup again."
  fi
  ok "all large files are downloaded"
else
  warn "This copy of the project is from before the team switched to Git LFS. Do not use Sync on it. Ask $HELP_CONTACT how to move your work to a fresh copy."
fi

# -------------------------------------------------------- Project access
step "Project on GitHub"
if [ -n "$(git config user.name)" ] && [ -n "$(git config user.email)" ]; then
  ok "your name for saved work is \"$(git config user.name)\""
else
  warn "Git does not know your name yet. In GitHub Desktop open Settings > Git, fill in Name and Email, and press Save."
fi
if git ls-remote --exit-code origin HEAD >/dev/null 2>&1; then
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
for c in "${CANDIDATES[@]}"; do
  [ -f "$c" ] || continue
  case "$c" in *"$WWISE_VERSION"*) CONSOLE="$c"; break ;; esac
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
if [ -f "$STEAM_AUDIO" ]; then
  ok "Steam Audio plugin found"
else
  warn "The Steam Audio plugin for Wwise was not found. Without it your soundbanks will come out different from everyone else's. Ask $HELP_CONTACT how to install it."
fi

# ------------------------------------------------------------------- Done
echo
if [ "$PROBLEMS" -eq 0 ]; then
  printf '\033[32mSetup complete.\033[0m From now on, use %s.\n' "$SYNC_NAME"
else
  printf '\033[33mSetup finished with %s warning(s) above.\033[0m Fix those before using %s.\n' "$PROBLEMS" "$SYNC_NAME"
fi
finish 0
