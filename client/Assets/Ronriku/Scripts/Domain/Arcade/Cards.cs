using System;
using System.Collections.Generic;
using System.Linq;
using Ronriku.Domain.Daily;

namespace Ronriku.Domain.Arcade
{
    public enum Suit { Fire = 0, Wave = 1, Leaf = 2, Bolt = 3 }

    public readonly struct Card : IEquatable<Card>
    {
        public const int Values = 9;
        public readonly int Value;
        public readonly Suit Suit;

        public Card(int value, Suit suit)
        {
            Value = value;
            Suit = suit;
        }

        public int Id => (int)Suit * Values + Value - 1;
        public static Card FromId(int id) => new Card(id % Values + 1, (Suit)(id / Values));
        public bool Equals(Card other) => Value == other.Value && Suit == other.Suit;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => $"{Value}{Suit.ToString()[0]}";
    }

    public enum Combo { High, Pair, TwoPair, Three, Straight, Flush, FullHouse, Four, StraightFlush }

    public static class HandEvaluator
    {
        public static (int chips, int mult) Base(Combo combo) => combo switch
        {
            Combo.High => (5, 1),
            Combo.Pair => (10, 2),
            Combo.TwoPair => (20, 2),
            Combo.Three => (30, 3),
            Combo.Straight => (30, 4),
            Combo.Flush => (35, 4),
            Combo.FullHouse => (40, 4),
            Combo.Four => (60, 7),
            _ => (100, 8)
        };

        public static string Name(Combo combo) => combo switch
        {
            Combo.High => "HIGH RUNE",
            Combo.Pair => "PAIR",
            Combo.TwoPair => "TWO PAIR",
            Combo.Three => "THREE",
            Combo.Straight => "LADDER",
            Combo.Flush => "SAME SIGN",
            Combo.FullHouse => "FULL HOUSE",
            Combo.Four => "FOUR",
            _ => "SIGN LADDER"
        };

        /// <summary>Best combo of 1–5 played cards and which of them score (bit i = played[i]).</summary>
        public static (Combo combo, int scoring) Evaluate(IReadOnlyList<Card> played)
        {
            int n = played.Count;
            if (n == 0) throw new ArgumentException("no cards");
            int all = (1 << n) - 1;
            bool flush = n == 5 && played.All(c => c.Suit == played[0].Suit);
            bool straight = false;
            if (n == 5)
            {
                var values = played.Select(c => c.Value).OrderBy(v => v).ToArray();
                straight = values.Distinct().Count() == 5 && values[4] - values[0] == 4;
            }
            if (straight && flush) return (Combo.StraightFlush, all);

            var groups = played.Select((c, i) => (c.Value, i)).GroupBy(p => p.Value)
                .Select(g => (value: g.Key, mask: g.Aggregate(0, (m, p) => m | (1 << p.i)), count: g.Count()))
                .OrderByDescending(g => g.count).ThenByDescending(g => g.value).ToList();

            if (groups[0].count == 4) return (Combo.Four, groups[0].mask);
            if (groups[0].count == 3 && groups.Count > 1 && groups[1].count >= 2) return (Combo.FullHouse, groups[0].mask | groups[1].mask);
            if (flush) return (Combo.Flush, all);
            if (straight) return (Combo.Straight, all);
            if (groups[0].count == 3) return (Combo.Three, groups[0].mask);
            if (groups[0].count == 2 && groups.Count > 1 && groups[1].count == 2) return (Combo.TwoPair, groups[0].mask | groups[1].mask);
            if (groups[0].count == 2) return (Combo.Pair, groups[0].mask);
            int high = 0;
            for (int i = 1; i < n; i++) if (played[i].Value > played[high].Value) high = i;
            return (Combo.High, 1 << high);
        }
    }

    /// <summary>Charms bend the scoring rules (the jokers of a Balatro run). Picked before a card fight.</summary>
    public enum CharmId
    {
        EmberHeart, Tide, Sprout, Storm, Twins, Ladder, Painter, Miser, Oddball, EvenKeel, LastStand, Hoarder
    }

    public static class Charms
    {
        public static readonly CharmId[] All = (CharmId[])Enum.GetValues(typeof(CharmId));

        public static string Name(CharmId id) => id switch
        {
            CharmId.EmberHeart => "EMBER HEART",
            CharmId.Tide => "TIDE STONE",
            CharmId.Sprout => "SPROUT",
            CharmId.Storm => "STORM EYE",
            CharmId.Twins => "TWINS",
            CharmId.Ladder => "LADDER",
            CharmId.Painter => "PAINTER",
            CharmId.Miser => "MISER",
            CharmId.Oddball => "ODDBALL",
            CharmId.EvenKeel => "EVEN KEEL",
            CharmId.LastStand => "LAST STAND",
            _ => "HOARDER"
        };

