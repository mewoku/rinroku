"use client";

/**
 * Typed data layer over the Supabase tables / RPCs from PLAN_V2 §7.
 * Reads fall back to clearly-labelled demo data (`source: "demo"`) when the backend is not
 * configured or not reachable. Writes never fake success: they throw `BackendUnavailableError`.
 */
import type { SupabaseClient } from "@supabase/supabase-js";
import { getBrowserSupabase } from "./supabase/client";
import { demoBossEvents, demoCollection, demoFigures, demoFriends, demoLeaderboard, demoListings, demoProfile } from "./demo/data";
import type { Currency, FigureRecord, FriendRow, LeaderboardRow, LeaderboardScope, Listing, Profile, Rarity, Sourced, Tier } from "./types";
import { dayNumber, shelfForDay, type ShelfItem } from "./shelf";

/** Stable snake_case RPC errors (backend/README.md) → player-facing text. */
const RPC_ERRORS: Record<string, string> = {
  insufficient_shards: "Not enough shards.",
  already_owned: "You already own this figure.",
  sol_only_item: "Legendaries are devnet-SOL only.",
  unknown_item: "That item is no longer on the shelf.",
  invalid_handle: "Handles are 3–20 letters, numbers or _.",
  handle_taken: "That handle is taken.",
  figure_not_owned: "You don't own that figure.",
  not_authenticated: "Sign in first.",
  mint_required_for_sol_listing: "Mint the figure before listing it for SOL.",
  minted_figures_list_for_sol: "Minted figures can only be listed for SOL.",
  no_pending_request: "That request is gone.",
};

export class BackendUnavailableError extends Error {
  constructor(message = "Backend not reachable — this action needs the live server.") {
    super(message);
  }
}

/** RPC names/argument keys, matching backend/supabase/migrations/20260924000002_rpc.sql. */
export const RPC = {
  leaderboard: (scope: LeaderboardScope, limit: number) => ["leaderboard", { p_scope: scope, p_limit: limit }] as const,
  /** p_item = today's shop_shelf.item_id (`fig-{seed}-{size}`). */
  buyFigureShards: (item: string) => ["buy_figure_shards", { p_item: item }] as const,
  listFigure: (figureId: string, priceShards: number | null, priceLamports: number | null) =>
    ["list_figure", { p_figure_id: figureId, p_price_shards: priceShards, p_price_lamports: priceLamports }] as const,
  buyListing: (listingId: string) => ["buy_listing", { p_listing_id: listingId }] as const,
  sendFriendRequest: (handle: string) => ["send_friend_request", { p_handle: handle }] as const,
  respondFriendRequest: (requesterId: string, accept: boolean) =>
    ["respond_friend_request", { p_requester_id: requesterId, p_action: accept ? "accept" : "decline" }] as const,
  setAvatar: (figureId: string) => ["update_profile", { p_avatar_figure_id: figureId }] as const,
  setHandle: (handle: string) => ["update_profile", { p_handle: handle }] as const,
};

type Row = Record<string, unknown>;

/** Columns granted to anon/authenticated (shards + wallet_address are owner-only via ensure_profile()). */
const PUBLIC_PROFILE_COLS = "id,handle,display_name,avatar_figure_id,rating,streak,best_streak,created_at";

function sb(): SupabaseClient {
  const client = getBrowserSupabase();
  if (!client) throw new BackendUnavailableError("Supabase is not configured (NEXT_PUBLIC_SUPABASE_URL / ANON_KEY).");
  return client;
}

const REACHABILITY_TTL_MS = 15_000;
let reachable: { value: boolean; at: number } | null = null;
let probe: Promise<boolean> | null = null;

/** Whether the Supabase REST API answers; re-probed every 15 s (not cached for the whole page). */
export async function backendReachable(): Promise<boolean> {
  if (reachable && Date.now() - reachable.at < REACHABILITY_TTL_MS) return reachable.value;
  const client = getBrowserSupabase();
  if (!client) return false;
  probe ??= (async () => {
    let ok = false;
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 2500);
      const { error } = await client.from("profiles").select("id", { head: true }).limit(1).abortSignal(ctrl.signal);
      clearTimeout(t);
      ok = !error;
    } catch {
      ok = false;
    }
    reachable = { value: ok, at: Date.now() };
    probe = null;
    return ok;
  })();
  return probe;
}

