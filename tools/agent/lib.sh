# shellcheck shell=bash
# Shared functions for the tools/agent scripts. Source it; do not run it.
# Contracts: tools/agent/README.md. Every script prints exactly one summary line last:
#   RESULT <script> <ok|fail|blocked> [<key>=<value> ...] log=<path>
# Exit codes: 0 ok, 1 tests or checks failed, 2 bad usage, 3 build failed,
# 4 environment missing something, 5 lock wait timed out, 124 command timed out.

AGENT_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AGENT_SCRIPT="${AGENT_SCRIPT:-$(basename "$0")}"
AGENT_LOG="${AGENT_LOG:-}"
AGENT_FINISHED=0

# The repo root.
agent_root() {
  git -C "$AGENT_LIB_DIR" rev-parse --show-toplevel
}

# Exits 4, naming the first command that is not on PATH.
agent_require() {
  local cmd
  for cmd in "$@"; do
    command -v "$cmd" >/dev/null 2>&1 || agent_die 4 "missing tool: $cmd (install it or put it on PATH)" missing="$cmd"
  done
}

# Puts dotnet on PATH and loads the host env file (K3).
agent_env() {
  local env_file="${SLIDEWORKS_AGENT_ENV:-$HOME/.config/slideworks/agent.env}"
  if [[ -f "$env_file" ]]; then
    # shellcheck source=/dev/null
    source "$env_file"
  fi
  if [[ -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
    :
  elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
    DOTNET_ROOT="$HOME/.dotnet"
  else
    agent_die 4 "dotnet not found in \$DOTNET_ROOT (${DOTNET_ROOT:-unset}) or \$HOME/.dotnet; set DOTNET_ROOT in $env_file" missing=dotnet
  fi
  export DOTNET_ROOT
  export PATH="$DOTNET_ROOT:$PATH"
  export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
}

# The task key: $AGENT_TASK, or the branch name without wip/.
agent_task() {
  if [[ -n "${AGENT_TASK:-}" ]]; then
    printf '%s\n' "$AGENT_TASK"
    return
  fi
  local branch
  branch="$(git -C "$AGENT_LIB_DIR" rev-parse --abbrev-ref HEAD)"
  printf '%s\n' "${branch#wip/}"
}

# Creates and prints this task's scratch folder.
agent_scratch() {
  local dir
  dir="${AGENT_SCRATCH_ROOT:-${TMPDIR:-/tmp}/slideworks-agent}/$(agent_task)"
  mkdir -p "$dir"
  printf '%s\n' "$dir"
}

# Prints a new log path: <scratch>/<name>-<yyyymmdd-HHMMSS>.log (a -N suffix if that name is taken).
agent_log() {
  local name="${1:?agent_log needs a name}" base path n=2
  base="$(agent_scratch)/$name-$(date +%Y%m%d-%H%M%S)"
  path="$base.log"
  while [[ -e "$path" ]]; do
    path="$base-$n.log"
    n=$((n + 1))
  done
  : >"$path"
  printf '%s\n' "$path"
}

# Runs one command under the shared test lock; exits 5 when the lock wait times out.
# Hold the lock for one command only, so other agents can interleave.
agent_locked() {
  flock -E 5 -w "${AGENT_LOCK_WAIT:-1500}" "${AGENT_LOCK:-/tmp/slideworks-test.lock}" "$@"
}

# Builds a project or the solution (default src/SlideWorks.slnx) under the lock.
# Output is appended to $AGENT_BUILD_LOG when set, else to a new log; AGENT_BUILD_LOG names it after.
# Returns 0, 3 (build failed), 4 (post-build step failed), 5 (lock wait) or 124 (timeout).
agent_build() {
  local target="${1:-src/SlideWorks.slnx}" root rc=0 start
  root="$(agent_root)"
  [[ "$target" == /* || -e "$target" ]] || target="$root/$target"
  AGENT_BUILD_LOG="${AGENT_BUILD_LOG:-$(agent_log build)}"
  start="$(wc -l <"$AGENT_BUILD_LOG")"
  agent_locked timeout -k 10 "${AGENT_BUILD_TIMEOUT:-900}" \
    dotnet build "$target" -nodeReuse:false -tl:off >>"$AGENT_BUILD_LOG" 2>&1 || rc=$?
  if [[ $rc -eq 5 || $rc -eq 124 ]]; then
    return "$rc"
  fi
  if ! tail -n +"$((start + 1))" "$AGENT_BUILD_LOG" | grep -q ' 0 Error(s)'; then
    return 3
  fi
  if [[ -n "${AGENT_POST_BUILD:-}" ]]; then
    # AGENT_POST_BUILD is a command line from the host env file; word splitting is intended.
    # shellcheck disable=SC2086
    $AGENT_POST_BUILD "$root" >>"$AGENT_BUILD_LOG" 2>&1 || return 4
  fi
  return 0
}

# Prints the K1 summary line.
agent_result() {
  local script="$1" status="$2" log="$3"
  shift 3
  local line="RESULT $script $status"
  if [[ $# -gt 0 ]]; then
    line+=" $*"
  fi
  printf '%s log=%s\n' "$line" "$log"
}

# Writes <log>.done with the exit code, after the log is complete.
agent_mark_done() {
  printf '%s\n' "$2" >"$1.done"
}

# The status word for an exit code.
agent_status_for() {
  case "$1" in
    0) echo ok ;;
    2 | 4 | 5) echo blocked ;;
    *) echo fail ;;
  esac
}

# Starts a run: makes its log, prints its path, and makes sure the run always ends with a summary and a .done.
agent_start() {
  AGENT_LOG="$(agent_log "$AGENT_SCRIPT")"
  echo "log: $AGENT_LOG"
  trap 'agent__on_exit $?' EXIT
}

agent__on_exit() {
  local rc="$1"
  if [[ $AGENT_FINISHED -eq 0 && -n "$AGENT_LOG" ]]; then
    AGENT_FINISHED=1
    local line
    line="$(agent_result "$AGENT_SCRIPT" "$(agent_status_for "$rc")" "$AGENT_LOG" "exit=$rc" reason=unexpected-exit)"
    printf '%s\n' "$line" >>"$AGENT_LOG"
    printf '%s\n' "$line"
    agent_mark_done "$AGENT_LOG" "$rc"
  fi
}

# Ends a run: appends the summary to the log, prints it, writes the .done marker and exits.
agent_finish() {
  local rc="$1"
  shift
  AGENT_FINISHED=1
  local line
  line="$(agent_result "$AGENT_SCRIPT" "$(agent_status_for "$rc")" "$AGENT_LOG" "$@")"
  printf '%s\n' "$line" >>"$AGENT_LOG"
  printf '%s\n' "$line"
  agent_mark_done "$AGENT_LOG" "$rc"
  exit "$rc"
}

# Ends a run with a message on stderr (and in the log), a summary line and the exit code.
agent_die() {
  local rc="$1" msg="$2"
  shift 2
  [[ -n "$AGENT_LOG" ]] || AGENT_LOG="$(agent_log "$AGENT_SCRIPT")"
  echo "$AGENT_SCRIPT: $msg" | tee -a "$AGENT_LOG" >&2
  agent_finish "$rc" "$@"
}

# Refuses --repeat-each in any form (exit 2).
agent_refuse_repeat_each() {
  local arg
  for arg in "$@"; do
    case "$arg" in
      --repeat-each*) agent_die 2 "--repeat-each is not allowed: run each test once; CI finds flakes" reason=repeat-each ;;
    esac
  done
}