        public static string Describe(CharmId id) => id switch
        {
            CharmId.EmberHeart => "+2 MULT FOR EACH FIRE RUNE",
            CharmId.Tide => "+20 CHIPS FOR EACH WAVE RUNE",
            CharmId.Sprout => "+3 MULT IF A LEAF RUNE SCORES",
            CharmId.Storm => "+2 MULT FOR EACH BOLT RUNE",
            CharmId.Twins => "+4 MULT ON PAIRS AND HOUSES",
            CharmId.Ladder => "+40 CHIPS +4 MULT ON LADDERS",
            CharmId.Painter => "X3 MULT ON SAME SIGN",
            CharmId.Miser => "+1 MULT PER DISCARD LEFT",
            CharmId.Oddball => "+1 MULT PER ODD RUNE",
            CharmId.EvenKeel => "+12 CHIPS PER EVEN RUNE",
            CharmId.LastStand => "X3 MULT ON YOUR LAST HAND",
            _ => "+1 CARD IN HAND"
        };

        /// <summary>Deterministic offer of distinct charms.</summary>
        public static CharmId[] Offer(long seed, int count)
        {
            var rng = new DeterministicRandom(unchecked((ulong)seed));
            var pool = new List<CharmId>(All);
            var offer = new CharmId[count];
            for (int i = 0; i < count; i++)
            {
                int k = rng.NextInt(pool.Count);
                offer[i] = pool[k];
                pool.RemoveAt(k);
            }
            return offer;
        }
    }

    public sealed class ScoreStep
    {
        public int CardIndex = -1;   // index into the played cards, or -1 for charm/base steps
        public CharmId? Charm;
        public int Chips;
        public int Mult;
        public int Times = 1;        // multiplicative step
        public string Text;
    }

    public sealed class ScoreBreakdown
    {
        public Combo Combo;
        public int ScoringMask;
        public readonly List<ScoreStep> Steps = new List<ScoreStep>();
        public int Chips;
        public int Mult;
        public int Total => Chips * Mult;
    }

    public sealed class CardsConfig
    {
        public long Seed;
        public int Target;
        public int Hands = 4;
        public int Discards = 3;
        public int HandSize = 7;
        public CharmId[] Charms = Array.Empty<CharmId>();
    }

    /// <summary>
    /// Rune Hand: a 36-card deck (1–9 × four signs). Play up to five cards as a combo, score
    /// (chips + rune values) × mult, beat the monster's HP within four hands. Pure and replayable from
    /// (seed, charms, actions).
    /// </summary>
    public sealed class CardsState
    {
        public const int MaxPlay = 5;

        private readonly List<Card> _deck = new List<Card>();
        private readonly List<Card> _hand = new List<Card>();
        private readonly List<string> _log = new List<string>();
        private int _draw;

        public CardsConfig Config { get; }
        public int Score { get; private set; }
        public int HandsLeft { get; private set; }
        public int DiscardsLeft { get; private set; }
        public int HandSize { get; }
        public IReadOnlyList<Card> Hand => _hand;
        public int DeckLeft => _deck.Count - _draw;
        public IReadOnlyList<string> Log => _log;
        public bool Won => Score >= Config.Target;
        public bool Lost => !Won && HandsLeft <= 0;
        public bool Over => Won || Lost;

        public CardsState(CardsConfig config)
        {
            Config = config;
            HandsLeft = config.Hands;
            DiscardsLeft = config.Discards;
            HandSize = config.HandSize + (Has(CharmId.Hoarder) ? 1 : 0);
            for (int id = 0; id < 4 * Card.Values; id++) _deck.Add(Card.FromId(id));
            var rng = new DeterministicRandom(unchecked((ulong)DailyPlan.Mix(config.Seed, 77)));
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }
            Refill();
        }

        public bool Has(CharmId charm) => Array.IndexOf(Config.Charms, charm) >= 0;

