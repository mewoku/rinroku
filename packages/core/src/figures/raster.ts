/**
 * Pure isometric pixel rasterizer for voxel figures (no Node / DOM dependencies).
 *
 * View: camera at (+x, -y, +z) looking at the figure's front-right corner, 2:1 pixel isometric.
 *   screenX = (x + y) * u,  screenY = (x - y) * u/2 - z * u        (u = pixels per voxel edge, even)
 * Visible faces and flat shading (MagicaVoxel-preview style, PLAN_V2 §2):
 *   top (+z) x1.0, left = front (-y) x0.8, right (+x) x0.62.
 * Painter's order: ascending (x - y + z) draws far voxels first; cubes with equal depth never
 * overlap in this projection.
 */
import type { Figure } from "./figure.js";

export const FACE_SHADE = { top: 1.0, left: 0.8, right: 0.62 } as const;

export interface RasterImage {
  width: number;
  height: number;
  /** RGBA, row-major, transparent background. */
  data: Uint8Array;
}

export interface RasterOptions {
  /** Voxel edge in base pixels (even). Default 4. */
  unit?: number;
  /** Square base canvas size in pixels; the figure is centred. Default 64. */
  canvas?: number;
}

type Face = 0 | 1 | 2; // top, left, right

function cubeMask(u: number): Int8Array {
  // -1 = outside, 0 = top, 1 = left, 2 = right. Pixel-centre test against the hexagon.
  const mask = new Int8Array(4 * u * u).fill(-1);
  for (let py = 0; py < 2 * u; py++)
    for (let px = 0; px < 2 * u; px++) {
      const cx = px + 0.5, cy = py + 0.5, dx = Math.abs(cx - u);
      if (cy < dx / 2 || cy > 2 * u - dx / 2) continue;
      const face: Face = cy < u - dx / 2 ? 0 : cx < u ? 1 : 2;
      mask[py * 2 * u + px] = face;
    }
  return mask;
}

function shadeRgb(rgb: number, k: number): [number, number, number] {
  return [Math.round(((rgb >> 16) & 0xff) * k), Math.round(((rgb >> 8) & 0xff) * k), Math.round((rgb & 0xff) * k)];
}

export function rasterizeFigure(figure: Figure, options: RasterOptions = {}): RasterImage {
  const u = options.unit ?? 4;
  if (u < 2 || u % 2 !== 0) throw new RangeError("unit must be an even number >= 2");
  const s = figure.size, h = figure.height;

  // Bounds of all voxel sprites (whole volume, so every figure of a tier sits at the same spot).
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const [x, y, z] of [[0, s - 1, h - 1], [s - 1, 0, 0], [0, 0, 0], [s - 1, s - 1, h - 1], [0, s - 1, 0], [s - 1, 0, h - 1]]) {
    const sx = (x + y) * u, sy = ((x - y) * u) / 2 - z * u;
    minX = Math.min(minX, sx); maxX = Math.max(maxX, sx + 2 * u);
    minY = Math.min(minY, sy); maxY = Math.max(maxY, sy + 2 * u);
  }
  const contentW = maxX - minX, contentH = maxY - minY;
  const width = Math.max(options.canvas ?? 64, contentW), height = Math.max(options.canvas ?? 64, contentH);
  const offX = Math.floor((width - contentW) / 2) - minX;
  const offY = Math.floor((height - contentH) / 2) - minY;

  const order: Array<[number, number, number, number]> = [];
  for (let z = 0; z < h; z++)
    for (let y = 0; y < s; y++)
      for (let x = 0; x < s; x++) {
        const v = figure.get(x, y, z);
        if (v !== 0) order.push([x - y + z, x, y, z]);
      }
  order.sort((a, b) => a[0] - b[0]);

  const mask = cubeMask(u);
  const data = new Uint8Array(width * height * 4);
  const shades = [FACE_SHADE.top, FACE_SHADE.left, FACE_SHADE.right];
  for (const [, x, y, z] of order) {
    const rgb = figure.palette[figure.get(x, y, z) - 1];
    const colours = shades.map((k) => shadeRgb(rgb, k));
    const ox = (x + y) * u + offX, oy = ((x - y) * u) / 2 - z * u + offY;
    for (let py = 0; py < 2 * u; py++)
      for (let px = 0; px < 2 * u; px++) {
        const face = mask[py * 2 * u + px];
        if (face < 0) continue;
        const i = ((oy + py) * width + ox + px) * 4;
        const c = colours[face];
        data[i] = c[0]; data[i + 1] = c[1]; data[i + 2] = c[2]; data[i + 3] = 255;
      }
  }
  return { width, height, data };
}

/** Nearest-neighbour integer upscale. */
export function upscale(image: RasterImage, factor: number): RasterImage {
  const width = image.width * factor, height = image.height * factor;
  const data = new Uint8Array(width * height * 4);
  for (let y = 0; y < height; y++)
    for (let x = 0; x < width; x++) {
      const src = (Math.floor(y / factor) * image.width + Math.floor(x / factor)) * 4;
      data.set(image.data.subarray(src, src + 4), (y * width + x) * 4);
    }
  return { width, height, data };
}
