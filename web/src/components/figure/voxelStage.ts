"use client";

import * as THREE from "three";
import { decodeRf1 } from "@/lib/rf1";
import { buildVoxelMesh } from "@/lib/voxelMesh";

/**
 * One shared WebGL renderer for every voxel viewer on the page (browsers cap WebGL contexts at
 * ~16). Each viewer owns a 2D canvas; the stage renders the viewer's scene into a corner of its
 * low-resolution GL canvas and blits it across, then CSS upscales with `image-rendering: pixelated`.
 */
const STAGE_SIZE = 256;
const FPS = 30;
const ELEVATION = Math.atan(1 / Math.SQRT2); // true isometric (~35.26°)

const vertexShader = /* glsl */ `
  attribute vec3 color;
  varying vec3 vColor;
  varying vec3 vNormal;
  void main() {
    vColor = color;
    vNormal = normalize(normalMatrix * normal);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

// Flat MagicaVoxel-like shading in view space: top 1.0, left ~0.8, right ~0.62 (matches core raster).
const fragmentShader = /* glsl */ `
  precision mediump float;
  varying vec3 vColor;
  varying vec3 vNormal;
  uniform vec3 uRim;
  void main() {
    vec3 n = normalize(vNormal);
    float k = 0.62 + max(dot(n, vec3(-0.255, 0.38, 0.06)), 0.0);
    vec3 c = vColor * k;
    // faint accent rim on faces pointing away from the light for extra pop on dark backgrounds
    c += uRim * max(n.x, 0.0) * 0.08;
    gl_FragColor = vec4(c, 1.0);
  }
`;

export interface ViewerHandle {
  canvas: HTMLCanvasElement;
  resolution: number;
  yaw: number;
  autoRotate: boolean;
  visible: boolean;
  dirty: boolean;
  /** radians per second */
  spin: number;
  pausedUntil: number;
  scene: THREE.Scene;
  extent: number;
  targetY: number;
  dispose: () => void;
}

class VoxelStage {
  private renderer: THREE.WebGLRenderer | null = null;
  private camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
  private viewers = new Set<ViewerHandle>();
  private raf = 0;
  private last = 0;
  private lastFrame = 0;
  private failed = false;

  private ensureRenderer(): THREE.WebGLRenderer | null {
    if (this.renderer || this.failed) return this.renderer;
    try {
      const canvas = document.createElement("canvas");
      canvas.width = STAGE_SIZE;
      canvas.height = STAGE_SIZE;
      this.renderer = new THREE.WebGLRenderer({ canvas, antialias: false, alpha: true, preserveDrawingBuffer: true, powerPreference: "low-power" });
      this.renderer.setPixelRatio(1);
      this.renderer.setSize(STAGE_SIZE, STAGE_SIZE, false);
      this.renderer.setClearColor(0x000000, 0);
      this.renderer.setScissorTest(true);
    } catch {
      this.failed = true;
      this.renderer = null;
    }
    return this.renderer;
  }

  get available(): boolean {
    return this.ensureRenderer() !== null;
  }

  add(canvas: HTMLCanvasElement, encoding: string, opts: { resolution: number; autoRotate: boolean; yaw?: number; rim?: string }): ViewerHandle | null {
    if (!this.ensureRenderer()) return null;
    const figure = decodeRf1(encoding);
    const data = buildVoxelMesh(figure);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute("position", new THREE.BufferAttribute(data.positions, 3));
    geometry.setAttribute("normal", new THREE.BufferAttribute(data.normals, 3));
    geometry.setAttribute("color", new THREE.BufferAttribute(data.colors, 3));
    geometry.setIndex(new THREE.BufferAttribute(data.indices, 1));
    const material = new THREE.ShaderMaterial({
      vertexShader,
      fragmentShader,
      uniforms: { uRim: { value: new THREE.Color(opts.rim ?? "#11C5B3") } },
    });
    const mesh = new THREE.Mesh(geometry, material);
    const scene = new THREE.Scene();
    scene.add(mesh);

    // Fit: rotating footprint diagonal vs projected height, with a small margin.
    const diag = figure.size * Math.SQRT2;
    const projH = figure.height * Math.cos(ELEVATION) + diag * Math.sin(ELEVATION);
    const extent = (Math.max(diag, projH) / 2) * 1.12;

    const res = Math.min(STAGE_SIZE, Math.max(16, Math.round(opts.resolution)));
    canvas.width = res;
    canvas.height = res;
    const handle: ViewerHandle = {
      canvas,
      resolution: res,
      yaw: opts.yaw ?? Math.PI / 4,
      autoRotate: opts.autoRotate,
      visible: true,
      dirty: true,
      spin: 0.5,
      pausedUntil: 0,
      scene,
      extent,
      targetY: figure.height / 2,
      dispose: () => {
        geometry.dispose();
        material.dispose();
        this.viewers.delete(handle);
        if (this.viewers.size === 0) this.stop();
      },
    };
    this.viewers.add(handle);
    this.start();
    return handle;
  }

  private start() {
    if (this.raf) return;
    this.last = performance.now();
    const loop = (now: number) => {
      this.raf = requestAnimationFrame(loop);
      if (document.hidden) return;
      if (now - this.lastFrame < 1000 / FPS) return;
      const dt = Math.min(0.1, (now - this.last) / 1000);
      this.last = now;
      this.lastFrame = now;
      this.frame(dt, now);
    };
    this.raf = requestAnimationFrame(loop);
  }

  private stop() {
    cancelAnimationFrame(this.raf);
    this.raf = 0;
  }

  private frame(dt: number, now: number) {
    const renderer = this.renderer;
    if (!renderer) return;
    const gl = renderer.domElement;
    for (const v of this.viewers) {
      if (!v.visible) continue;
      if (v.autoRotate && now > v.pausedUntil) {
        v.yaw += v.spin * dt;
        v.dirty = true;
      }
      if (!v.dirty) continue;
      v.dirty = false;
      const r = v.resolution;
      const cam = this.camera;
      cam.left = -v.extent;
      cam.right = v.extent;
      cam.top = v.extent;
      cam.bottom = -v.extent;
      cam.updateProjectionMatrix();
      const dist = 40;
      cam.position.set(
        Math.sin(v.yaw) * Math.cos(ELEVATION) * dist,
        v.targetY + Math.sin(ELEVATION) * dist,
        Math.cos(v.yaw) * Math.cos(ELEVATION) * dist,
      );
      cam.lookAt(0, v.targetY, 0);
      renderer.setViewport(0, 0, r, r);
      renderer.setScissor(0, 0, r, r);
      renderer.clear();
      renderer.render(v.scene, cam);
      const ctx = v.canvas.getContext("2d");
      if (!ctx) continue;
      ctx.clearRect(0, 0, r, r);
      ctx.drawImage(gl, 0, STAGE_SIZE - r, r, r, 0, 0, r, r);
    }
  }
}

let stage: VoxelStage | null = null;
export function getVoxelStage(): VoxelStage {
  stage ??= new VoxelStage();
  return stage;
}
