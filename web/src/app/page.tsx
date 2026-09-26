import { AmbientBackground } from "@/components/ambient/AmbientBackground";
import { HeroFigures } from "@/components/landing/HeroFigures";
import { LinkArt, PatternArt, SpatialArt } from "@/components/landing/TrialArt";
import { VoxelViewer } from "@/components/figure/VoxelViewer";
import { WorldMap } from "@/components/play/WorldMap";
import { PixelButton } from "@/components/ui/PixelButton";
import { PixelIcon, type IconName } from "@/components/ui/PixelIcon";
import { SectionTitle } from "@/components/ui/PixelPanel";
import { demoFigures, demoMonsters } from "@/lib/demo/data";
import { BOSS_ENTRY, EARN, formatSol } from "@/lib/economy";

const featured = demoFigures.filter((f) => f.rarity !== "Common").slice(0, 8);
const boss = demoMonsters.find((m) => m.tier === 5) ?? demoMonsters[0]!;

const TRIALS = [
  { palette: "pattern", name: "Pattern", verb: "Spot the rule", body: "Examples morph from input to output. Find the transformation, pick the grid that follows it.", Art: PatternArt },
  { palette: "lab", name: "Spatial", verb: "Turn the board", body: "Rotate the board, roll the cubes, land every shadow on its target in par moves.", Art: SpatialArt },
  { palette: "link", name: "Link", verb: "Draw one path", body: "Connect the numbers in order with a single line that fills every open tile.", Art: LinkArt },
] as const;

const STEPS: { icon: IconName; title: string; body: string }[] = [
  { icon: "play", title: "Arcade adventure", body: "Five worlds, sixty levels: puzzle battles, Balatro-style rune hands, ice dashes and a rhythm dungeon. A two-phase boss ends every world." },
  { icon: "bolt", title: "Daily three", body: "Pattern → Spatial → Link. Same puzzles for everyone, one run a day. Rating, streak and a share grid." },
  { icon: "shard", title: "Earn shards", body: `+${EARN.levelClear} per clear, +${EARN.dailyCompletion} per Daily, up to +${EARN.bossWinMax} per boss. Spend them on figures.` },
  { icon: "cube", title: "Collect figures", body: "Procedural voxel people in 3×3, 4×4 and 5×5 tiers. Equip one as your avatar. Early access: collectibles live on Solana devnet." },
];

