"use client";

import { useEffect, useRef, useState } from "react";
import { usePrefersReducedMotion } from "@/lib/hooks";
import { getVoxelStage, type ViewerHandle } from "./voxelStage";

export interface VoxelViewerProps {
  encoding: string;
  /** CSS size in px (square). */
  size?: number;
  /** Low-res render size in px; upscaled with nearest-neighbour. Default size / 4. */
  resolution?: number;
  autoRotate?: boolean;
  interactive?: boolean;
  /** Accent used for the faint rim tint. */
  rim?: string;
  className?: string;
  label?: string;
  /** Initial yaw offset in radians (to desync turntables). */
  phase?: number;
}

/** Pixelated voxel figure: three.js ortho iso render → tiny canvas → pixelated upscale. */
export function VoxelViewer({
  encoding,
  size = 160,
  resolution,
  autoRotate = true,
  interactive = true,
  rim,
  className = "",
  label = "Voxel figure",
  phase = 0,
}: VoxelViewerProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const handleRef = useRef<ViewerHandle | null>(null);
  const reduced = usePrefersReducedMotion();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    let handle: ViewerHandle | null = null;
    try {
      handle = getVoxelStage().add(canvas, encoding, {
        resolution: resolution ?? Math.max(32, Math.round(size / 4)),
        autoRotate: autoRotate && !reduced,
        yaw: Math.PI / 4 + phase,
        rim,
      });
    } catch {
      handle = null;
    }
    if (!handle) {
      setFailed(true);
      return;
    }
    setFailed(false);
    handleRef.current = handle;
    const io = new IntersectionObserver(
      (entries) => {
        for (const e of entries) if (handle) {
          handle.visible = e.isIntersecting;
          handle.dirty = true;
        }
      },
      { rootMargin: "64px" },
    );
    io.observe(canvas);
    return () => {
      io.disconnect();
      handle?.dispose();
      handleRef.current = null;
    };
  }, [encoding, size, resolution, autoRotate, reduced, rim, phase]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !interactive) return;
    let dragging = false;
    let lastX = 0;
    const down = (e: PointerEvent) => {
      dragging = true;
      lastX = e.clientX;
      canvas.setPointerCapture(e.pointerId);
    };
    const move = (e: PointerEvent) => {
      const h = handleRef.current;
      if (!dragging || !h) return;
      h.yaw -= ((e.clientX - lastX) / size) * Math.PI * 1.5;
      lastX = e.clientX;
      h.dirty = true;
      h.pausedUntil = performance.now() + 1500;
    };
    const up = (e: PointerEvent) => {
      dragging = false;
      if (canvas.hasPointerCapture(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
    };
    const key = (e: KeyboardEvent) => {
      const h = handleRef.current;
      if (!h) return;
      if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
        e.preventDefault();
        h.yaw += (e.key === "ArrowLeft" ? 1 : -1) * (Math.PI / 8);
        h.dirty = true;
        h.pausedUntil = performance.now() + 3000;
      }
    };
    canvas.addEventListener("pointerdown", down);
    canvas.addEventListener("pointermove", move);
    canvas.addEventListener("pointerup", up);
    canvas.addEventListener("pointercancel", up);
    canvas.addEventListener("keydown", key);
    return () => {
      canvas.removeEventListener("pointerdown", down);
      canvas.removeEventListener("pointermove", move);
      canvas.removeEventListener("pointerup", up);
      canvas.removeEventListener("pointercancel", up);
      canvas.removeEventListener("keydown", key);
    };
  }, [interactive, size]);

  return (
    <div className={`relative inline-block ${className}`} style={{ width: size, height: size }}>
      {/* dithered floor shadow */}
      <div
        aria-hidden="true"
        className="absolute left-1/2 -translate-x-1/2"
        style={{
          bottom: Math.round(size * 0.1),
          width: Math.round(size * 0.5),
          height: Math.round(size * 0.1),
          background: "radial-gradient(closest-side, rgb(0 0 0 / 0.7), transparent)",
          maskImage: "conic-gradient(#000 25%, transparent 0 50%, #000 0 75%, transparent 0)",
          maskSize: "4px 4px",
        }}
      />
      <canvas
        ref={canvasRef}
        role="img"
        aria-label={label}
        tabIndex={interactive ? 0 : -1}
        className={`pixelated relative block h-full w-full ${interactive ? "cursor-grab touch-pan-y active:cursor-grabbing" : ""}`}
      />
      {failed && (
        <div className="absolute inset-0 grid place-items-center text-center font-pixel text-[10px] text-muted">
          NO 3D
        </div>
      )}
    </div>
  );
}
