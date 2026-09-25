# Self-hosting RONRIKU (Docker)

One origin, three pieces:

```
browser ──► Caddy (:80/:443, or 127.0.0.1:8080 locally)
             ├─ /rest/v1 /auth/v1 /functions/v1 /storage/v1 /realtime/v1 ──► Supabase Kong (CLI stack)
             ├─ /unity/*  ──► Unity WebGL files (mounted read-only, served by Caddy)
             └─ /*        ──► web (Next.js standalone, non-root, read-only rootfs)
```

| file | purpose |
|---|---|
| `web.Dockerfile` (+ `.dockerignore`) | multi-stage pnpm-workspace build of `web/` → `output: 'standalone'`, runs as `node` |
| `docker-compose.yml` | `web` + `caddy`; both join the Supabase CLI network `supabase_network_ronriku` |
| `Caddyfile` | routing, automatic HTTPS when `DOMAIN` is a hostname, default security headers, Unity headers |
| `.env.example` | compose variables; `up.sh` writes the real `deploy/.env` (git-ignored, mode 600) |
| `up.sh` | bootstrap / redeploy (fresh Ubuntu VPS or local) |

## VPS (Ubuntu 22.04/24.04, ≥ 4 GB RAM recommended; 2 GB + swap works without Studio)

1. Point a DNS A (and AAAA) record for your domain at the server.
2. Run:

   ```bash
   DOMAIN=play.example.com REPO_URL=https://github.com/<you>/RONRIKU.git bash up.sh
   # low RAM: add SUPABASE_EXCLUDE=studio,imgproxy,inbucket (keep edge-runtime: it serves wallet-link)
   ```

   It installs Docker + Node 24 + pnpm, sets Docker's default publish address to `127.0.0.1`, enables `ufw` (SSH, 80, 443 only), clones the repo, starts Supabase, runs `db reset` **on the first run only** (later runs: `migration up`, data kept), runs `pnpm publish:content`, fills `deploy/.env` (Supabase keys from `supabase status`, a new mint-authority key), builds the image, starts `web` + `caddy`, installs a daily `publish:content` cron, and smoke-tests the site.
3. Copy the Unity WebGL build to `web/public/unity/` on the server (e.g. `rsync -a web/public/unity/ vps:RONRIKU/web/public/unity/`). It is mounted, so no rebuild or restart is needed.

Redeploy after `git pull`: `bash deploy/up.sh --no-install`.

### What is exposed

- Public: only Caddy on 80/443. Supabase is reached through Caddy's `/rest/v1`, `/auth/v1`, `/functions/v1`, `/storage/v1`, `/realtime/v1` (Kong still enforces the anon/service keys, and RLS applies).
- Postgres (54322), Studio (54323), Kong direct (54321) and Mailpit (54324) are published by the Supabase CLI. **Docker publishes ports around `ufw`**, so `up.sh` sets `{"ip": "127.0.0.1"}` in `/etc/docker/daemon.json` before starting anything: they bind to loopback only. Check with `ss -tlnp` (only `:80`/`:443` should be on `0.0.0.0`). Reach Studio via `ssh -L 54323:127.0.0.1:54323 vps`.
- Local Supabase CLI keys (anon, service role, JWT secret) are the well-known dev defaults. Fine behind this setup for a devnet game, but **before real users or money**, move to hosted Supabase or the self-hosted Supabase compose with your own JWT secret (see Risks).

## Local (Docker Desktop)

```bash
cd backend && npx supabase start && cd ..      # once; keep your data
bash deploy/up.sh --local                        # → http://localhost:8080  (add --reset on a fresh clone)
```

## Operations

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs -f web caddy
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build   # after code changes
docker compose -f deploy/docker-compose.yml --env-file deploy/.env down            # stop site (Supabase keeps running)
```

After a host reboot the Supabase containers restart on their own (`unless-stopped`); `up.sh` also sets that on the edge runtime (wallet-link), which the CLI creates with `restart: no`.

## Notes

- `NEXT_PUBLIC_SUPABASE_URL=same-origin` is baked into the image: the browser uses `window.location.origin`, so the same image works on any domain; the server uses `SUPABASE_INTERNAL_URL` (Kong on the Docker network). The page CSP then allows Supabase via `'self'`. `SITE_URL` and the anon key are also build-time values; change them → rebuild (`up.sh` does it).
- Unity files: Caddy serves `/unity/*` with the right `Content-Encoding`/`Content-Type` for `.br`/`.gz` builds, `application/wasm` for `.wasm`, and no encoding for `.unityweb` (decompression-fallback builds, the current one). `Cache-Control: no-cache` because Unity file names carry no hash.
- Wallet link: give the edge function `WALLET_LINK_DOMAIN=<your domain>` (`[edge_runtime.secrets]` in `backend/supabase/config.toml`, then `supabase stop && supabase start`) so wallets show the real site in the link message. Default: `ronriku.local`.
- Unity client behind Caddy: backend URL = `SITE_URL` (e.g. `http://localhost:8080` / `https://play.example.com`), anon key = `SUPABASE_ANON_KEY` in `deploy/.env`. On Android use HTTPS in production (disable `insecureHttpOption`).
