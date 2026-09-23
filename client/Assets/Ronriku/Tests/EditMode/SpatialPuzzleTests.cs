using NUnit.Framework;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Tests
{
    public sealed class SpatialPuzzleTests
    {
        [Test]
        public void Generator_IsDeterministicAcrossFiveHundredSeeds()
        {
            var generator = new SpatialPuzzleGenerator();
            for (long seed = 1; seed <= 500; seed++)
            {
                var first = generator.Generate(seed, PuzzleDifficulty.Hard, seed * 31);
                var second = generator.Generate(seed, PuzzleDifficulty.Hard, seed * 31);
                Assert.That(second.Metadata.ContentHash, Is.EqualTo(first.Metadata.ContentHash));
                Assert.That(second.TargetOrientation, Is.EqualTo(first.TargetOrientation));
            }
        }

        [Test]
        public void EveryGeneratedPuzzle_HasExactlyOneNonTrivialAnswer()
        {
            var generator = new SpatialPuzzleGenerator();
            var validator = new SpatialPuzzleValidator();
            foreach (PuzzleDifficulty difficulty in System.Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= 500; seed++)
            {
                var data = generator.Generate(seed, difficulty, seed * 47);
                int accepted = 0;
                for (int answer = 0; answer < 4; answer++) if (validator.IsCorrect(data, answer)) accepted++;
                Assert.That(accepted, Is.EqualTo(1), $"seed={seed}, difficulty={difficulty}");
                Assert.That(data.StartOrientation, Is.Not.EqualTo(data.TargetOrientation));
            }
        }

        [Test]
        public void VariantSeed_ChangesPresentationButPreservesLogicalStructure()
        {
            var generator = new SpatialPuzzleGenerator();
            var first = generator.Generate(42, PuzzleDifficulty.Hard, 1);
            var second = generator.Generate(42, PuzzleDifficulty.Hard, 2);
            Assert.That(second.Cubes, Is.EqualTo(first.Cubes));
            Assert.That(second.Metadata.Seed, Is.EqualTo(first.Metadata.Seed));
            Assert.That(second.Metadata.VariantSeed, Is.Not.EqualTo(first.Metadata.VariantSeed));
        }

        [Test]
        public void Scorer_IsDeterministicAndRewardsEfficiency()
        {
            var data = new SpatialPuzzleGenerator().Generate(5, PuzzleDifficulty.Standard, 9);
            var scorer = new SpatialPuzzleScorer();
            var efficient = scorer.Calculate(data, true, 12000, 1, 0);
            var inefficient = scorer.Calculate(data, true, 45000, 7, 1);
            Assert.That(efficient.Points, Is.GreaterThan(inefficient.Points));
            Assert.That(scorer.Calculate(data, false, 1000, 1, 0).Points, Is.Zero);
            Assert.That(scorer.Calculate(data, true, 12000, 1, 0).Points, Is.EqualTo(efficient.Points));
        }
    }
}
