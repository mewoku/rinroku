using System;
using Ronriku.Domain.Player;

namespace Ronriku.Domain.Arcade
{
    /// <summary>
    /// Adaptive difficulty ("heat") per arcade mode, −2…+2, so nobody finds a mode trivially basic or
    /// hopelessly hard. Clean wins (★★★) heat it up, losses cool it down, scrappy wins hold. Heat scales
    /// monster HP / swing speed, card targets, board tier and Beat Crawl tempo. Rewards and stars are
    /// unaffected, so heat only changes how the level feels, never what it pays.
    /// </summary>
    public static class Adaptive
    {
        public const int Min = -2;
        public const int Max = 2;
        private const int Modes = 5;

        public static int Heat(PlayerProfile profile, LevelMode mode)
        {
            if (profile?.modeHeat == null || profile.modeHeat.Count <= (int)mode) return 0;
            return Math.Max(Min, Math.Min(Max, profile.modeHeat[(int)mode]));
        }

        /// <summary>Updates heat after a run; returns the new value.</summary>
        public static int Record(PlayerProfile profile, LevelMode mode, bool won, int stars)
        {
            while (profile.modeHeat.Count < Modes) profile.modeHeat.Add(0);
            int heat = Heat(profile, mode);
            if (!won) heat--;
            else if (stars >= 3) heat++;
            heat = Math.Max(Min, Math.Min(Max, heat));
            profile.modeHeat[(int)mode] = heat;
            return heat;
        }

        public static int TierFor(int baseTier, int heat) => heat >= 1 ? 1 : heat <= -1 ? 0 : baseTier;

        public static BattleConfig Apply(BattleConfig c, int heat)
        {
            c.MonsterHp = (int)Math.Round(c.MonsterHp * (1.0 + 0.12 * heat));
            c.AttackMs = Math.Max(5000, c.AttackMs - 700 * heat);
            c.Tier = TierFor(c.Tier, heat);
            c.Heat = heat;
            return c;
        }

        public static CardsConfig Apply(CardsConfig c, int heat)
        {
            c.Target = (int)Math.Round(c.Target * (1.0 + 0.11 * heat) / 10.0) * 10;
            c.Heat = heat;
            return c;
        }

        /// <summary>Beat Crawl: music beats per hero move. Struggling players get a slower walk.</summary>
        public static int CrawlBeatsPerMove(int tuned, int heat) => heat <= -1 ? Math.Max(tuned, 3) : tuned;

        public static string Label(int heat) => heat switch
        {
            <= -2 => "CHILL",
            -1 => "EASY",
            0 => "NORMAL",
            1 => "HOT",
            _ => "BLAZING"
        };
    }
}