function markUnreachable() {
  reachable = { value: false, at: Date.now() };
}

/**
 * Demo data only when the backend is not configured or not reachable. When it *is* reachable and a
 * call fails, the error surfaces (never silently replaced by demo numbers).
 */
async function withFallback<T>(live: (c: SupabaseClient) => Promise<T>, demo: () => T): Promise<Sourced<T>> {
  if (!(await backendReachable())) {
    return { data: demo(), source: "demo", reason: getBrowserSupabase() ? "Backend not reachable" : "Backend not configured" };
  }
  try {
    return { data: await live(sb()), source: "live" };
  } catch (e) {
    if (e instanceof TypeError) markUnreachable(); // network failure: next call re-probes
    throw e;
  }
}

async function requireLive(): Promise<SupabaseClient> {
  if (!(await backendReachable())) throw new BackendUnavailableError();
  return sb();
}

function check<T>(res: { data: T; error: { message: string } | null }): T {
  if (res.error) throw new Error(RPC_ERRORS[res.error.message] ?? res.error.message);
  return res.data;
}

// ---------- mappers ----------
const str = (v: unknown): string | null => (typeof v === "string" ? v : v == null ? null : String(v));
const num = (v: unknown, d = 0): number => (typeof v === "number" ? v : typeof v === "string" && v !== "" ? Number(v) : d);

export function mapFigure(r: Row): FigureRecord {
  return {
    id: String(r.id),
    seed: String(r.seed ?? ""),
    tier: num(r.tier, 3) as Tier,
    encoding: String(r.encoding ?? ""),
    rarity: (str(r.rarity) ?? "Common") as Rarity,
    name: str(r.name) ?? "",
    ownerId: str(r.owner_id),
    mintAddress: str(r.mint_address),
    createdAt: str(r.created_at),
  };
}

export function mapProfile(r: Row, avatarEncoding: string | null = null): Profile {
  return {
    id: String(r.id),
    handle: str(r.handle) ?? "",
    displayName: str(r.display_name),
    avatarFigureId: str(r.avatar_figure_id),
    avatarEncoding,
    rating: num(r.rating, 1200),
    shards: num(r.shards),
    streak: num(r.streak),
    bestStreak: num(r.best_streak),
    walletAddress: str(r.wallet_address),
    createdAt: str(r.created_at),
  };
}

async function encodingsFor(c: SupabaseClient, figureIds: (string | null)[]): Promise<Map<string, string>> {
  const ids = [...new Set(figureIds.filter((x): x is string => !!x))];
  if (!ids.length) return new Map();
  const rows = check(await c.from("figures").select("id,encoding").in("id", ids)) as Row[];
  return new Map(rows.map((r) => [String(r.id), String(r.encoding)]));
}

// ---------- auth / me ----------
export async function currentUserId(): Promise<string | null> {
  const client = getBrowserSupabase();
  if (!client) return null;
  const { data } = await client.auth.getSession();
  return data.session?.user.id ?? null;
}

export async function signInAsGuest(): Promise<string> {
  const c = await requireLive();
  const existing = await currentUserId();
  if (existing) return existing;
  const { data, error } = await c.auth.signInAnonymously();
  if (error || !data.user) throw new Error(error?.message ?? "Anonymous sign-in failed.");
  return data.user.id;
}

export async function signOut(): Promise<void> {
  await getBrowserSupabase()?.auth.signOut();
}

export async function fetchMyProfile(): Promise<Profile | null> {
  if (!(await backendReachable())) return null;
  const uid = await currentUserId();
  if (!uid) return null;
  const c = sb();
  // ensure_profile() creates the row on first sign-in and returns the owner's full row (incl. shards, wallet).
  const ensured = check(await c.rpc("ensure_profile")) as Row | Row[] | null; // failures surface (M7)
  const row = (Array.isArray(ensured) ? ensured[0] : ensured) ?? null;
  if (!row) throw new Error("ensure_profile returned no profile.");
  const enc = await encodingsFor(c, [str(row.avatar_figure_id)]);
  return mapProfile(row, enc.get(String(row.avatar_figure_id)) ?? null);
}

