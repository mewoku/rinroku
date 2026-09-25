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

        private readonly List<(int answer, int ms)> _log = new List<(int, int)>();
        private int _lastKind = -1;

        public BattleConfig Config { get; }
        public int Hp { get; private set; }
        public int Hearts { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Index { get; private set; }
        public int MonsterSwings { get; private set; }
        public Challenge Current { get; private set; }
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
            return Challenges.Generate(pool[pick], DailyPlan.Mix(Config.Seed, 1000 + index), Config.Tier);
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
                int chips = BaseChips + speed;
                int mult = Math.Min(MaxMult, 1 + Combo);
                int damage = chips * mult;
                Hp = Math.Max(0, Hp - damage);
                Combo++;
                BestCombo = Math.Max(BestCombo, Combo);
                result = new HitResult(true, BaseChips, speed, mult, damage, Combo);
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
            Hearts--;
            Combo = 0;
        }

        public int Stars => Won ? Math.Max(1, Math.Min(3, Hearts)) : 0;
    }
}
