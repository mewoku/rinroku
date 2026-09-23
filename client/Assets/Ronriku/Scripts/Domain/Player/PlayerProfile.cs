using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;

namespace Ronriku.Domain.Player
{
    [Serializable]
    public sealed class SkillEntry
    {
        public string name;
        public int rating;
    }

    [Serializable]
    public sealed class DailyRecord
    {
        public int day;
        public string challengeId;
        public int elapsedMs;
        public int solved;
        public int points;
        public int ratingBefore;
        public int ratingAfter;
        public int streak;
    }

    /// <summary>
    /// Persistent local profile. Public fields keep it serialisable by any JSON serializer.
    /// Bump <see cref="CurrentSchemaVersion"/> and extend <see cref="Migrate"/> for every shape change.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;
        public const int HistoryLimit = 60;
        public const int XpPerLevel = 1000;

        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string displayName;
        public int rating = ReasoningRating.Initial;
        public int completedDailies;
        public int xp;
        public int streak;
        public int bestStreak;
        public int lastCompletedDay;
        public List<SkillEntry> skills = new List<SkillEntry>();
        public List<DailyRecord> history = new List<DailyRecord>();

        public int Level => 1 + xp / XpPerLevel;

        public bool HasCompleted(int day) => completedDailies > 0 && lastCompletedDay >= day;

        /// <summary>Streak as it should be shown today: a missed day breaks it even before the next completion.</summary>
        public int DisplayStreak(int today) =>
            completedDailies > 0 && lastCompletedDay >= today - 1 ? streak : 0;

        public int Skill(string name)
        {
            foreach (var entry in skills) if (entry.name == name) return entry.rating;
            return ReasoningRating.Initial;
        }

        public void SetSkill(string name, int value)
        {
            foreach (var entry in skills)
            {
                if (entry.name != name) continue;
                entry.rating = value;
                return;
            }
            skills.Add(new SkillEntry { name = name, rating = value });
        }

        public static PlayerProfile CreateNew(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player id required.", nameof(playerId));
            var profile = new PlayerProfile
            {
                playerId = playerId,
                displayName = "PLAYER " + playerId.Substring(0, Math.Min(4, playerId.Length)).ToUpperInvariant()
            };
            profile.Migrate();
            return profile;
        }

        /// <summary>Brings a deserialised profile to the current schema. Returns false if it cannot be used.</summary>
        public bool Migrate()
        {
            if (schemaVersion <= 0 || schemaVersion > CurrentSchemaVersion || string.IsNullOrWhiteSpace(playerId))
                return false;
            skills ??= new List<SkillEntry>();
            history ??= new List<DailyRecord>();
            foreach (string name in SkillDimensions.All)
            {
                bool present = false;
                foreach (var entry in skills) present |= entry.name == name;
                if (!present) skills.Add(new SkillEntry { name = name, rating = ReasoningRating.Initial });
            }
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "PLAYER";
            rating = Math.Max(ReasoningRating.Minimum, Math.Min(ReasoningRating.Maximum, rating));
            schemaVersion = CurrentSchemaVersion;
            return true;
        }
    }

    public readonly struct SkillChange
    {
        public readonly string Name;
        public readonly int Before;
        public readonly int After;

        public SkillChange(string name, int before, int after)
        {
            Name = name;
            Before = before;
            After = after;
        }

        public int Delta => After - Before;
    }

    public sealed class DailyResult
    {
        public int Day;
        public string ChallengeId;
        public int ElapsedMilliseconds;
        public int Solved;
        public int Trials;
        public int Points;
        public int RatingBefore;
        public int RatingAfter;
        public int StreakAfter;
        public bool Counted;
        public IReadOnlyList<TrialOutcome> Outcomes;
        public IReadOnlyList<SkillChange> Skills;

        public int RatingDelta => RatingAfter - RatingBefore;
    }

    /// <summary>
    /// Applies a finished Daily to the profile. Only the first completion of a UTC day, and only a
    /// day later than the last counted one, changes rating, skills, XP and streak; replays are practice.
    /// </summary>
    public static class DailyCompletion
    {
        public static DailyResult Apply(PlayerProfile profile, DailySession session)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (session == null || !session.IsComplete) throw new ArgumentException("Session is not complete.");

            IReadOnlyList<TrialOutcome> outcomes = session.Outcomes;
            int day = session.Plan.Day;
            var result = new DailyResult
            {
                Day = day,
                ChallengeId = session.Plan.ChallengeId,
                Trials = outcomes.Count,
                Outcomes = outcomes,
                RatingBefore = profile.rating
            };
            foreach (var o in outcomes)
            {
                result.ElapsedMilliseconds += o.ElapsedMilliseconds;
                result.Points += o.Points;
                if (o.Solved) result.Solved++;
            }

            bool counts = profile.completedDailies == 0 || day > profile.lastCompletedDay;
            if (!counts)
            {
                result.RatingAfter = profile.rating;
                result.StreakAfter = profile.DisplayStreak(day);
                result.Skills = Array.Empty<SkillChange>();
                return result;
            }

            int sessions = profile.completedDailies;
            profile.rating = ReasoningRating.UpdateOverall(profile.rating, sessions, outcomes);

            var skills = new List<SkillChange>();
            foreach (var pair in ReasoningRating.DimensionObservations(outcomes))
            {
                int before = profile.Skill(pair.Key);
                int after = ReasoningRating.Update(before, sessions, pair.Value);
                profile.SetSkill(pair.Key, after);
                skills.Add(new SkillChange(pair.Key, before, after));
            }
            skills.Sort((a, b) => Array.IndexOf(SkillDimensions.All, a.Name).CompareTo(Array.IndexOf(SkillDimensions.All, b.Name)));

            profile.streak = profile.completedDailies > 0 && profile.lastCompletedDay == day - 1 ? profile.streak + 1 : 1;
            profile.bestStreak = Math.Max(profile.bestStreak, profile.streak);
            profile.lastCompletedDay = day;
            profile.completedDailies++;
            profile.xp += result.Points / 10;
            profile.history.Add(new DailyRecord
            {
                day = day,
                challengeId = session.Plan.ChallengeId,
                elapsedMs = result.ElapsedMilliseconds,
                solved = result.Solved,
                points = result.Points,
                ratingBefore = result.RatingBefore,
                ratingAfter = profile.rating,
                streak = profile.streak
            });
            if (profile.history.Count > PlayerProfile.HistoryLimit)
                profile.history.RemoveRange(0, profile.history.Count - PlayerProfile.HistoryLimit);

            result.Counted = true;
            result.RatingAfter = profile.rating;
            result.StreakAfter = profile.streak;
            result.Skills = skills;
            return result;
        }
    }
}