export async function setHandle(handle: string): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.setHandle(handle);
  check(await c.rpc(fn, args));
}

// ---------- market ----------
export interface ListingFilters {
  tier?: Tier | null;
  rarity?: Rarity | null;
  currency?: Currency | null;
  maxShards?: number | null;
  sort?: "new" | "price_asc" | "price_desc";
}

export function applyListingFilters(rows: Listing[], f: ListingFilters): Listing[] {
  let out = rows.filter(
    (l) =>
      (!f.tier || l.figure.tier === f.tier) &&
      (!f.rarity || l.figure.rarity === f.rarity) &&
      (!f.currency || (f.currency === "sol" ? l.priceLamports != null : l.priceShards != null)) &&
      (!f.maxShards || l.priceShards == null || l.priceShards <= f.maxShards),
  );
  const price = (l: Listing) => l.priceShards ?? (l.priceLamports ?? 0) / 1e5; // rough cross-currency ordering
  if (f.sort === "price_asc") out = [...out].sort((a, b) => price(a) - price(b));
  if (f.sort === "price_desc") out = [...out].sort((a, b) => price(b) - price(a));
  return out;
}

export async function fetchListings(filters: ListingFilters): Promise<Sourced<Listing[]>> {
  return withFallback(
    async (c) => {
      let q = c.from("listings").select("*, figure:figures(*)").eq("status", "active").order("created_at", { ascending: false }).limit(60);
      if (filters.currency === "sol") q = q.not("price_lamports", "is", null);
      if (filters.currency === "shards") q = q.not("price_shards", "is", null);
      const rows = check(await q) as Row[];
      const sellers = [...new Set(rows.map((r) => String(r.seller_id)))];
      const handles = sellers.length
        ? new Map(((check(await c.from("profiles").select("id,handle").in("id", sellers)) as Row[]) ?? []).map((p) => [String(p.id), String(p.handle)]))
        : new Map<string, string>();
      const listings: Listing[] = rows
        .filter((r) => r.figure)
        .map((r) => ({
          id: String(r.id),
          figure: mapFigure(r.figure as Row),
          sellerHandle: handles.get(String(r.seller_id)) ?? "unknown",
          priceShards: r.price_shards == null ? null : num(r.price_shards),
          priceLamports: r.price_lamports == null ? null : num(r.price_lamports),
          status: String(r.status),
          createdAt: str(r.created_at),
        }));
      return applyListingFilters(listings, filters);
    },
    () => applyListingFilters(demoListings, filters),
  );
}

export async function buyListing(listingId: string): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.buyListing(listingId);
  check(await c.rpc(fn, args));
}

/**
 * Today's shelf from `shop_shelf` (written by the backend publisher with the same generator).
 * Offline, the shelf is computed locally with @ronriku/core — identical figures, but not buyable.
 */
export async function fetchShelf(day: number = dayNumber()): Promise<Sourced<ShelfItem[]>> {
  return withFallback(
    async (c) => {
      const rows = check(await c.from("shop_shelf").select("day,slot,item_id,seed,tier,rarity,name,encoding,price_shards,price_lamports").eq("day", day).order("slot")) as Row[];
      if (!rows.length) return []; // not published yet — the UI says so (no locally computed stand-in)
      return rows.map((r) => ({
        id: String(r.item_id),
        slot: num(r.slot),
        day: num(r.day),
        seed: BigInt.asUintN(64, BigInt(String(r.seed))).toString(),
        tier: num(r.tier) as Tier,
        name: String(r.name),
        rarity: String(r.rarity) as Rarity,
        encoding: String(r.encoding),
        priceShards: r.price_shards == null ? null : num(r.price_shards),
        priceLamports: r.price_lamports == null ? null : num(r.price_lamports),
      }));
    },
    () => shelfForDay(day),
  );
}

