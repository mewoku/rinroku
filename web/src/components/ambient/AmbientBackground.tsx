"use client";

import { useEffect, useRef } from "react";
import { hexToRgb, PALETTES, type PaletteName } from "@/lib/palettes";
import { usePrefersReducedMotion } from "@/lib/hooks";

/** Ordered-dither Bayer 4×4 thresholds (0..1). */
const BAYER4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5].map((v) => (v + 0.5) / 16);
const PIXEL = 6; // CSS px per ambient pixel
const LEVELS = 5; // quantisation steps per blob
const FPS = 15;

interface Blob {
  color: [number, number, number];
  strength: number;
  radius: number; // fraction of the larger viewport side
  ax: number;
  ay: number;
  px: number;
  py: number;
  sx: number;
  sy: number;
}

function blobsFor(name: PaletteName): Blob[] {
  const p = PALETTES[name];
  const amb = hexToRgb(p.ambient);
  const a2 = hexToRgb(p.accent2);
  // ambient hue lifted a little toward accent2 so it reads on the near-black base
  const lifted: [number, number, number] = [0, 1, 2].map((i) => amb[i]! * 1.35 + a2[i]! * 0.15) as [number, number, number];
  return [
    { color: lifted, strength: 1.0, radius: 0.7, ax: 0.22, ay: 0.12, px: 0.2, py: 0.15, sx: 0.011, sy: 0.017 },
    { color: a2, strength: 0.55, radius: 0.5, ax: 0.2, ay: 0.16, px: 0.85, py: 0.6, sx: 0.013, sy: 0.009 },
    { color: hexToRgb(p.accent), strength: 0.28, radius: 0.36, ax: 0.26, ay: 0.2, px: 0.5, py: 0.95, sx: 0.007, sy: 0.012 },
  ];
}

const BASE_TOP: [number, number, number] = [7, 8, 11];
const BASE_BOTTOM: [number, number, number] = [14, 16, 22];

/**
 * Pixel ambient background (PLAN §2): 2–3 slowly drifting radial blobs, ordered-dithered at low
 * resolution and point-upscaled. Fixed behind the page; static when reduced motion is requested.
 */
export function AmbientBackground({ palette = "lab" }: { palette?: PaletteName }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const reduced = usePrefersReducedMotion();

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) return;
    const blobs = blobsFor(palette);
    let w = 0;
    let h = 0;
    let image: ImageData | null = null;
    let raf = 0;
    let last = 0;
    const start = performance.now();

    const resize = () => {
      w = Math.max(16, Math.ceil(window.innerWidth / PIXEL));
      h = Math.max(16, Math.ceil(window.innerHeight / PIXEL));
      canvas.width = w;
      canvas.height = h;
      image = ctx.createImageData(w, h);
    };

    const draw = (t: number) => {
      if (!image) return;
      const data = image.data;
      const side = Math.max(w, h);
      const centres = blobs.map((b) => ({
        x: (b.px + Math.sin(t * b.sx * 6.283) * b.ax) * w,
        y: (b.py + Math.cos(t * b.sy * 6.283) * b.ay) * h,
        inv: 1 / (b.radius * side) ** 2,
      }));
      let i = 0;
      for (let y = 0; y < h; y++) {
        const v = y / (h - 1 || 1);
        const br = BASE_TOP[0] + (BASE_BOTTOM[0] - BASE_TOP[0]) * v;
        const bg = BASE_TOP[1] + (BASE_BOTTOM[1] - BASE_TOP[1]) * v;
        const bb = BASE_TOP[2] + (BASE_BOTTOM[2] - BASE_TOP[2]) * v;
        const row = (y & 3) << 2;
        for (let x = 0; x < w; x++) {
          const threshold = BAYER4[row + (x & 3)]!;
          let r = br;
          let g = bg;
          let b = bb;
          for (let k = 0; k < blobs.length; k++) {
            const c = centres[k]!;
            const dx = x - c.x;
            const dy = y - c.y;
            const falloff = 1 - (dx * dx + dy * dy) * c.inv;
            if (falloff <= 0) continue;
            const blob = blobs[k]!;
            // smooth → quantised with ordered dither
            const q = Math.floor(falloff * falloff * LEVELS + threshold) / LEVELS;
            if (q <= 0) continue;
            const a = q * blob.strength * 0.85;
            r += (blob.color[0] - r) * a;
            g += (blob.color[1] - g) * a;
            b += (blob.color[2] - b) * a;
          }
          data[i++] = r;
          data[i++] = g;
          data[i++] = b;
          data[i++] = 255;
        }
      }
      ctx.putImageData(image, 0, 0);
    };

    const loop = (now: number) => {
      raf = requestAnimationFrame(loop);
      if (document.hidden || now - last < 1000 / FPS) return;
      last = now;
      draw((now - start) / 1000);
    };

    resize();
    draw(7);
    const onResize = () => {
      resize();
      draw((performance.now() - start) / 1000 + 7);
    };
    window.addEventListener("resize", onResize);
    if (!reduced) raf = requestAnimationFrame(loop);
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("resize", onResize);
    };
  }, [palette, reduced]);

  return (
    <canvas
      ref={canvasRef}
      aria-hidden="true"
      className="pixelated pointer-events-none fixed inset-0 -z-10 h-full w-full"
      style={{ background: "var(--bg-0)" }}
    />
  );
}