        private void Refill()
        {
            while (_hand.Count < HandSize && _draw < _deck.Count) _hand.Add(_deck[_draw++]);
            _hand.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Suit.CompareTo(b.Suit));
        }

        private List<Card> Take(int mask)
        {
            var taken = new List<Card>();
            for (int i = 0; i < _hand.Count; i++) if ((mask & (1 << i)) != 0) taken.Add(_hand[i]);
            return taken;
        }

        private static bool ValidMask(int mask, int handCount) =>
            mask != 0 && (mask >> handCount) == 0 && Challenges.PopCount(mask) <= MaxPlay;

        /// <summary>What playing these cards would score, without playing them.</summary>
        public ScoreBreakdown Preview(int mask)
        {
            if (!ValidMask(mask, _hand.Count)) return null;
            return ScoreOf(Take(mask), HandsLeft == 1);
        }

        public ScoreBreakdown Play(int mask)
        {
            if (Over || !ValidMask(mask, _hand.Count)) return null;
            var played = Take(mask);
            var breakdown = ScoreOf(played, HandsLeft == 1);
            _log.Add("P" + mask);
            Score += breakdown.Total;
            HandsLeft--;
            for (int i = _hand.Count - 1; i >= 0; i--) if ((mask & (1 << i)) != 0) _hand.RemoveAt(i);
            Refill();
            return breakdown;
        }

        public bool Discard(int mask)
        {
            if (Over || DiscardsLeft <= 0 || !ValidMask(mask, _hand.Count)) return false;
            _log.Add("D" + mask);
            DiscardsLeft--;
            for (int i = _hand.Count - 1; i >= 0; i--) if ((mask & (1 << i)) != 0) _hand.RemoveAt(i);
            Refill();
            return true;
        }

        private ScoreBreakdown ScoreOf(List<Card> played, bool lastHand)
        {
            var (combo, scoring) = HandEvaluator.Evaluate(played);
            var (chips, mult) = HandEvaluator.Base(combo);
            var b = new ScoreBreakdown { Combo = combo, ScoringMask = scoring };
            b.Steps.Add(new ScoreStep { Chips = chips, Mult = mult, Text = HandEvaluator.Name(combo) });
            bool leafScored = false;
            for (int i = 0; i < played.Count; i++)
            {
                if ((scoring & (1 << i)) == 0) continue;
                Card card = played[i];
                int addChips = card.Value, addMult = 0;
                if (card.Suit == Suit.Fire && Has(CharmId.EmberHeart)) addMult += 2;
                if (card.Suit == Suit.Wave && Has(CharmId.Tide)) addChips += 20;
                if (card.Suit == Suit.Bolt && Has(CharmId.Storm)) addMult += 2;
                if (card.Suit == Suit.Leaf) leafScored = true;
                if (card.Value % 2 == 1 && Has(CharmId.Oddball)) addMult += 1;
                if (card.Value % 2 == 0 && Has(CharmId.EvenKeel)) addChips += 12;
                chips += addChips;
                mult += addMult;
                b.Steps.Add(new ScoreStep { CardIndex = i, Chips = addChips, Mult = addMult });
            }

            void Add(CharmId charm, int c, int m)
            {
                chips += c;
                mult += m;
                b.Steps.Add(new ScoreStep { Charm = charm, Chips = c, Mult = m, Text = Charms.Name(charm) });
            }

            void Times(CharmId charm, int x)
            {
                mult *= x;
                b.Steps.Add(new ScoreStep { Charm = charm, Times = x, Text = Charms.Name(charm) });
            }

            bool pairs = combo == Combo.Pair || combo == Combo.TwoPair || combo == Combo.FullHouse;
            bool ladder = combo == Combo.Straight || combo == Combo.StraightFlush;
            bool sameSign = combo == Combo.Flush || combo == Combo.StraightFlush;
            if (pairs && Has(CharmId.Twins)) Add(CharmId.Twins, 0, 4);
            if (ladder && Has(CharmId.Ladder)) Add(CharmId.Ladder, 40, 4);
            if (Has(CharmId.Miser) && DiscardsLeft > 0) Add(CharmId.Miser, 0, DiscardsLeft);
            if (leafScored && Has(CharmId.Sprout)) Add(CharmId.Sprout, 0, 3);
            if (sameSign && Has(CharmId.Painter)) Times(CharmId.Painter, 3);
            if (lastHand && Has(CharmId.LastStand)) Times(CharmId.LastStand, 3);

            b.Chips = chips;
            b.Mult = Math.Max(1, mult);
            return b;
        }

        /// <summary>Hands left over after the killing blow: 2+ → ★★★, 1 → ★★, 0 → ★.</summary>
        public int Stars => !Won ? 0 : HandsLeft >= 2 ? 3 : HandsLeft == 1 ? 2 : 1;

        /// <summary>Highest-scoring play in the current hand (brute force, ≤ 218 subsets). For hints and bots.</summary>
        public (int mask, int total) BestPlay()
        {
            int best = 0, bestMask = 0;
            for (int mask = 1; mask < 1 << _hand.Count; mask++)
            {
                if (Challenges.PopCount(mask) > MaxPlay) continue;
                int total = ScoreOf(Take(mask), HandsLeft == 1).Total;
                if (total > best) { best = total; bestMask = mask; }
            }
            return (bestMask, best);
        }
    }
}
