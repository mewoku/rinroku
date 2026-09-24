import { describe, expect, it } from "vitest";
import { MINT_FEE_LAMPORTS, formatPrice, formatShards, formatSol, solPriceLamports, solToLamports } from "./economy";

describe("price formatting", () => {
  it("formats SOL from lamports without float drift", () => {
    expect(formatSol(100_000_000)).toBe("0.10");
    expect(formatSol(10_000_000)).toBe("0.01");
    expect(formatSol(1_500_000_000)).toBe("1.50");
    expect(formatSol(1_000_000_000)).toBe("1.00");
    expect(formatSol(5_000)).toBe("0.000005");
    expect(formatSol(1)).toBe("0.000000001");
    expect(formatSol(0)).toBe("0.00");
    expect(formatSol(12_345_000_000_000n)).toBe("12,345.00");
    expect(formatSol(-250_000_000)).toBe("-0.25");
  });

  it("formats shards as grouped integers", () => {
    expect(formatShards(2000)).toBe("2,000");
    expect(formatShards(300)).toBe("300");
    expect(formatShards(1999.9)).toBe("1,999");
    expect(formatShards(Number.NaN)).toBe("—");
  });

  it("formats prices with currency", () => {
    expect(formatPrice(800, "shards")).toBe("800 ◆");
    expect(formatPrice(100_000_000, "sol")).toBe("0.10 SOL");
  });

  it("parses SOL strings exactly", () => {
    expect(solToLamports("0.1")).toBe(100_000_000);
    expect(solToLamports("1")).toBe(1_000_000_000);
    expect(solToLamports("0.000000001")).toBe(1);
    expect(() => solToLamports("0.0000000001")).toThrow();
    expect(() => solToLamports("-1")).toThrow();
    expect(() => solToLamports("1e3")).toThrow();
  });

  it("uses PLAN §6 SOL prices", () => {
    expect(solPriceLamports("figure-legendary")).toBe(100_000_000);
    expect(solPriceLamports("boss-entry")).toBe(10_000_000);
    expect(solPriceLamports("mint-fee")).toBe(MINT_FEE_LAMPORTS);
    expect(solPriceLamports("figure-common")).toBeNull();
  });
});
