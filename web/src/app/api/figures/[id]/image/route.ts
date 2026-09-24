import { NextResponse, type NextRequest } from "next/server";
import { renderPng, resolveFigure } from "@/lib/server/figures";
import { figureIdSchema } from "@/lib/validation";

export const runtime = "nodejs";

/** 256×256 PNG rendered by @ronriku/core (same isometric raster as the NFT spec, PLAN §5). */
export async function GET(_req: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const parsed = figureIdSchema.safeParse((await params).id);
  if (!parsed.success) return NextResponse.json({ error: "Invalid figure id." }, { status: 400 });
  const fig = await resolveFigure(parsed.data);
  if (!fig) return NextResponse.json({ error: "Figure not found." }, { status: 404 });
  let png: Buffer;
  try {
    png = renderPng(fig.encoding, 256);
  } catch {
    return NextResponse.json({ error: "Could not render figure." }, { status: 500 });
  }
  return new NextResponse(new Uint8Array(png), {
    headers: {
      "content-type": "image/png",
      "cache-control": "public, max-age=86400, immutable",
    },
  });
}
