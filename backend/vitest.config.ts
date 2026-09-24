import { defineConfig } from "vitest/config";

// Integration tests against the local Supabase stack (`npx supabase start`). Serial: they share one DB.
export default defineConfig({
  test: { globalSetup: ["tests/global-setup.ts"], include: ["tests/**/*.test.ts"], testTimeout: 60_000, hookTimeout: 120_000, fileParallelism: false },
});
