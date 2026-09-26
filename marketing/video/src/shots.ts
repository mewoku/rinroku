/**
 * Gameplay shots. Each id maps to public/clips/<id>.mp4 (record per docs/pitch/04-video-ads.md)
 * and a fallback still from docs/evidence/ used with Ken Burns motion until the clip exists.
 *
 * raw: recorded footage name: public/clips/<raw>.mp4 (preferred) or the JPG sequence public/clips/raw/<raw>/ from FootageTests.cs.
 * startFrom: seconds to skip at the head of the clip or sequence (tune after recording).
 * tail: if set, start this many seconds before the END of the sequence instead (e.g. the KO at the end of a fight).
 * focusY: vertical focus (0 = top, 1 = bottom) when a 1080×2400 frame is cropped to 9:16 full-bleed.
 */
export type ShotDef = { still: string; raw?: string; startFrom: number; tail?: number; focusY: number; note: string };

export const SHOTS = {
  "battle-combo": { still: "arcade-2-battle-hit.png", raw: "battle", startFrom: 4.6, focusY: 0.15, note: "W2-5 battle, 3–4 fast right answers, combo ×3+" },
  "battle-ko": { still: "arcade-1-battle.png", raw: "battle", startFrom: 14.25, focusY: 0.15, note: "final hit, HP to 0, victory banner" },
  "wrong-answer": { still: "arcade-1-battle.png", startFrom: 0, focusY: 0.15, note: "wrong tap, monster swings, heart lost" },
  "charm-pick": { still: "arcade-3-charm-pick.png", raw: "cards", startFrom: 1.6, focusY: 0.2, note: "pick a charm" },
  "rune-play": { still: "arcade-5-rune-scoring.png", raw: "cards", startFrom: 5.2, focusY: 0.5, note: "select combo, PLAY, chips × mult count-up" },
  "ice-dash": { still: "arcade-6-ice-dash.png", raw: "dash", startFrom: 0.5, focusY: 0.45, note: "swipes at par, gems, door, 3 stars" },
  "beat-crawl": { still: "arcade-7-beat-crawl.png", raw: "crawl", startFrom: 3.5, focusY: 0.5, note: "on-beat moves, bump a slime, combo" },
  "map-walk": { still: "arcade-0-map.png", raw: "map", startFrom: 0, focusY: 0.6, note: "hero walks node 1 → 2, world tab switch" },
  boss: { still: "seeker-20x9-0-bosses.png", startFrom: 0, focusY: 0.2, note: "world boss phase 1 → phase 2" },
  daily: { still: "seeker-20x9-1-home.png", raw: "daily", startFrom: 0, focusY: 0.45, note: "BEGIN, pattern, answer, rating count-up" },
  "daily-results": { still: "seeker-20x9-4-results.png", startFrom: 0, focusY: 0.4, note: "results screen (still only)" },
  shop: { still: "seeker-20x9-0-shop.png", startFrom: 0, focusY: 0.35, note: "scroll the voxel figure shelf" },
  "web-play": { still: "play-1440.png", startFrom: 0, focusY: 0.5, note: "browser /play (optional)" },
} satisfies Record<string, ShotDef>;

export type ShotId = keyof typeof SHOTS;
