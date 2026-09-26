import type { FigureRecord } from "./types";

export const COLLECTION_SYMBOL = "ODLET";

export interface MetaplexMetadata {
  name: string;
  symbol: string;
  description: string;
  image: string;
  external_url: string;
  attributes: { trait_type: string; value: string | number }[];
  properties: { files: { uri: string; type: string }[]; category: "image" };
}

/** Metaplex-compatible JSON (token-metadata standard; Core `uri` points here). */
export function buildFigureMetadata(
  fig: FigureRecord,
  origin: string,
  traits: Record<string, string | number | boolean> | null,
): MetaplexMetadata {
  const image = `${origin}/api/figures/${fig.id}/image`;
  const attributes: MetaplexMetadata["attributes"] = [
    { trait_type: "Tier", value: `${fig.tier}x${fig.tier}` },
    { trait_type: "Rarity", value: fig.rarity },
    { trait_type: "Seed", value: fig.seed },
  ];
  if (traits) {
    for (const [k, v] of Object.entries(traits)) attributes.push({ trait_type: k, value: typeof v === "boolean" ? (v ? "Yes" : "No") : v });
  }
  return {
    name: `${fig.name || "ODLET"} #${fig.id.slice(0, 8)}`.slice(0, 32),
    symbol: COLLECTION_SYMBOL,
    description: `A ${fig.rarity} ${fig.tier}×${fig.tier} Odlet voxel figure. Encoding: ${fig.encoding}`,
    image,
    external_url: `${origin}/market`,
    attributes,
    properties: { files: [{ uri: image, type: "image/png" }], category: "image" },
  };
}
