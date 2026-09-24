"use client";

import { useMemo, useState } from "react";
import { PixelTabs } from "../ui/PixelTabs";
import { PixelButton } from "../ui/PixelButton";
import { Badge, DemoBadge } from "../ui/Badge";
import { Modal } from "../ui/Modal";
import { useToast } from "../ui/Toast";
import { PixelIcon } from "../ui/PixelIcon";
import { FigureCard } from "../figure/FigureCard";
import { FigureDetail } from "../figure/FigureDetail";
import { WalletConnect } from "../wallet/WalletConnect";
import { useSolPurchase } from "../wallet/useSolPurchase";
import { useSession } from "../SessionProvider";
import { BackendUnavailableError, buyFigureWithShards, buyListing, fetchListings, fetchShelf, type ListingFilters } from "@/lib/api";
import { formatPrice, formatShards, formatSol } from "@/lib/economy";
import { useAsync } from "@/lib/hooks";
import type { ShelfItem } from "@/lib/shelf";
import { RARITIES, TIERS, type Currency, type FigureRecord, type Listing, type Rarity, type Tier } from "@/lib/types";

type Tab = "shelf" | "listings";

export function MarketView() {
  const [tab, setTab] = useState<Tab>("shelf");
  return (
    <div className="flex flex-col gap-6">
      <PixelTabs
        label="Shop sections"
        tabs={[
          { id: "shelf", label: "Daily shelf" },
          { id: "listings", label: "Market" },
        ]}
        value={tab}
        onChange={setTab}
      />
      {tab === "shelf" ? <DailyShelf /> : <Listings />}
    </div>
  );
}

function errText(e: unknown, fallback: string) {
  if (e instanceof BackendUnavailableError) return "This needs the live backend — sign in when the server is up.";
  return e instanceof Error ? e.message : fallback;
}

// ---------------------------------------------------------------- daily shelf (mirrors the game's SHOP tab)
function shelfRecord(s: ShelfItem): FigureRecord {
  return { id: s.id, seed: s.seed, tier: s.tier, encoding: s.encoding, rarity: s.rarity, name: s.name, ownerId: null, mintAddress: null, createdAt: null };
}

