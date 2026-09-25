# syntax=docker/dockerfile:1.7
# Production image for web/ (Next.js standalone). Build context = repo root:
#   docker compose -f deploy/docker-compose.yml build web
# NEXT_PUBLIC_* values are inlined into the browser bundle at build time, so they are build args.
# No secrets here: server secrets (service-role key, mint authority) are runtime env only.
# The Unity WebGL build (web/public/unity) is NOT baked in: it is mounted read-only at runtime
# (see docker-compose.yml), so a new Unity build needs no image rebuild.

ARG NODE_IMAGE=node:24-bookworm-slim

FROM ${NODE_IMAGE} AS base
ENV PNPM_HOME=/pnpm PATH=/pnpm:$PATH NEXT_TELEMETRY_DISABLED=1 CI=1
RUN corepack enable && corepack prepare pnpm@9.15.4 --activate
WORKDIR /repo

# ---- dependencies (cached until a manifest or the lockfile changes)
FROM base AS deps
COPY pnpm-lock.yaml pnpm-workspace.yaml package.json ./
COPY packages/core/package.json packages/core/
COPY backend/package.json backend/
COPY web/package.json web/
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
    pnpm install --frozen-lockfile --filter "web..."

# ---- build
FROM deps AS build
COPY packages/core packages/core
COPY web web
ARG NEXT_PUBLIC_SUPABASE_URL=same-origin
ARG NEXT_PUBLIC_SUPABASE_ANON_KEY=
ARG NEXT_PUBLIC_SOLANA_RPC_URL=https://api.devnet.solana.com
ARG NEXT_PUBLIC_PAYMENT_RECIPIENT=
ARG NEXT_PUBLIC_SITE_URL=http://localhost:8080
ENV NEXT_PUBLIC_SUPABASE_URL=$NEXT_PUBLIC_SUPABASE_URL \
    NEXT_PUBLIC_SUPABASE_ANON_KEY=$NEXT_PUBLIC_SUPABASE_ANON_KEY \
    NEXT_PUBLIC_SOLANA_RPC_URL=$NEXT_PUBLIC_SOLANA_RPC_URL \
    NEXT_PUBLIC_PAYMENT_RECIPIENT=$NEXT_PUBLIC_PAYMENT_RECIPIENT \
    NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL \
    NEXT_STANDALONE=1 \
    NODE_OPTIONS=--max-old-space-size=2048
RUN pnpm --filter @ronriku/core build && pnpm --filter web build

# ---- runtime
FROM ${NODE_IMAGE} AS runner
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 PORT=3000 HOSTNAME=0.0.0.0
WORKDIR /app
# outputFileTracingRoot is the monorepo root, so the standalone tree mirrors it: /app/web/server.js.
COPY --from=build /repo/web/.next/standalone ./
COPY --from=build /repo/web/.next/static ./web/.next/static
COPY --from=build /repo/web/public ./web/public
# Code is root-owned and read-only for the app user; only the Next cache dir is writable
# (a tmpfs in docker-compose.yml, which also runs the container with a read-only root fs).
RUN mkdir -p web/.next/cache web/public/unity && chown node:node web/.next/cache
USER node
WORKDIR /app/web
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3000/api/purchase/sol').then(r=>process.exit(r.ok?0:1),()=>process.exit(1))"
CMD ["node", "server.js"]
