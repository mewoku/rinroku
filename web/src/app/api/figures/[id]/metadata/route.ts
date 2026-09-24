import { NextResponse, type NextRequest } from "next/server";
import { buildFigureMetadata } from "@/lib/metadata";
import { publicEnv } from "@/lib/env";
import { resolveFigure, traitsFor, TRAIT_LABELS } from "@/lib/server/figures";
import { figureIdSchema } from "@/lib/validation";

export const runtime = "nodejs";

export async function GET(_req: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const parsed = figureIdSchema.safeParse((await params).id);
  if (!parsed.success) return NextResponse.json({ error: "Invalid figure id." }, { status: 400 });
  const fig = await resolveFigure(parsed.data);
  if (!fig) return NextResponse.json({ error: "Figure not found." }, { status: 404 });
  const traits = traitsFor(fig);
  const labelled = traits
    ? Object.fromEntries(Object.entries(traits).map(([k, v]) => [TRAIT_LABELS[k as keyof typeof TRAIT_LABELS] ?? k, v]))
    : null;
  return NextResponse.json(buildFigureMetadata(fig, publicEnv.siteUrl, labelled), {
    headers: { "cache-control": "public, max-age=300, s-maxage=300" },
  });
}