export interface BossEvent {
  id: string;
  name: string;
  palette: string;
  entryShards: number;
  entryLamports: number;
  rewardShards: number;
  timeLimitMs: number;
  stages: number;
  startsAt: string;
  endsAt: string;
}

export async function fetchBossEvents(): Promise<Sourced<BossEvent[]>> {
  return withFallback(
    async (c) => {
      const rows = check(await c.from("boss_events").select("*").gte("ends_at", new Date().toISOString()).order("starts_at").limit(6)) as Row[];
      return rows.map((r) => ({
        id: String(r.id),
        name: String(r.name),
        palette: str(r.palette) ?? "boss",
        entryShards: num(r.entry_shards, 150),
        entryLamports: num(r.entry_lamports, 10_000_000),
        rewardShards: num(r.reward_shards, 500),
        timeLimitMs: num(r.time_limit_ms, 300_000),
        stages: Array.isArray(r.stages) ? r.stages.length : 3,
        startsAt: String(r.starts_at),
        endsAt: String(r.ends_at),
      }));
    },
    () => demoBossEvents(),
  );
}

export async function buyFigureWithShards(item: string): Promise<FigureRecord | null> {
  const c = await requireLive();
  const [fn, args] = RPC.buyFigureShards(item);
  const data = check(await c.rpc(fn, args)) as Row | Row[] | null;
  const row = Array.isArray(data) ? data[0] : data;
  return row && typeof row === "object" && "encoding" in row ? mapFigure(row) : null;
}

// ---------- inventory ----------
export async function fetchInventory(): Promise<Sourced<FigureRecord[]>> {
  return withFallback(
    async (c) => {
      const uid = await currentUserId();
      if (!uid) return [];
      const rows = check(await c.from("figures").select("*").eq("owner_id", uid).order("created_at", { ascending: false })) as Row[];
      return rows.map(mapFigure);
    },
    () => demoFigures.slice(0, 8),
  );
}

export async function equipAvatar(figureId: string): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.setAvatar(figureId);
  check(await c.rpc(fn, args));
}

export async function listFigure(figureId: string, price: { shards?: number; lamports?: number }): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.listFigure(figureId, price.shards ?? null, price.lamports ?? null);
  check(await c.rpc(fn, args));
}

// ---------- leaderboard ----------
export async function fetchLeaderboard(scope: LeaderboardScope, limit = 50): Promise<Sourced<LeaderboardRow[]>> {
  return withFallback(
    async (c) => {
      const [fn, args] = RPC.leaderboard(scope, limit);
      const rows = (check(await c.rpc(fn, args)) as Row[]) ?? [];
      const enc = await encodingsFor(c, rows.map((r) => str(r.avatar_figure_id)));
      return rows.map((r, i) => ({
        // Server rank; ties share a rank (the RPC is authoritative).
        rank: num(r.rank, i + 1),
        userId: String(r.user_id ?? `${i}`),
        handle: str(r.handle) ?? "—",
        score: num(r.score),
        elapsedMs: r.elapsed_ms == null ? null : num(r.elapsed_ms),
        avatarEncoding: str(r.avatar_encoding) ?? enc.get(String(r.avatar_figure_id)) ?? null,
      }));
    },
    () => demoLeaderboard(scope),
  );
}

// ---------- profiles ----------
export async function fetchPublicProfile(handle: string): Promise<Sourced<{ profile: Profile; collection: FigureRecord[] } | null>> {
  return withFallback(
    async (c) => {
      const row = check(await c.from("profiles").select(PUBLIC_PROFILE_COLS).ilike("handle", handle).maybeSingle()) as Row | null;
      if (!row) return null;
      const figs = (check(await c.from("figures").select("*").eq("owner_id", String(row.id)).limit(24)) as Row[]).map(mapFigure);
      const avatarId = str(row.avatar_figure_id);
      const avatar = figs.find((f) => f.id === avatarId)?.encoding ?? (await encodingsFor(c, [avatarId])).get(avatarId ?? "") ?? null;
      return { profile: mapProfile(row, avatar), collection: figs };
    },
    () => ({ profile: demoProfile(handle), collection: demoCollection(handle) }),
  );
}