function DailyShelf() {
  const state = useAsync(() => fetchShelf(), []);
  const [open, setOpen] = useState<ShelfItem | null>(null);
  const [pending, setPending] = useState<string | null>(null);
  const toast = useToast();
  const { purchase, busy, connected } = useSolPurchase();
  const { refresh, profile, userId } = useSession();
  const offline = state.status === "ready" && state.value.source === "demo";
  const shelf = state.status === "ready" ? state.value.data : [];

  const buyShards = async (s: ShelfItem) => {
    setPending(s.id);
    try {
      const fig = await buyFigureWithShards(s.id);
      toast(`${fig?.name ?? s.name} joined your collection!`, "ok");
      setOpen(null);
      await refresh();
    } catch (e) {
      toast(errText(e, "Purchase failed"), "error");
    } finally {
      setPending(null);
    }
  };

  const buySol = async (s: ShelfItem) => {
    setPending(s.id);
    try {
      const r = await purchase("figure-legendary", { day: s.day, slot: s.slot });
      toast(r.mintAddress ? `${s.name} minted! Core asset ${r.mintAddress.slice(0, 6)}…` : "Payment verified.", "ok");
      setOpen(null);
      await refresh();
    } catch (e) {
      toast(errText(e, "Purchase failed"), "error");
    } finally {
      setPending(null);
    }
  };

  const action = (s: ShelfItem) => {
    if (s.priceShards != null) {
      if (offline) return <Badge color="var(--muted)">{formatShards(s.priceShards)} ◆ · offline</Badge>;
      if (!userId)
        return (
          <PixelButton size="sm" href="/login" variant="secondary">
            Sign in to buy
          </PixelButton>
        );
      const short = profile != null && profile.shards < s.priceShards;
      return (
        <PixelButton size="sm" onClick={() => buyShards(s)} disabled={pending !== null || short} aria-label={`Buy ${s.name} for ${s.priceShards} shards`}>
          {pending === s.id ? "…" : `${formatShards(s.priceShards)} ◆`}
        </PixelButton>
      );
    }
    return connected ? (
      <PixelButton size="sm" palette="boss" onClick={() => buySol(s)} disabled={busy || pending !== null || offline}>
        {pending === s.id ? "Confirm…" : `${formatSol(s.priceLamports ?? 0)} SOL`}
      </PixelButton>
    ) : (
      <WalletConnect size="sm" variant="primary" />
    );
  };

  return (
    <section aria-label="Daily shelf" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-[720px] text-[14px] leading-5 text-muted">
          Six figures rotate every UTC day — the same shelf as the game&apos;s SHOP tab. One copy per design. <b className="text-text">Legendaries</b> are devnet-SOL only and mint straight to your wallet as a Metaplex Core NFT.
        </p>
        {profile && (
          <span className="flex items-center gap-2 font-pixel text-[14px] text-yellow">
            <PixelIcon name="shard" /> {formatShards(profile.shards)}
          </span>
        )}
        {offline && <DemoBadge reason="Server offline — shelf computed locally, not buyable" />}
      </div>
      {state.status === "loading" && <GridSkeleton n={6} />}
      {state.status === "error" && <p className="px-panel p-6 text-center text-danger">{state.error}</p>}
      {state.status === "ready" && !offline && shelf.length === 0 && (
        <p className="px-panel p-6 text-center text-muted">Today&apos;s shelf isn&apos;t published yet. Check back in a moment.</p>
      )}
      <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
        {shelf.map((s, i) => (
          <li key={s.id}>
            <FigureCard
              figure={shelfRecord(s)}
              size={128}
              phase={i * 0.9}
              onOpen={() => setOpen(s)}
              footer={
                <div className="flex flex-col gap-2">
                  <span className="font-pixel text-[10px] text-muted">
                    {s.tier}×{s.tier} · SLOT {s.slot + 1}
                  </span>
                  {action(s)}
                </div>
              }
            />
          </li>
        ))}
      </ul>
      <Modal open={!!open} onClose={() => setOpen(null)} title="Daily shelf" palette="link">
        {open && (
          <FigureDetail figure={shelfRecord(open)}>
            <div className="flex flex-wrap items-center gap-3">{action(open)}</div>
          </FigureDetail>
        )}
      </Modal>
    </section>
  );
}

