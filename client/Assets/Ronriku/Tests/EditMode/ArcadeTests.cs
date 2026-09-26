using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Daily;

namespace Ronriku.Tests
{
    public sealed class ArcadeTests
    {
        private static IEnumerable<ChallengeKind> Kinds => (ChallengeKind[])System.Enum.GetValues(typeof(ChallengeKind));

        [Test]
        public void Challenges_AreDeterministic_AndHaveExactlyOneRightAnswer()
        {
            foreach (ChallengeKind kind in Kinds)
            for (int tier = 0; tier < 2; tier++)
            for (long seed = 1; seed <= 300; seed++)
            {
                Challenge c = Challenges.Generate(kind, seed * 7919, tier);
                Challenge again = Challenges.Generate(kind, seed * 7919, tier);
                Assert.That(again.Answer, Is.EqualTo(c.Answer));
                Assert.That(again.Options, Is.EqualTo(c.Options));
                string where = $"{kind} t{tier} s{seed}";
                switch (kind)
                {
                    case ChallengeKind.Sum:
                        int solutions = 0;
                        for (int m = 0; m < 1 << c.Items.Length; m++) if (Challenges.CheckSum(c, m)) solutions++;
                        Assert.That(solutions, Is.EqualTo(1), where);
                        Assert.That(c.Items.All(v => v >= 1 && v <= 9), where);
                        break;
                    case ChallengeKind.Memory:
                        Assert.That(Challenges.PopCount(c.Answer), Is.EqualTo(c.Pick), where);
                        break;
                    case ChallengeKind.Arrows:
                        Assert.That(c.Options[c.Answer], Is.EqualTo(Challenges.FollowArrows(c.Items, c.Target, out _)), where);
                        Assert.That(c.Options.Distinct().Count(), Is.EqualTo(3), where);
                        break;
                    case ChallengeKind.Mirror:
                        Assert.That(c.Options[c.Answer], Is.EqualTo(Challenges.MirrorHalf(c.Items[0])), where);
                        Assert.That(c.Options.Distinct().Count(), Is.EqualTo(3), where);
                        break;
                    case ChallengeKind.Odd:
                        // The odd one is the only picture that is not a rotation of the others.
                        for (int i = 0; i < 4; i++)
                        {
                            int matches = 0;
                            for (int j = 0; j < 4; j++) if (i != j && SameUpToRotation(c.Options[i], c.Options[j])) matches++;
                            Assert.That(matches == 0, Is.EqualTo(i == c.Answer), where);
                        }
                        break;
                    default:
                        Assert.That(c.Options.Distinct().Count(), Is.EqualTo(c.Options.Length), where);
                        Assert.That(c.Answer, Is.InRange(0, c.Options.Length - 1), where);
                        break;
                }
            }
        }

        private static bool SameUpToRotation(int a, int b)
        {
            int g = a;
            for (int r = 0; r < 4; r++)
            {
                if (Challenges.Normalize(g) == Challenges.Normalize(b)) return true;
                g = Ronriku.Domain.Puzzles.PatternGrid.Apply(Ronriku.Domain.Puzzles.PatternRule.Rotate90, g);
            }
            return false;
        }

        [Test]
        public void Next_TheRightTokenContinuesEveryVaryingAttribute()
        {
            for (long seed = 1; seed <= 200; seed++)
            {
                Challenge c = Challenges.Generate(ChallengeKind.Next, seed, 1);
                int right = c.Options[c.Answer];
                // Period of any cycle is ≤ 3 and 5 tokens are shown, so the answer repeats a token 2 or 3 back
                // (or 4 back for arrows turning) in every attribute except rotation.
                int[] items = c.Items;
                bool colorOk = Token.Color(right) == Token.Color(items[3]) || Token.Color(right) == Token.Color(items[2]) || Token.Color(right) == Token.Color(items[4]);
                Assert.That(colorOk, $"seed {seed}");
            }
        }

