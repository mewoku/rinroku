import "server-only";

import { readdir } from "node:fs/promises";
import path from "node:path";

export interface UnityBuildFiles {
  loaderUrl: string;
  dataUrl: string;
  frameworkUrl: string;
  codeUrl: string;
}

const BUILD_DIR = path.join(process.cwd(), "public", "unity", "Build");

/**
 * Discovers a Unity WebGL build in public/unity/Build. Unity names files after the build folder
 * (`<name>.loader.js`, `<name>.data[.br|.gz|.unityweb]`, `<name>.framework.js[...]`, `<name>.wasm[...]`).
 */
export async function findUnityBuild(): Promise<UnityBuildFiles | null> {
  let files: string[];
  try {
    files = await readdir(BUILD_DIR);
  } catch {
    return null;
  }
  const loader = files.find((f) => f.endsWith(".loader.js"));
  if (!loader) return null;
  const name = loader.slice(0, -".loader.js".length);
  const pick = (base: string) => [base, `${base}.unityweb`, `${base}.br`, `${base}.gz`].find((f) => files.includes(f));
  const data = pick(`${name}.data`);
  const framework = pick(`${name}.framework.js`);
  const code = pick(`${name}.wasm`);
  if (!data || !framework || !code) return null;
  const url = (f: string) => `/unity/Build/${encodeURIComponent(f)}`;
  return { loaderUrl: url(loader), dataUrl: url(data), frameworkUrl: url(framework), codeUrl: url(code) };
}
