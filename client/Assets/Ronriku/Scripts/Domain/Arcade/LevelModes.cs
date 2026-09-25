using System;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Daily;

namespace Ronriku.Domain.Arcade
{
    /// <summary>What a map level plays as. Classic trials (Pattern/Shadow/Link) stay in Daily and raids.</summary>
    public enum LevelMode { Battle = 0, Cards = 1, Dash = 2, Crawl = 3, Boss = 4 }

    /// <summary>
    /// Arcade contract for adventure levels (v3). Everything derives from LevelDef.Seed:
    ///   mode       = Layout[index]
    ///   tier       = world 1 first half → 0, everything else → 1 (kept in the middle band)
    ///   battle     = BattleConfig { Seed = Mix(seed, 11), HP 180 + 20w + 4i, swing 9000 - 400w ms (≥ 7000) }
    ///   cards      = CardsConfig { Seed = Mix(seed, 22), target 540 + 10w + 6i, charms from Offer(Mix(seed, 55), 3) }
    ///   dash       = DashLevel.Generate(Mix(seed, 33), tier)
    ///   crawl      = CrawlLevel.Generate(Mix(seed, 44), tier)
    ///   boss       = battle (HP 360 + 40w, tier 1) then cards (target 820 + 15w, two charms)
    /// </summary>
    public static class LevelModes
    {
        public static readonly LevelMode[] Layout =
        {
            LevelMode.Battle, LevelMode.Battle, LevelMode.Dash, LevelMode.Cards,
            LevelMode.Battle, LevelMode.Crawl, LevelMode.Cards, LevelMode.Dash,
            LevelMode.Battle, LevelMode.Crawl, LevelMode.Cards, LevelMode.Boss
        };

        public static LevelMode For(int index) => Layout[index];

        public static int Tier(int world, int index) => world == 0 && index < 6 ? 0 : 1;

        public static string Name(LevelMode mode) => mode switch
        {
            LevelMode.Battle => "BATTLE",
            LevelMode.Cards => "RUNE HAND",
            LevelMode.Dash => "ICE DASH",
            LevelMode.Crawl => "BEAT CRAWL",
            _ => "BOSS"
        };

        public static string Hint(LevelMode mode) => mode switch
        {
            LevelMode.Battle => "SOLVE FAST TO HIT HARD",
            LevelMode.Cards => "PLAY COMBOS · BEAT THE HP",
            LevelMode.Dash => "SWIPE · GRAB GEMS · EXIT",
            LevelMode.Crawl => "MOVE ON THE BEAT",
            _ => "TWO PHASES · NO MERCY"
        };

        public static BattleConfig Battle(LevelDef def) => new BattleConfig
        {
            Seed = DailyPlan.Mix(def.Seed, 11),
            MonsterHp = 180 + 20 * def.World + 4 * def.Index,
            AttackMs = Math.Max(7000, 9000 - 400 * def.World),
            Tier = Tier(def.World, def.Index),
            Pool = BattleConfig.PoolFor(def.World)
        };

        public static CharmId[] CharmOffer(LevelDef def) => Charms.Offer(DailyPlan.Mix(def.Seed, 55), 3);

        public static CardsConfig Cards(LevelDef def, params CharmId[] charms) => new CardsConfig
        {
            Seed = DailyPlan.Mix(def.Seed, 22),
            Target = 540 + 10 * def.World + 6 * def.Index,
            Charms = charms
        };

        public static DashLevel Dash(LevelDef def) => DashLevel.Generate(DailyPlan.Mix(def.Seed, 33), Tier(def.World, def.Index));

        public static CrawlLevel Crawl(LevelDef def) => CrawlLevel.Generate(DailyPlan.Mix(def.Seed, 44), Tier(def.World, def.Index));

        public static BattleConfig BossBattle(LevelDef def) => new BattleConfig
        {
            Seed = DailyPlan.Mix(def.Seed, 11),
            MonsterHp = 360 + 40 * def.World,
            AttackMs = Math.Max(6500, 8500 - 400 * def.World),
            Tier = 1,
            Pool = BattleConfig.PoolFor(def.World)
        };

        public static CardsConfig BossCards(LevelDef def, params CharmId[] charms) => new CardsConfig
        {
            Seed = DailyPlan.Mix(def.Seed, 22),
            Target = 820 + 15 * def.World,
            Charms = charms
        };
    }
}