// ---------- friends ----------
export async function fetchFriends(): Promise<Sourced<FriendRow[]>> {
  return withFallback(
    async (c) => {
      const uid = await currentUserId();
      if (!uid) return [];
      // list_friends() → (user_id, handle, display_name, avatar_figure_id, rating, status, direction)
      const rows = (check(await c.rpc("list_friends")) as Row[]) ?? [];
      const enc = await encodingsFor(c, rows.map((p) => str(p.avatar_figure_id)));
      return rows
        .filter((r) => r.status !== "blocked")
        .map(
          (r): FriendRow => ({
            userId: String(r.user_id),
            handle: str(r.handle) ?? "unknown",
            rating: num(r.rating, 1200),
            status: r.status === "accepted" ? "accepted" : r.direction === "outgoing" ? "pending_out" : "pending_in",
            avatarEncoding: enc.get(String(r.avatar_figure_id)) ?? null,
          }),
        );
    },
    () => demoFriends,
  );
}

export async function searchHandles(query: string): Promise<Sourced<Profile[]>> {
  const q = query.replace(/[^a-zA-Z0-9_]/g, "").slice(0, 20);
  return withFallback(
    async (c) => {
      if (q.length < 2) return [];
      const rows = check(await c.from("profiles").select(PUBLIC_PROFILE_COLS).ilike("handle", `${q}%`).limit(10)) as Row[];
      const enc = await encodingsFor(c, rows.map((r) => str(r.avatar_figure_id)));
      return rows.map((r) => mapProfile(r, enc.get(String(r.avatar_figure_id)) ?? null));
    },
    () => (q.length < 2 ? [] : [demoProfile(`demo_${q.toLowerCase()}`)]),
  );
}

export async function sendFriendRequest(handle: string): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.sendFriendRequest(handle);
  check(await c.rpc(fn, args));
}

export async function respondFriendRequest(requesterId: string, accept: boolean): Promise<void> {
  const c = await requireLive();
  const [fn, args] = RPC.respondFriendRequest(requesterId, accept);
  check(await c.rpc(fn, args));
}

// ---------- wallet link (edge function `wallet-link`) ----------
/** Step 1: `{action:"nonce"}` → the exact `message` to sign (never rebuilt client-side). */
export async function requestWalletNonce(): Promise<{ nonce: string; message: string; expiresAt: string | null }> {
  const c = await requireLive();
  const { data, error } = await c.functions.invoke("wallet-link", { body: { action: "nonce" } });
  if (error) throw new Error(await functionError(error));
  const d = data as { nonce?: string; message?: string; expires_at?: string };
  if (!d?.nonce || !d.message) throw new Error("wallet-link returned no nonce.");
  return { nonce: d.nonce, message: d.message, expiresAt: d.expires_at ?? null };
}

/** Step 2: `{action:"verify", address, signature}` (base58 ed25519 signature over the message bytes). */
export async function verifyWalletLink(address: string, signatureBase58: string): Promise<string> {
  const c = await requireLive();
  const { data, error } = await c.functions.invoke("wallet-link", { body: { action: "verify", address, signature: signatureBase58 } });
  if (error) throw new Error(await functionError(error));
  return String((data as { wallet_address?: string })?.wallet_address ?? address);
}

const WALLET_ERRORS: Record<string, string> = {
  wallet_in_use: "That wallet is already linked to another profile.",
  bad_signature: "Signature did not verify.",
  no_nonce: "Link request expired — try again.",
  nonce_expired: "Link request expired — try again.",
};

async function functionError(error: { message: string; context?: unknown }): Promise<string> {
  try {
    const ctx = error.context as Response | undefined;
    const body = ctx && typeof ctx.json === "function" ? ((await ctx.json()) as { error?: string }) : null;
    if (body?.error) return WALLET_ERRORS[body.error] ?? body.error;
  } catch {
    /* fall through */
  }
  return error.message;
}

/** Access token for calling our own Next API routes on behalf of the user. */
export async function accessToken(): Promise<string | null> {
  const client = getBrowserSupabase();
  if (!client) return null;
  const { data } = await client.auth.getSession();
  return data.session?.access_token ?? null;
}
