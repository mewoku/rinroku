#!/usr/bin/env bash
# Odlet self-host bootstrap: fresh Ubuntu VPS (22.04/24.04) → site on https://$DOMAIN.
#
#   curl -fsSL https://raw.githubusercontent.com/<you>/RONRIKU/main/deploy/up.sh -o up.sh
#   DOMAIN=odlet.xyz REPO_URL=https://github.com/<you>/RONRIKU.git bash up.sh
#
# Or from an existing checkout:   bash deploy/up.sh      (DOMAIN defaults to odlet.xyz)
# Local (Docker Desktop, Git Bash): bash deploy/up.sh --local      (http://localhost:8080)
#
# Idempotent: safe to re-run after `git pull` (rebuilds, applies new migrations, keeps data).
# Flags:
#   --local        skip OS packages / firewall / cron; bind to 127.0.0.1:8080, plain HTTP
#   --no-install   skip OS packages (Docker, Node) even on a server
#   --reset        wipe the database (`supabase db reset`). Server: automatic on the first run only.
#                  Local: never automatic (your dev data is kept) — pass --reset on a fresh clone.
# Env: DOMAIN (default odlet.xyz), REPO_URL, APP_DIR (default ~/RONRIKU), SUPABASE_EXCLUDE (e.g. "studio,imgproxy"),
#      PAYMENT_RECIPIENT (default: owner wallet), SUPABASE_CLI (default supabase@2.118.0).
set -euo pipefail

LOCAL=0 INSTALL=1 RESET=0
for a in "$@"; do
  case "$a" in
    --local) LOCAL=1 INSTALL=0 ;;
    --no-install) INSTALL=0 ;;
    --reset) RESET=1 ;;
    *) echo "unknown flag $a" >&2; exit 2 ;;
  esac
done

