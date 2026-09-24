using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Domain.Player
{
    public static class SkillDimensions
    {
        public const string Pattern = "Pattern";
        public const string Spatial = "Spatial";
        public const string Logic = "Logic";
        public const string Planning = "Planning";
        public const string Memory = "Memory";
        public const string Speed = "Speed";

        public static readonly string[] All = { Pattern, Spatial, Logic, Planning, Memory, Speed };

        public static string Primary(TrialKind kind) => kind switch
        {
            TrialKind.Pattern => Pattern,
            TrialKind.Logic => Logic,
            _ => Spatial
        };
    }

    /// <summary>What happened in one trial. Contains no solution data.</summary>
    public readonly struct TrialOutcome
    {
        public readonly TrialKind Kind;
        public readonly PuzzleDifficulty Difficulty;
        public readonly bool Solved;
        public readonly int ElapsedMilliseconds;
        public readonly int TargetMilliseconds;
        public readonly int Moves;
        public readonly int Par;
        public readonly int Resets;
        public readonly int Points;
        /// <summary>
        /// The player's submitted answer as JSON for server replay: Pattern = option index, Spatial = move
        /// list, Logic = cell path. Never send it to analytics.
        /// </summary>
        public readonly string Answer;

        public TrialOutcome(TrialKind kind, PuzzleDifficulty difficulty, bool solved, int elapsedMilliseconds,
            int targetMilliseconds, int moves, int par, int resets, int points, string answer = null)
        {
            Answer = answer;
            Kind = kind;
            Difficulty = difficulty;
            Solved = solved;
            ElapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            TargetMilliseconds = Math.Max(1, targetMilliseconds);
            Moves = Math.Max(0, moves);
            Par = Math.Max(0, par);
            Resets = Math.Max(0, resets);
            Points = Math.Max(0, points);
        }

        public static TrialOutcome FromSpatial(SpatialPuzzleData data, SpatialAttemptScore score, string answer = null) => new TrialOutcome(
            TrialKind.Spatial, data.Metadata.Difficulty, score.Solved, score.ElapsedMilliseconds,
            SpatialPuzzleScorer.TargetMilliseconds(data.Metadata.Difficulty), score.Moves, score.Par, score.Resets,
            score.Points, answer);
    }

    /// <summary>
    /// Reasoning Rating: a bounded Elo-style update against fixed trial difficulty ratings.
    ///
    /// performance p (0..1) per trial:
    ///   unsolved → 0
    ///   solved   → clamp(0.5 + 0.25·time + 0.25·plan − min(0.2, 0.1·resets))
    ///   time = clamp(1 − elapsed / (2·target)),  plan = par / max(moves, par)
    /// expected E = 1 / (1 + 10^((D − R) / 400)),  D = 900 + 200·difficulty (1100 / 1300 / 1500)
    /// Δ = round(K · mean(p − E)),  K = 40 for the first 10 Dailies, then 24
    /// rating is clamped to [100, 3000]; initial rating 1200.
    ///
    /// Skill dimensions use the same update with their own rating: the trial's primary
    /// dimension uses p, Speed uses the time term, Planning uses the plan term.
    /// </summary>
    public static class ReasoningRating
    {
        public const int Initial = 1200;
        public const int Minimum = 100;
        public const int Maximum = 3000;
        public const int ProvisionalSessions = 10;
        public const int ProvisionalK = 40;
        public const int EstablishedK = 24;

        public static int DifficultyRating(PuzzleDifficulty difficulty) => 900 + 200 * (int)difficulty;

        public static int K(int completedSessions) =>
            completedSessions < ProvisionalSessions ? ProvisionalK : EstablishedK;

        public static double TimeScore(TrialOutcome o) =>
            o.Solved ? Clamp01(1.0 - o.ElapsedMilliseconds / (2.0 * o.TargetMilliseconds)) : 0.0;

        public static double PlanScore(TrialOutcome o) =>
            o.Solved ? (o.Par <= 0 ? 1.0 : o.Par / (double)Math.Max(o.Moves, o.Par)) : 0.0;

        public static double Performance(TrialOutcome o)
        {
            if (!o.Solved) return 0.0;
            double resetPenalty = Math.Min(0.2, 0.1 * o.Resets);
            return Clamp01(0.5 + 0.25 * TimeScore(o) + 0.25 * PlanScore(o) - resetPenalty);
        }

        public static double Expected(int rating, int opponent) =>
            1.0 / (1.0 + Math.Pow(10.0, (opponent - rating) / 400.0));

        /// <summary>Applies Δ for a set of (performance, difficulty) observations to one rating.</summary>
        public static int Update(int rating, int completedSessions,
            IReadOnlyList<(double performance, PuzzleDifficulty difficulty)> observations)
        {
            if (observations == null || observations.Count == 0) return rating;
            double sum = 0.0;
            foreach (var (performance, difficulty) in observations)
                sum += performance - Expected(rating, DifficultyRating(difficulty));
            int delta = (int)Math.Round(K(completedSessions) * sum / observations.Count, MidpointRounding.AwayFromZero);
            return Math.Max(Minimum, Math.Min(Maximum, rating + delta));
        }

        public static int UpdateOverall(int rating, int completedSessions, IReadOnlyList<TrialOutcome> outcomes)
        {
            var observations = new List<(double, PuzzleDifficulty)>(outcomes.Count);
            foreach (var o in outcomes) observations.Add((Performance(o), o.Difficulty));
            return Update(rating, completedSessions, observations);
        }

        /// <summary>Observations per skill dimension produced by a set of outcomes.</summary>
        public static Dictionary<string, List<(double, PuzzleDifficulty)>> DimensionObservations(
            IReadOnlyList<TrialOutcome> outcomes)
        {
            var result = new Dictionary<string, List<(double, PuzzleDifficulty)>>();
            void Add(string name, double value, PuzzleDifficulty difficulty)
            {
                if (!result.TryGetValue(name, out var list)) result[name] = list = new List<(double, PuzzleDifficulty)>();
                list.Add((value, difficulty));
            }
            foreach (var o in outcomes)
            {
                Add(SkillDimensions.Primary(o.Kind), Performance(o), o.Difficulty);
                Add(SkillDimensions.Speed, TimeScore(o), o.Difficulty);
                if (o.Par > 0) Add(SkillDimensions.Planning, PlanScore(o), o.Difficulty);
            }
            return result;
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
