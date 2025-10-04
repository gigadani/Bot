#!/usr/bin/env bash
set -Eeuo pipefail

# Debian/Ubuntu installer for the RSVP Telegram Bot as a systemd service.
# Usage (install from published output):
#   sudo ./scripts/install-service.sh --from /path/to/publish --name rsvp-bot
# Or publish and install from project:
#   sudo ./scripts/install-service.sh --project /path/to/Bot.csproj --name rsvp-bot

SERVICE_NAME="rsvp-bot"
INSTALL_DIR="/opt/${SERVICE_NAME}"
RUN_USER="${SERVICE_NAME}"
RUN_GROUP="${RUN_USER}"
DESCRIPTION="RSVP Telegram Bot"
ENABLE_SERVICE=1
START_SERVICE=1
FROM_DIR=""
PROJECT_FILE=""

fail() { echo "[ERR] $*" >&2; exit 1; }
log() { echo "[INF] $*"; }

need_root() {
  if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
    fail "Run as root (sudo).";
  fi
}

usage() {
  cat <<USAGE
Usage:
  $0 --from <published_dir> [--name NAME] [--install-dir DIR] [--user USER] [--description TEXT] [--no-enable] [--no-start]
  $0 --project <Bot.csproj>     [--name NAME] [--install-dir DIR] [--user USER] [--description TEXT] [--no-enable] [--no-start]

Notes:
  - --from points to 'dotnet publish -c Release -o <dir>' output (must contain Bot.dll)
  - --project will run 'dotnet publish -c Release -o <temp>' automatically
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from) FROM_DIR="$2"; shift 2;;
    --project) PROJECT_FILE="$2"; shift 2;;
    --name) SERVICE_NAME="$2"; INSTALL_DIR="/opt/${SERVICE_NAME}"; RUN_USER="${SERVICE_NAME}"; RUN_GROUP="${RUN_USER}"; shift 2;;
    --install-dir) INSTALL_DIR="$2"; shift 2;;
    --user) RUN_USER="$2"; RUN_GROUP="$2"; shift 2;;
    --description) DESCRIPTION="$2"; shift 2;;
    --no-enable) ENABLE_SERVICE=0; shift;;
    --no-start) START_SERVICE=0; shift;;
    -h|--help) usage; exit 0;;
    *) fail "Unknown arg: $1";;
  esac
done

need_root

if [[ -n "$PROJECT_FILE" && -n "$FROM_DIR" ]]; then
  fail "Use either --project or --from, not both.";
fi

DOTNET_BIN="$(command -v dotnet || true)"
[[ -z "$DOTNET_BIN" ]] && fail "dotnet not found. Install .NET SDK or use --from with a published directory."

WORK_DIR="$(pwd)"
TEMP_BUILD=""
cleanup() { [[ -n "$TEMP_BUILD" && -d "$TEMP_BUILD" ]] && rm -rf "$TEMP_BUILD"; }
trap cleanup EXIT

if [[ -n "$PROJECT_FILE" ]]; then
  [[ -f "$PROJECT_FILE" ]] || fail "Project file not found: $PROJECT_FILE"
  TEMP_BUILD="$(mktemp -d)"
  log "Publishing project to $TEMP_BUILD ..."
  "$DOTNET_BIN" publish "$PROJECT_FILE" -c Release -o "$TEMP_BUILD" >/dev/null
  FROM_DIR="$TEMP_BUILD"
fi

[[ -n "$FROM_DIR" ]] || fail "Provide --from <published_dir> or --project <Bot.csproj>"
[[ -d "$FROM_DIR" ]] || fail "Published dir not found: $FROM_DIR"
[[ -f "$FROM_DIR/Bot.dll" ]] || fail "Expected Bot.dll in $FROM_DIR (is this the publish output?)"

# Create user/group if missing
if ! getent group "$RUN_GROUP" >/dev/null; then
  log "Creating group $RUN_GROUP"; groupadd --system "$RUN_GROUP";
fi
if ! id -u "$RUN_USER" >/dev/null 2>&1; then
  log "Creating user $RUN_USER"; useradd --system --gid "$RUN_GROUP" --shell /usr/sbin/nologin --home-dir "$INSTALL_DIR" "$RUN_USER";
fi

log "Installing to $INSTALL_DIR ..."
mkdir -p "$INSTALL_DIR"
rsync -a --delete "$FROM_DIR/" "$INSTALL_DIR/" 2>/dev/null || {
  rm -rf "$INSTALL_DIR"/*
  cp -a "$FROM_DIR/." "$INSTALL_DIR/"
}
mkdir -p "$INSTALL_DIR/data"
chown -R "$RUN_USER":"$RUN_GROUP" "$INSTALL_DIR"

SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
log "Writing unit file $SERVICE_FILE ..."
cat > "$SERVICE_FILE" <<UNIT
[Unit]
Description=$DESCRIPTION
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$RUN_USER
Group=$RUN_GROUP
WorkingDirectory=$INSTALL_DIR
ExecStart=$(command -v dotnet) $INSTALL_DIR/Bot.dll
Restart=on-failure
RestartSec=5s
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$INSTALL_DIR

[Install]
WantedBy=multi-user.target
UNIT

log "Reloading systemd ..."
systemctl daemon-reload

if [[ $ENABLE_SERVICE -eq 1 ]]; then
  log "Enabling $SERVICE_NAME ..."; systemctl enable "$SERVICE_NAME"
fi

if [[ $START_SERVICE -eq 1 ]]; then
  log "Starting $SERVICE_NAME ..."; systemctl restart "$SERVICE_NAME"
  log "Follow logs: journalctl -u $SERVICE_NAME -f"
else
  log "Service installed. Start with: systemctl start $SERVICE_NAME"
fi

log "Done."