SUPABASE_CLI="${SUPABASE_CLI:-supabase@2.118.0}"
supa() { npx --yes "$SUPABASE_CLI" "$@"; }
log() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\033[1;31merror: %s\033[0m\n' "$*" >&2; exit 1; }
SUDO=""; [ "$(id -u)" -ne 0 ] && SUDO="sudo"

# ---------------------------------------------------------------- 1. OS packages (server only)
if [ "$INSTALL" = 1 ]; then
  [ -r /etc/os-release ] && . /etc/os-release
  [ "${ID:-}" = "ubuntu" ] || die "OS install supports Ubuntu only (use --no-install elsewhere)."
  log "Installing base packages, Docker, Node 24"
  $SUDO apt-get update -y
  $SUDO apt-get install -y ca-certificates curl git ufw openssl
  if ! command -v docker >/dev/null; then
    curl -fsSL https://get.docker.com | $SUDO sh
  fi
  # Published container ports default to 127.0.0.1: Postgres (54322), Studio (54323), Kong (54321)
  # and Mailpit (54324) from the Supabase CLI are NOT reachable from the internet. Docker bypasses
  # ufw for published ports, so this (not ufw) is what protects them. Caddy binds 0.0.0.0 explicitly.
  if [ ! -f /etc/docker/daemon.json ]; then
    echo '{ "ip": "127.0.0.1", "log-driver": "local" }' | $SUDO tee /etc/docker/daemon.json >/dev/null
    $SUDO systemctl restart docker
  elif ! grep -q '"ip": *"127.0.0.1"' /etc/docker/daemon.json; then
    die "/etc/docker/daemon.json exists without \"ip\": \"127.0.0.1\" — add it (see deploy/README.md) and re-run."
  fi
  $SUDO usermod -aG docker "$USER" || true
  if ! command -v node >/dev/null || [ "$(node -p 'process.versions.node.split(".")[0]')" -lt 24 ]; then
    curl -fsSL https://deb.nodesource.com/setup_24.x | $SUDO -E bash -
    $SUDO apt-get install -y nodejs
  fi
  $SUDO corepack enable
  corepack prepare pnpm@9.15.4 --activate
  log "Firewall: allow SSH, HTTP, HTTPS only"
  $SUDO ufw allow OpenSSH
  $SUDO ufw allow 80/tcp
  $SUDO ufw allow 443/tcp
  $SUDO ufw allow 443/udp
  $SUDO ufw --force enable
fi

if ! docker info >/dev/null 2>&1; then
  # Fresh install: this shell is not in the docker group yet (the Supabase CLI needs it).
  if [ -z "${RONRIKU_SG:-}" ] && command -v sg >/dev/null && $SUDO docker info >/dev/null 2>&1; then
    exec sg docker -c "RONRIKU_SG=1 bash '$0' $*"
  fi
  die "Docker is not reachable for $USER (log out and back in after install, then re-run)."
fi
DOCKER="docker"

# ---------------------------------------------------------------- 2. Code
if [ -f "$(dirname "$0")/docker-compose.yml" ] && [ -d "$(dirname "$0")/../web" ]; then
  ROOT="$(cd "$(dirname "$0")/.." && pwd)"
else
  ROOT="${APP_DIR:-$HOME/RONRIKU}"
  [ -n "${REPO_URL:-}" ] || die "Set REPO_URL (or run from a checkout)."
  if [ -d "$ROOT/.git" ]; then git -C "$ROOT" pull --ff-only; else git clone "$REPO_URL" "$ROOT"; fi
fi
cd "$ROOT"
ENV_FILE="$ROOT/deploy/.env"
STATE_DIR="$ROOT/deploy/.state"
mkdir -p "$STATE_DIR"

if [ "$LOCAL" = 0 ]; then
  log "Installing workspace dependencies"
  pnpm install --frozen-lockfile
fi

# ---------------------------------------------------------------- 3. Supabase (CLI stack)
log "Starting Supabase"
cd "$ROOT/backend"
EXCLUDE_ARGS=()
[ -n "${SUPABASE_EXCLUDE:-}" ] && EXCLUDE_ARGS=(-x "$SUPABASE_EXCLUDE")
supa start "${EXCLUDE_ARGS[@]}" >/dev/null   # its non-TTY output includes keys
# The CLI creates the edge runtime (wallet-link function) with restart=no; keep it up across reboots.
$DOCKER update --restart unless-stopped supabase_edge_runtime_ronriku >/dev/null 2>&1 || true
$DOCKER start supabase_edge_runtime_ronriku >/dev/null 2>&1 || true

if [ "$RESET" = 1 ] || { [ "$LOCAL" = 0 ] && [ ! -f "$STATE_DIR/db-initialized" ]; }; then
  log "Initializing database (db reset: migrations + seed)"
  supa db reset
  touch "$STATE_DIR/db-initialized"
else
  log "Applying pending migrations (data kept)"
  supa migration up
fi
log "Publishing content (answer keys, shop shelf, bosses)"
pnpm publish:content

STATUS="$(supa status -o env 2>/dev/null)"
sb() { printf '%s\n' "$STATUS" | sed -n "s/^$1=\"\{0,1\}\([^\"]*\)\"\{0,1\}$/\1/p" | head -n1; }
ANON="$(sb ANON_KEY)"; SERVICE="$(sb SERVICE_ROLE_KEY)"
[ -n "$ANON" ] && [ -n "$SERVICE" ] || die "Could not read Supabase keys from 'supabase status'."
cd "$ROOT"

# ---------------------------------------------------------------- 4. deploy/.env (never committed)
log "Writing $ENV_FILE"
[ -f "$ENV_FILE" ] || cp deploy/.env.example "$ENV_FILE"
chmod 600 "$ENV_FILE"
setenv() { # setenv KEY VALUE — replace or append, value never echoed
  local k="$1" v="$2" tmp; tmp="$(mktemp)"
  grep -v "^$k=" "$ENV_FILE" > "$tmp" || true
  printf '%s=%s\n' "$k" "$v" >> "$tmp"
  cat "$tmp" > "$ENV_FILE"; rm -f "$tmp"
}
getenv() { sed -n "s/^$1=//p" "$ENV_FILE" | tail -n1; }

setenv SUPABASE_ANON_KEY "$ANON"
setenv SUPABASE_SERVICE_ROLE_KEY "$SERVICE"
if [ "$LOCAL" = 1 ]; then
  setenv DOMAIN ":80"; setenv SITE_URL "http://localhost:${HTTP_PORT:-8080}"
  setenv BIND_ADDR "127.0.0.1"; setenv HTTP_PORT "${HTTP_PORT:-8080}"; setenv HTTPS_PORT "${HTTPS_PORT:-8443}"
else
  [ -n "${DOMAIN:-}" ] || DOMAIN="$(getenv DOMAIN)"
  case "${DOMAIN:-}" in ""|:*) DOMAIN="odlet.xyz" ;; esac
  setenv DOMAIN "$DOMAIN"; setenv SITE_URL "https://$DOMAIN"
  setenv BIND_ADDR "0.0.0.0"; setenv HTTP_PORT 80; setenv HTTPS_PORT 443
fi
[ -n "${PAYMENT_RECIPIENT:-}" ] && setenv PAYMENT_RECIPIENT "$PAYMENT_RECIPIENT"
if [ -z "$(getenv MINT_AUTHORITY_SECRET_KEY)" ]; then
  # New server mint authority (needs no SOL). Only the public key is printed.
  KP="$(cd web && node --input-type=module -e "import {Keypair} from '@solana/web3.js'; const k=Keypair.generate(); console.log(JSON.stringify(Array.from(k.secretKey))+' '+k.publicKey.toBase58())")"
  setenv MINT_AUTHORITY_SECRET_KEY "${KP% *}"
  echo "mint authority: ${KP##* } (secret stored in deploy/.env only)"
fi

# ---------------------------------------------------------------- 5. Build + run
ls "$ROOT"/web/public/unity/Build/*.loader.js >/dev/null 2>&1 || echo "note: no Unity WebGL build in web/public/unity — /play shows a placeholder until you add one (no rebuild needed)."
mkdir -p "$ROOT/web/public/unity"
log "Building and starting web + caddy"
$DOCKER compose -f deploy/docker-compose.yml --env-file "$ENV_FILE" up -d --build

# ---------------------------------------------------------------- 6. Daily publisher (server only)
if [ "$LOCAL" = 0 ] && command -v crontab >/dev/null; then
  LINE="17 0 * * * cd $ROOT/backend && PATH=/usr/local/bin:/usr/bin:/bin pnpm publish:content >> $STATE_DIR/publish.log 2>&1"
  ( crontab -l 2>/dev/null | grep -v 'pnpm publish:content' ; echo "$LINE" ) | crontab -
  log "Installed daily publish:content cron (00:17 UTC)"
fi

# ---------------------------------------------------------------- 7. Smoke test
URL="$(getenv SITE_URL)"
log "Waiting for $URL"
for _ in $(seq 1 60); do curl -fsS -o /dev/null "$URL/" && break; sleep 2; done
curl -fsS -o /dev/null -w "home %{http_code}\n" "$URL/"
curl -fsS -o /dev/null -w "rest %{http_code}\n" -H "apikey: $ANON" "$URL/rest/v1/shop_shelf?select=day&limit=1"
echo
echo "Odlet is up at $URL"
echo "Unity client config: backend URL $URL, anon key = SUPABASE_ANON_KEY in deploy/.env"
