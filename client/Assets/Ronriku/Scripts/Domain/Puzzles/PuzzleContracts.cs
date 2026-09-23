using System;

namespace Ronriku.Domain.Puzzles
{
    public enum PuzzleDifficulty
    {
        Easy = 1,
        Standard = 2,
        Hard = 3
    }

    [Serializable]
    public sealed class PuzzleMetadata
    {
        public string Id { get; }
        public int Version { get; }
        public string Type { get; }
        public long Seed { get; }
        public long VariantSeed { get; }
        public PuzzleDifficulty Difficulty { get; }
        public int RulesVersion { get; }
        public string Goal { get; }
        public int TimeLimitSeconds { get; }
        public string[] SkillDimensions { get; }
        public string ContentHash { get; }

        public PuzzleMetadata(string id, int version, string type, long seed, long variantSeed,
            PuzzleDifficulty difficulty, int rulesVersion, string goal, int timeLimitSeconds,
            string[] skillDimensions, string contentHash)
        {
            Id = id;
            Version = version;
            Type = type;
            Seed = seed;
            VariantSeed = variantSeed;
            Difficulty = difficulty;
            RulesVersion = rulesVersion;
            Goal = goal;
            TimeLimitSeconds = timeLimitSeconds;
            SkillDimensions = skillDimensions;
            ContentHash = contentHash;
        }
    }

    public interface IPuzzleGenerator<out TData>
    {
        TData Generate(long seed, PuzzleDifficulty difficulty, long variantSeed);
    }

    public interface IPuzzleValidator<in TData, in TAnswer>
    {
        bool IsCorrect(TData data, TAnswer answer);
    }
}