export default function LandingPage() {
  return (
    <div data-palette="lab" className="relative overflow-x-clip">
      <AmbientBackground palette="lab" />

      {/* ---------------- hero ---------------- */}
      <section className="mx-auto grid max-w-[1200px] items-center gap-8 px-4 pt-8 pb-12 md:grid-cols-[1.1fr_1fr] md:pt-16 md:pb-20">
        <div className="px-rise flex flex-col items-center text-center md:items-start md:text-left">
          <p className="font-pixel text-[12px] leading-4 text-yellow uppercase">Puzzle combat · Solana Seeker</p>
          <h1
            className="mt-4 text-[44px] leading-[48px] text-teal min-[400px]:text-[56px] min-[400px]:leading-[56px] md:text-[96px] md:leading-[96px]"
            style={{
              textShadow: "0 4px 0 #135b73, 0 8px 0 #0b3b4a, 0 0 32px rgb(17 197 179 / 0.45)",
            }}
          >
            RONRIKU
          </h1>
          <p className="mt-6 max-w-[440px] text-[18px] leading-7 text-text">
            Every card is a puzzle. Solve fast, stack combos, watch chips × mult flatten the monster.
          </p>
          <p className="mt-2 max-w-[440px] text-[14px] leading-5 text-muted">Bite-size fights, a daily brain run, a funky chiptune soundtrack. Free, no wallet needed to start.</p>
          <div className="mt-8 flex w-full flex-col items-stretch gap-4 min-[400px]:w-auto min-[400px]:flex-row min-[400px]:items-center">
            <PixelButton href="/play" size="lg">
              <PixelIcon name="play" size={16} /> Play in browser
            </PixelButton>
            <PixelButton href="#get-seeker" size="lg" variant="secondary" palette="frost">
              Get on Seeker
            </PixelButton>
          </div>
        </div>
        <HeroFigures figures={featured} />
      </section>

      {/* ---------------- trials ---------------- */}
      <section className="mx-auto max-w-[1200px] px-4 py-12" aria-labelledby="trials-title">
        <SectionTitle kicker="The Daily" title="Three trials. Three worlds." />
        <span id="trials-title" className="sr-only">Daily trials</span>
        <div className="grid gap-6 md:grid-cols-3">
          {TRIALS.map(({ palette, name, verb, body, Art }, i) => (
            <article key={name} data-palette={palette} data-accent="true" className="px-panel flex flex-col gap-4 p-4 sm:p-6">
              <div className="flex items-center justify-between">
                <span className="font-pixel text-[12px] text-muted">TRIAL 0{i + 1}</span>
                <span className="h-2 w-12" style={{ background: "linear-gradient(90deg, var(--accent), var(--accent-2))" }} />
              </div>
              <div className="dither-bg px-border grid place-items-center p-4" style={{ background: "color-mix(in srgb, var(--ambient) 70%, #07080b)", ["--pb" as string]: "var(--accent-2)" }}>
                <div className="w-full max-w-[220px]">
                  <Art />
                </div>
              </div>
              <div>
                <h3 className="text-[24px] leading-8 text-accent">{name}</h3>
                <p className="font-pixel text-[12px] leading-4 text-text uppercase">{verb}</p>
                <p className="mt-2 text-[15px] leading-6 text-muted">{body}</p>
              </div>
            </article>
          ))}
        </div>
      </section>

      {/* ---------------- adventure ---------------- */}
      <section className="mx-auto max-w-[1200px] px-4 py-12" aria-label="Adventure worlds">
        <SectionTitle kicker="Play · Adventure" title="Five worlds. Sixty guardians." />
        <WorldMap />
      </section>

      {/* ---------------- how it works ---------------- */}
      <section className="mx-auto max-w-[1200px] px-4 py-12" aria-label="How it works">
        <SectionTitle kicker="How it works" title="Short sessions. Long streaks." />
        <ol className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {STEPS.map((s, i) => (
            <li key={s.title} className="px-panel flex gap-4 p-4">
              <span className="grid size-12 shrink-0 place-items-center bg-teal text-bg-0" style={{ boxShadow: "inset -4px -4px 0 #135b73" }}>
                <PixelIcon name={s.icon} size={24} />
              </span>
              <div>
                <p className="font-pixel text-[10px] text-yellow">STEP {i + 1}</p>
                <h3 className="text-[16px] leading-6 text-text">{s.title}</h3>
                <p className="mt-1 text-[14px] leading-5 text-muted">{s.body}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      {/* ---------------- boss teaser ---------------- */}
      <section data-palette="boss" className="mx-auto max-w-[1200px] px-4 py-12" aria-label="Boss events">
        <div data-accent="true" className="px-panel scanlines relative grid items-center gap-6 overflow-hidden p-6 md:grid-cols-[auto_1fr] md:p-10" style={{ background: "linear-gradient(135deg, #3a0a14 0%, #14161d 70%)" }}>
          <div className="relative mx-auto">
            <div aria-hidden="true" className="absolute inset-0" style={{ background: "radial-gradient(closest-side, rgb(255 59 92 / 0.35), transparent)" }} />
            <VoxelViewer encoding={boss.encoding} size={200} resolution={56} rim="#FF3B5C" label={`Boss ${boss.name}`} />
          </div>
          <div>
            <p className="font-pixel text-[12px] text-accent-2 uppercase">Bosses · weekly raids</p>
            <h2 className="mt-2 text-[32px] leading-10 text-accent md:text-[40px] md:leading-[48px]">{boss.name}, the Warden</h2>
            <p className="mt-3 max-w-[520px] text-[16px] leading-6 text-text">
              Weekly raids: three chained puzzles against the clock. Every solve chips its HP bar. Win up to {EARN.bossWinMax.toLocaleString("en-US")} shards and a spot on the boss board.
            </p>
            <div className="mt-4 max-w-[420px]" aria-hidden="true">
              <div className="flex justify-between font-pixel text-[10px] text-muted">
                <span>HP</span>
                <span>3 / 3 STAGES</span>
              </div>
              <div className="px-border mt-1 h-4 bg-bg-0" style={{ ["--pb" as string]: "#FF3B5C" }}>
                <div className="h-full" style={{ width: "72%", background: "repeating-linear-gradient(90deg, #FF3B5C 0 12px, #c92a47 12px 14px)" }} />
              </div>
            </div>
            <div className="mt-6 flex flex-wrap items-center gap-4">
              <PixelButton href="/bosses" palette="boss">
                <PixelIcon name="skull" size={16} /> Enter the arena
              </PixelButton>
              <span className="font-pixel text-[12px] text-muted">
                ENTRY {BOSS_ENTRY.shards} ◆ OR {formatSol(BOSS_ENTRY.lamports)} DEVNET SOL
              </span>
            </div>
          </div>
        </div>
      </section>

      {/* ---------------- seeker ---------------- */}
      <section id="get-seeker" className="mx-auto max-w-[1200px] scroll-mt-24 px-4 pt-12 pb-[120px] md:pb-20" aria-label="Get on Seeker">
        <div data-palette="frost" data-accent="true" className="px-panel flex flex-col items-start gap-4 p-6 md:flex-row md:items-center md:justify-between md:p-8">
          <div>
            <p className="font-pixel text-[12px] text-accent uppercase">Solana Seeker</p>
            <h2 className="mt-1 text-[24px] leading-8 text-text md:text-[32px] md:leading-10">Built for the phone in your pocket.</h2>
            <p className="mt-2 max-w-[560px] text-[15px] leading-6 text-muted">
              Native Android build with haptics, gyroscope parallax and 60 fps pixel combat, made for the Seeker. Coming to the Solana dApp Store — play the same game in the browser today.
            </p>
          </div>
          <div className="flex flex-wrap gap-4">
            <PixelButton href="/play" palette="frost">Play now</PixelButton>
            <PixelButton href="/market" variant="secondary" palette="link">
              Visit the shop
            </PixelButton>
          </div>
        </div>
        <footer className="mt-12 flex flex-wrap items-center justify-between gap-4 border-t-2 border-line pt-6 font-pixel text-[10px] text-muted">
          <span>© RONRIKU · Early access · collectibles on Solana devnet (test SOL, no real funds)</span>
          <span>Fonts: Silkscreen, Pixelify Sans (OFL)</span>
        </footer>
      </section>
    </div>
  );
}
