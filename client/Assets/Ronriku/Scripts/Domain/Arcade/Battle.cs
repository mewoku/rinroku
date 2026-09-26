using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;

namespace Ronriku.Domain.Arcade
{
    public sealed class BattleConfig
    {
        public long Seed;
        public int MonsterHp;
        /// <summary>Monster swings every this many ms of real time; hero hits push the swing back.</summary>
        public int AttackMs;
        public int Hearts = 3;
        public int Tier;
        public ChallengeKind[] Pool;
        /// <summary>Adaptive heat this config was built with (recorded in proofs for replay).</summary>
        public int Heat;

        /// <summary>World pools grow one kind per world so each world teaches something new.</summary>
        public static ChallengeKind[] PoolFor(int world)
        {
            var pool = new List<ChallengeKind> { ChallengeKind.Next, ChallengeKind.Sum, ChallengeKind.Memory, ChallengeKind.Odd };
            if (world >= 1) pool.Add(ChallengeKind.Mirror);
            if (world >= 2) pool.Add(ChallengeKind.Arrows);
            if (world >= 3) pool.Add(ChallengeKind.Scales);
            return pool.ToArray();
        }
    }

    public readonly struct HitResult
    {
        public readonly bool Correct;
        public readonly int Chips;
        public readonly int SpeedBonus;
        public readonly int Mult;
        public readonly int Damage;
        public readonly int Combo;

        public HitResult(bool correct, int chips, int speedBonus, int mult, int damage, int combo)
        {
            Correct = correct;
            Chips = chips;
            SpeedBonus = speedBonus;
            Mult = mult;
            Damage = damage;
            Combo = combo;
        }
    }

    /// <summary>
    /// Battle rules (pure, replayable). Each solved challenge is a hero attack worth
    /// (10 base + up to 10 speed) chips × (1 + combo, max ×6) mult. A wrong answer or a monster swing
    /// costs a heart and breaks the combo. Stars: hearts kept (3 → ★★★).
    /// </summary>
    public sealed class BattleState
    {
        public const int BaseChips = 10;
        public const int MaxSpeedBonus = 10;
        public const int FastMs = 2500;
        public const int SlowMs = 9000;
        public const int MaxMult = 6;
        /// <summary>Combo at which cards turn HARD (one tier up, +5 base chips).</summary>
        public const int HeatCombo = 3;
        public const int HardBonus = 5;

        private readonly List<(int answer, int ms)> _log = new List<(int, int)>();
        private readonly List<int> _swingsAt = new List<int>();
        /// <summary>Answer index at which each monster swing landed (cards depend on the combo, so replay needs this).</summary>
        public IReadOnlyList<int> SwingsAt => _swingsAt;
        private int _lastKind = -1;

        public BattleConfig Config { get; }
        public int Hp { get; private set; }
        public int Hearts { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Index { get; private set; }
        public int MonsterSwings { get; private set; }
        public Challenge Current { get; private set; }
        /// <summary>True when the current card is a HARD card (streak escalation).</summary>
        public bool CurrentHard { get; private set; }
        public IReadOnlyList<(int answer, int ms)> Log => _log;

        public bool Won => Hp <= 0;
        public bool Lost => Hearts <= 0 && Hp > 0;
        public bool Over => Won || Lost;

        public BattleState(BattleConfig config)
        {
            Config = config;
            Hp = config.MonsterHp;
            Hearts = config.Hearts;
            Current = Make(0);
        }

        private Challenge Make(int index)
        {
            var rng = new DeterministicRandom(unchecked((ulong)DailyPlan.Mix(Config.Seed, 2000 + index)));
            var pool = Config.Pool;
            int pick = rng.NextInt(pool.Length);
            if (pool.Length > 1 && pick == _lastKind) pick = (pick + 1 + rng.NextInt(pool.Length - 1)) % pool.Length;
            _lastKind = pick;
            CurrentHard = Combo >= HeatCombo;
            int tier = CurrentHard ? 1 : Config.Tier;
            return Challenges.Generate(pool[pick], DailyPlan.Mix(Config.Seed, 1000 + index), tier);
        }

        public static int SpeedBonus(int ms)
        {
            if (ms <= FastMs) return MaxSpeedBonus;
            if (ms >= SlowMs) return 0;
            return (int)Math.Round(MaxSpeedBonus * (SlowMs - ms) / (double)(SlowMs - FastMs));
        }

        public HitResult Answer(int answer, int thinkMs)
        {
            if (Over) throw new InvalidOperationException("battle over");
            _log.Add((answer, thinkMs));
            bool correct = Challenges.IsCorrect(Current, answer);
            HitResult result;
            if (correct)
            {
                int speed = SpeedBonus(thinkMs);
                int baseChips = BaseChips + (CurrentHard ? HardBonus : 0);
                int chips = baseChips + speed;
                int mult = Math.Min(MaxMult, 1 + Combo);
                int damage = chips * mult;
                Hp = Math.Max(0, Hp - damage);
                Combo++;
                BestCombo = Math.Max(BestCombo, Combo);
                result = new HitResult(true, baseChips, speed, mult, damage, Combo);
            }
            else
            {
                Combo = 0;
                Hearts--;
                result = new HitResult(false, 0, 0, 0, 0, 0);
            }
            Index++;
            if (!Over) Current = Make(Index);
            return result;
        }

        /// <summary>The monster's timed swing landed.</summary>
        public void MonsterSwing()
        {
            if (Over) return;
            MonsterSwings++;
            _swingsAt.Add(Index);
            Hearts--;
            Combo = 0;
        }

        public int Stars => Won ? Math.Max(1, Math.Min(3, Hearts)) : 0;

        /// <summary>
        /// Rebuilds a battle from its proof ("B1:h&lt;heat&gt;|a@ms,...|s&lt;index&gt;,...") so a verifier can check
        /// the stars without trusting the client. Swings are applied just before the answer at their index.
        /// </summary>
        public static BattleState Replay(BattleConfig config, string proof)
        {
            var state = new BattleState(config);
            string[] parts = proof.Substring(3).Split('|');
            var swings = new List<int>();
            if (parts.Length > 2)
                foreach (string s in parts[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    swings.Add(int.Parse(s.Substring(1)));
            int next = 0;
            foreach (string entry in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                while (next < swings.Count && swings[next] <= state.Index) { state.MonsterSwing(); next++; }
                if (state.Over) break;
                string[] am = entry.Split('@');
                state.Answer(int.Parse(am[0]), int.Parse(am[1]));
                if (state.Over) break;
            }
            while (next < swings.Count && !state.Over) { state.MonsterSwing(); next++; }
            return state;
        }
    }
}