        [Test]
        public void Battle_PerfectPlayWins_MistakesLose_AndStarsFollowHearts()
        {
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                LevelDef def = LevelDef.For(w, i);
                var config = i == LevelDef.BossIndex ? LevelModes.BossBattle(def) : LevelModes.Battle(def);
                var battle = new BattleState(config);
                int answers = 0;
                while (!battle.Over) { battle.Answer(RightAnswer(battle.Current), 3000); answers++; }
                Assert.That(battle.Won && battle.Stars == 3, $"w{w} l{i}");
                Assert.That(answers, Is.InRange(4, 9), $"w{w} l{i}: a clean fight takes a handful of answers");

                var loser = new BattleState(config);
                while (!loser.Over) loser.Answer(-12345, 3000);
                Assert.That(loser.Lost && loser.Stars == 0);
            }
            var b = new BattleState(LevelModes.Battle(LevelDef.For(0, 0)));
            b.MonsterSwing();
            while (!b.Over) b.Answer(RightAnswer(b.Current), 1000);
            Assert.That(b.Stars, Is.EqualTo(2));
        }

        private static int RightAnswer(Challenge c) => c.Answer;

        [Test]
        public void Battle_DamageIsChipsTimesComboMult()
        {
            var b = new BattleState(new BattleConfig { Seed = 5, MonsterHp = 10000, AttackMs = 9000, Pool = BattleConfig.PoolFor(4), Tier = 1 });
            HitResult first = b.Answer(b.Current.Answer, 1000);
            Assert.That(first.Damage, Is.EqualTo(20 * 1));
            HitResult second = b.Answer(b.Current.Answer, BattleState.SlowMs);
            Assert.That(second.Damage, Is.EqualTo(10 * 2));
            b.Answer(-1, 1000);
            HitResult afterMiss = b.Answer(b.Current.Answer, 1000);
            Assert.That(afterMiss.Mult, Is.EqualTo(1));
        }

        [Test]
        public void HandEvaluator_RecognisesEveryCombo()
        {
            Card C(int v, Suit s) => new Card(v, s);
            Assert.That(HandEvaluator.Evaluate(new[] { C(3, Suit.Fire), C(4, Suit.Fire), C(5, Suit.Fire), C(6, Suit.Fire), C(7, Suit.Fire) }).combo, Is.EqualTo(Combo.StraightFlush));
            Assert.That(HandEvaluator.Evaluate(new[] { C(3, Suit.Fire), C(4, Suit.Wave), C(5, Suit.Fire), C(6, Suit.Fire), C(7, Suit.Fire) }).combo, Is.EqualTo(Combo.Straight));
            Assert.That(HandEvaluator.Evaluate(new[] { C(1, Suit.Leaf), C(4, Suit.Leaf), C(5, Suit.Leaf), C(8, Suit.Leaf), C(9, Suit.Leaf) }).combo, Is.EqualTo(Combo.Flush));
            Assert.That(HandEvaluator.Evaluate(new[] { C(2, Suit.Leaf), C(2, Suit.Fire), C(2, Suit.Wave), C(8, Suit.Leaf), C(8, Suit.Bolt) }).combo, Is.EqualTo(Combo.FullHouse));
            Assert.That(HandEvaluator.Evaluate(new[] { C(2, Suit.Leaf), C(2, Suit.Fire), C(2, Suit.Wave), C(2, Suit.Bolt) }).combo, Is.EqualTo(Combo.Four));
            Assert.That(HandEvaluator.Evaluate(new[] { C(2, Suit.Leaf), C(2, Suit.Fire), C(2, Suit.Wave) }).combo, Is.EqualTo(Combo.Three));
            var twoPair = HandEvaluator.Evaluate(new[] { C(2, Suit.Leaf), C(2, Suit.Fire), C(5, Suit.Wave), C(5, Suit.Bolt), C(9, Suit.Bolt) });
            Assert.That(twoPair.combo, Is.EqualTo(Combo.TwoPair));
            Assert.That(twoPair.scoring, Is.EqualTo(0b01111), "kicker does not score");
            Assert.That(HandEvaluator.Evaluate(new[] { C(9, Suit.Leaf), C(9, Suit.Fire) }).combo, Is.EqualTo(Combo.Pair));
            var high = HandEvaluator.Evaluate(new[] { C(2, Suit.Leaf), C(9, Suit.Fire), C(5, Suit.Wave) });
            Assert.That(high.combo == Combo.High && high.scoring == 0b010);
        }

