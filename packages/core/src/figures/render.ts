/**
 * Node-only PNG output for NFT images (`@ronriku/core/render`). Kept out of the main entry so browser
 * bundles do not pull in pngjs.
 */
import { PNG } from "pngjs";
import type { Figure } from "./figure.js";
import { rasterizeFigure, upscale, type RasterOptions } from "./raster.js";

export interface RenderPngOptions extends RasterOptions {
  /** Output edge in pixels; must be a multiple of the base canvas. Default 256 (64 x 4). */
  size?: number;
}

export function renderFigurePng(figure: Figure, options: RenderPngOptions = {}): Buffer {
  const canvas = options.canvas ?? 64;
  const size = options.size ?? 256;
  if (size % canvas !== 0) throw new RangeError("size must be a multiple of canvas");
  const base = rasterizeFigure(figure, { unit: options.unit, canvas });
  if (base.width !== canvas || base.height !== canvas) throw new RangeError("figure does not fit the base canvas; raise canvas or lower unit");
  const image = upscale(base, size / canvas);
  const png = new PNG({ width: image.width, height: image.height, colorType: 6 });
  png.data = Buffer.from(image.data.buffer, image.data.byteOffset, image.data.byteLength);
  return PNG.sync.write(png);
}

export { rasterizeFigure, upscale };