// ---------------------------------------------------------------- listings
function Listings() {
  const [tier, setTier] = useState<Tier | null>(null);
  const [rarity, setRarity] = useState<Rarity | null>(null);
  const [currency, setCurrency] = useState<Currency | null>(null);
  const [sort, setSort] = useState<ListingFilters["sort"]>("new");
  const [maxShards, setMaxShards] = useState<number | null>(null);
  const [open, setOpen] = useState<Listing | null>(null);
  const filters = useMemo(() => ({ tier, rarity, currency, sort, maxShards }), [tier, rarity, currency, sort, maxShards]);
  const state = useAsync(() => fetchListings(filters), [filters]);
  const toast = useToast();

  const onBuy = async (l: Listing) => {
    try {
      if (l.priceLamports != null) {
        toast("SOL listings settle via the treasury delegate — coming with M3.", "info");
        return;
      }
      await buyListing(l.id);
      toast(`You bought ${l.figure.name}!`, "ok");
      setOpen(null);
      state.reload();
    } catch (e) {
      toast(e instanceof BackendUnavailableError ? "Buying needs the live backend." : e instanceof Error ? e.message : "Failed", "error");
    }
  };

  const chip = (active: boolean) =>
    `px-border min-h-10 px-3 font-pixel text-[12px] uppercase ${active ? "bg-accent text-bg-0" : "bg-surface-1 text-muted hover:text-text"}`;

  return (
    <section aria-label="Player listings" className="flex flex-col gap-4">
      <div className="px-panel flex flex-col gap-4 p-4 lg:flex-row lg:flex-wrap lg:items-end">
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-2 font-pixel text-[10px] text-muted">TIER</legend>
          <div className="flex gap-2">
            <button className={chip(tier === null)} style={tier === null ? { ["--pb" as string]: "var(--accent)" } : undefined} onClick={() => setTier(null)} aria-pressed={tier === null}>
              All
            </button>
            {TIERS.map((t) => (
              <button key={t} className={chip(tier === t)} style={tier === t ? { ["--pb" as string]: "var(--accent)" } : undefined} onClick={() => setTier(t)} aria-pressed={tier === t}>
                {t}×{t}
              </button>
            ))}
          </div>
        </fieldset>
        <label className="flex flex-col gap-2">
          <span className="font-pixel text-[10px] text-muted">RARITY</span>
          <select className="px-input" value={rarity ?? ""} onChange={(e) => setRarity((e.target.value || null) as Rarity | null)}>
            <option value="">Any</option>
            {RARITIES.map((r) => (
              <option key={r}>{r}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-2">
          <span className="font-pixel text-[10px] text-muted">CURRENCY</span>
          <select className="px-input" value={currency ?? ""} onChange={(e) => setCurrency((e.target.value || null) as Currency | null)}>
            <option value="">Any</option>
            <option value="shards">Shards ◆</option>
            <option value="sol">Devnet SOL</option>
          </select>
        </label>
        <label className="flex flex-col gap-2">
          <span className="font-pixel text-[10px] text-muted">MAX ◆</span>
          <input
            className="px-input w-32"
            inputMode="numeric"
            placeholder="∞"
            value={maxShards ?? ""}
            onChange={(e) => {
              const v = e.target.value.replace(/\D/g, "");
              setMaxShards(v ? Math.min(1_000_000, Number(v)) : null);
            }}
          />
        </label>
        <label className="flex flex-col gap-2">
          <span className="font-pixel text-[10px] text-muted">SORT</span>
          <select className="px-input" value={sort} onChange={(e) => setSort(e.target.value as ListingFilters["sort"])}>
            <option value="new">Newest</option>
            <option value="price_asc">Price ↑</option>
            <option value="price_desc">Price ↓</option>
          </select>
        </label>
        <div className="lg:ml-auto">{state.status === "ready" && state.value.source === "demo" && <DemoBadge reason={state.value.reason} />}</div>
      </div>

      {state.status === "loading" && <GridSkeleton />}
      {state.status === "error" && <p className="text-danger">{state.error}</p>}
      {state.status === "ready" && state.value.data.length === 0 && (
        <p className="px-panel p-6 text-center text-muted">No listings match. Try widening the filters.</p>
      )}
      {state.status === "ready" && state.value.data.length > 0 && (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {state.value.data.map((l, i) => (
            <li key={l.id}>
              <FigureCard
                figure={l.figure}
                phase={i * 0.7}
                size={120}
                onOpen={() => setOpen(l)}
                footer={
                  <div className="flex items-center justify-between gap-2">
                    <span className="tabular font-pixel text-[12px] text-yellow">
                      {l.priceShards != null ? formatPrice(l.priceShards, "shards") : formatPrice(l.priceLamports ?? 0, "sol")}
                    </span>
                    <span className="truncate text-[12px] text-muted">@{l.sellerHandle}</span>
                  </div>
                }
              />
            </li>
          ))}
        </ul>
      )}

      <Modal open={!!open} onClose={() => setOpen(null)} title="Figure" palette="pattern">
        {open && (
          <FigureDetail figure={open.figure}>
            <p className="text-[14px] text-muted">
              Listed by <span className="text-text">@{open.sellerHandle}</span>
            </p>
            <div className="flex flex-wrap items-center gap-3">
              <PixelButton onClick={() => onBuy(open)} disabled={state.status === "ready" && state.value.source === "demo"}>
                Buy · {open.priceShards != null ? formatPrice(open.priceShards, "shards") : formatPrice(open.priceLamports ?? 0, "sol")}
              </PixelButton>
              {state.status === "ready" && state.value.source === "demo" && <DemoBadge reason="Demo listing — not purchasable" />}
            </div>
          </FigureDetail>
        )}
      </Modal>
    </section>
  );
}

export function GridSkeleton({ n = 10 }: { n?: number }) {
  return (
    <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5" aria-busy="true" aria-label="Loading">
      {Array.from({ length: n }, (_, i) => (
        <li key={i} className="px-panel dither-bg h-[200px] animate-pulse" />
      ))}
    </ul>
  );
}