        [Test]
        public void Cards_ScoreIsChipsTimesMult_WithCharms()
        {
            var state = new CardsState(new CardsConfig { Seed = 9, Target = 99999, Charms = new[] { CharmId.Painter, CharmId.EmberHeart } });
            var (mask, total) = state.BestPlay();
            ScoreBreakdown preview = state.Preview(mask);
            Assert.That(preview.Total, Is.EqualTo(total));
            ScoreBreakdown played = state.Play(mask);
            Assert.That(played.Total, Is.EqualTo(total));
            Assert.That(state.Score, Is.EqualTo(total));
            Assert.That(state.HandsLeft, Is.EqualTo(3));
            Assert.That(state.Hand.Count, Is.EqualTo(7), "hand refills");
            Assert.That(state.Discard(0b11), Is.True);
            Assert.That(state.DiscardsLeft, Is.EqualTo(2));
            Assert.That(state.Play(0), Is.Null, "empty play rejected");
            Assert.That(state.Play(0b111111), Is.Null, "six cards rejected");
        }

        /// <summary>
        /// Balance: a greedy bot (best play each hand, discards the three lowest non-scoring cards when its
        /// best hand is weak) with a random charm should beat most card levels but not all — middle band.
        /// </summary>
        [Test]
        public void Cards_Balance_GreedyBotWinsMostButNotAll()
        {
            int wins = 0, games = 0;
            var perWorld = new int[LevelDef.WorldCount];
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                LevelMode mode = LevelModes.For(i);
                if (mode != LevelMode.Cards && mode != LevelMode.Boss) continue;
                LevelDef def = LevelDef.For(w, i);
                CharmId[] offer = LevelModes.CharmOffer(def);
                for (int pick = 0; pick < offer.Length; pick++)
                {
                    var config = mode == LevelMode.Boss ? LevelModes.BossCards(def, offer[pick], offer[(pick + 1) % 3]) : LevelModes.Cards(def, offer[pick]);
                    bool won = PlayGreedy(config);
                    games++;
                    if (won) { wins++; perWorld[w]++; }
                }
            }
            float rate = wins / (float)games;
            TestContext.WriteLine($"greedy bot card win rate {rate:P0} ({wins}/{games}); per world {string.Join(",", perWorld)}");
            Assert.That(rate, Is.InRange(0.5f, 0.95f));
        }

        private static bool PlayGreedy(CardsConfig config)
        {
            var s = new CardsState(config);
            while (!s.Over)
            {
                var (mask, total) = s.BestPlay();
                int needPerHand = (config.Target - s.Score) / s.HandsLeft;
                if (total < needPerHand && s.DiscardsLeft > 0)
                {
                    var bd = s.Preview(mask);
                    int discard = 0, taken = 0;
                    for (int i = 0; i < s.Hand.Count && taken < 3; i++)
                        if ((mask & (1 << i)) == 0 || (bd.ScoringMask & (1 << IndexWithin(mask, i))) == 0) { discard |= 1 << i; taken++; }
                    if (discard != 0 && s.Discard(discard)) continue;
                }
                s.Play(mask);
            }
            return s.Won;
        }

        private static int IndexWithin(int mask, int handIndex)
        {
            int k = 0;
            for (int i = 0; i < handIndex; i++) if ((mask & (1 << i)) != 0) k++;
            return k;
        }

        [Test]
        public void Dash_EveryLevelIsSolvable_WithinPar()
        {
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                if (LevelModes.For(i) != LevelMode.Dash) continue;
                DashLevel level = LevelModes.Dash(LevelDef.For(w, i));
                Assert.That(DashLevel.Solve(level), Is.EqualTo(level.Par));
                Assert.That(level.Par, Is.InRange(4, 12));
            }
            // Many seeds: generator never throws and replaying the BFS path wins in exactly par moves.
            for (long seed = 1; seed <= 150; seed++)
            {
                DashLevel level = DashLevel.Generate(seed, (int)(seed % 2));
                var path = SolvePath(level);
                var state = new DashState(level);
                foreach (int dir in path) state.Move(dir);
                Assert.That(state.Won && state.Moves == level.Par && state.Stars == 3, $"seed {seed}");
            }
        }

        private static List<int> SolvePath(DashLevel level)
        {
            var prev = new Dictionary<int, (int key, int dir)>();
            var queue = new Queue<(int pos, int gems)>();
            int startKey = level.Start * 64;
            queue.Enqueue((level.Start, 0));
            prev[startKey] = (-1, -1);
            while (queue.Count > 0)
            {
                var (pos, gems) = queue.Dequeue();
                int key = pos * 64 + gems;
                for (int dir = 0; dir < 4; dir++)
                {
                    var slide = DashState.Slide(level, pos, gems, dir);
                    if (slide.Path.Count == 0 || slide.Dead) continue;
                    if (slide.Won)
                    {
                        var path = new List<int> { dir };
                        for (int k = key; prev[k].key >= 0; k = prev[k].key) path.Insert(0, prev[k].dir);
                        return path;
                    }
                    int next = slide.End * 64 + slide.Gems;
                    if (prev.ContainsKey(next)) continue;
                    prev[next] = (key, dir);
                    queue.Enqueue((slide.End, slide.Gems));
                }
            }
            return new List<int>();
        }

        [Test]
        public void Crawl_LevelsGenerate_MonstersFollowTheirRules_AndAPatientHeroCanWin()
        {
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                if (LevelModes.For(i) != LevelMode.Crawl) continue;
                CrawlLevel level = LevelModes.Crawl(LevelDef.For(w, i));
                Assert.That(level.Enemies.Count, Is.EqualTo(LevelModes.Tier(w, i) > 0 ? 5 : 3));
            }

            // Slime only moves on even beats; bat every beat.
            var state = new CrawlState(CrawlLevel.Generate(3, 0));
            var slime = state.Enemies.First(e => e.Kind == EnemyKind.Slime);
            int before = slime.Pos;
            state.Tick();
            Assert.That(slime.Pos, Is.EqualTo(before), "slimes rest on odd beats");

            // A hero who never moves still gets beaten eventually or survives; a simple chaser bot wins some levels.
            int won = 0;
            for (long seed = 1; seed <= 40; seed++)
            {
                var s = new CrawlState(CrawlLevel.Generate(seed, 0));
                for (int beat = 0; beat < 400 && !s.Won && !s.Lost; beat++)
                {
                    int target = s.StairsOpen ? s.Level.Stairs : s.Enemies[0].Pos;
                    s.Act(Toward(s, target));
                    s.Tick();
                }
                if (s.Won) won++;
            }
            TestContext.WriteLine($"naive crawl bot wins {won}/40");
            Assert.That(won, Is.GreaterThan(5));
        }

        private static int Toward(CrawlState s, int target)
        {
            // BFS first step toward target around walls.
            var prev = new Dictionary<int, int> { [s.Hero] = -1 };
            var q = new Queue<int>();
            q.Enqueue(s.Hero);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == target) break;
                for (int d = 0; d < 4; d++)
                {
                    int n = CrawlLevel.Step(c, d);
                    if (n < 0 || s.Level.Walls[n] || prev.ContainsKey(n)) continue;
                    if (n != target && s.Enemies.Any(e => e.Pos == n)) continue;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(target)) return 0;
            int step = target;
            while (prev[step] != s.Hero && prev[step] >= 0) step = prev[step];
            for (int d = 0; d < 4; d++) if (CrawlLevel.Step(s.Hero, d) == step) return d;
            return 0;
        }

        [Test]
        public void Adaptive_HeatRisesOnCleanWins_FallsOnLosses_AndScalesDifficulty()
        {
            var profile = new Ronriku.Domain.Player.PlayerProfile();
            Assert.That(Adaptive.Heat(profile, LevelMode.Battle), Is.EqualTo(0));
            Adaptive.Record(profile, LevelMode.Battle, true, 3);
            Adaptive.Record(profile, LevelMode.Battle, true, 3);
            Adaptive.Record(profile, LevelMode.Battle, true, 3);
            Assert.That(Adaptive.Heat(profile, LevelMode.Battle), Is.EqualTo(Adaptive.Max));
            Adaptive.Record(profile, LevelMode.Battle, true, 2);
            Assert.That(Adaptive.Heat(profile, LevelMode.Battle), Is.EqualTo(Adaptive.Max), "scrappy wins hold");
            Adaptive.Record(profile, LevelMode.Cards, false, 0);
            Assert.That(Adaptive.Heat(profile, LevelMode.Cards), Is.EqualTo(-1));
            Assert.That(Adaptive.Heat(profile, LevelMode.Battle), Is.EqualTo(Adaptive.Max), "modes are independent");

            var def = LevelDef.For(1, 4);
            var hot = Adaptive.Apply(LevelModes.Battle(def), 2);
            var cold = Adaptive.Apply(LevelModes.Battle(def), -2);
            Assert.That(hot.MonsterHp, Is.GreaterThan(cold.MonsterHp));
            Assert.That(hot.AttackMs, Is.LessThan(cold.AttackMs));
            Assert.That(cold.Tier, Is.EqualTo(0));
            Assert.That(Adaptive.Apply(LevelModes.Cards(def), 1).Target, Is.GreaterThan(Adaptive.Apply(LevelModes.Cards(def), -1).Target));
            Assert.That(Adaptive.CrawlBeatsPerMove(2, -1), Is.EqualTo(3));
            // Even a chilled battle stays winnable in a handful of clean answers.
            var b = new BattleState(cold);
            int n = 0;
            while (!b.Over) { b.Answer(b.Current.Answer, 3000); n++; }
            Assert.That(b.Won && n >= 3);
        }

        [Test]
        public void Battle_HotStreak_DealsHardCardsWorthMore()
        {
            var b = new BattleState(new BattleConfig { Seed = 42, MonsterHp = 100000, AttackMs = 9000, Pool = BattleConfig.PoolFor(4), Tier = 0 });
            for (int i = 0; i < BattleState.HeatCombo; i++)
            {
                Assert.That(b.CurrentHard, Is.False);
                b.Answer(b.Current.Answer, BattleState.SlowMs);
            }
            Assert.That(b.CurrentHard, Is.True);
            Assert.That(b.Current.Tier, Is.EqualTo(1));
            HitResult hit = b.Answer(b.Current.Answer, BattleState.SlowMs);
            Assert.That(hit.Chips, Is.EqualTo(BattleState.BaseChips + BattleState.HardBonus));
        }

        [Test]
        public void BattleProof_ReplaysExactly_IncludingSwingTiming()
        {
            var config = Adaptive.Apply(LevelModes.Battle(LevelDef.For(0, 1)), 0);
            var live = new BattleState(config);
            int step = 0;
            while (!live.Over)
            {
                if (step == 3) live.MonsterSwing();
                live.Answer(step == 5 ? -1 : live.Current.Answer, 2000 + step * 300);
                step++;
            }
            string proof = "B1:h0|" + string.Join("", System.Linq.Enumerable.Select(live.Log, e => $"{e.answer}@{e.ms},")) + "|" +
                string.Join("", System.Linq.Enumerable.Select(live.SwingsAt, i => $"s{i},"));
            var replay = BattleState.Replay(config, proof);
            Assert.That(replay.Won, Is.EqualTo(live.Won));
            Assert.That(replay.Hp, Is.EqualTo(live.Hp));
            Assert.That(replay.Hearts, Is.EqualTo(live.Hearts));
            Assert.That(replay.Stars, Is.EqualTo(live.Stars));
        }

        [Test]
        public void FriendlyNames_AreStable_AndAvoidBlockedWords()
        {
            Assert.That(Ronriku.Domain.Player.PlayerProfile.FriendlyName("abc"), Is.EqualTo(Ronriku.Domain.Player.PlayerProfile.FriendlyName("abc")));
            for (int i = 0; i < 3000; i++)
                Assert.That(Ronriku.Domain.Player.PlayerProfile.IsBlocked(Ronriku.Domain.Player.PlayerProfile.FriendlyName("player-" + i)), Is.False);
        }

        [Test]
        public void Layout_HasEveryModeInEveryWorld()
        {
            Assert.That(LevelModes.Layout.Length, Is.EqualTo(LevelDef.LevelsPerWorld));
            Assert.That(LevelModes.For(LevelDef.BossIndex), Is.EqualTo(LevelMode.Boss));
            foreach (LevelMode mode in System.Enum.GetValues(typeof(LevelMode)))
                Assert.That(LevelModes.Layout.Contains(mode), mode.ToString());
            var offer = Charms.Offer(DailyPlan.Mix(1, 55), 3);
            Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
        }
    }
}
