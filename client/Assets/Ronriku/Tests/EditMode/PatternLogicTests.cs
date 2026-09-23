using System.Collections.Generic;
using NUnit.Framework;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Tests
{
    public sealed class PatternLogicTests
    {
        [Test]
        public void PatternRules_AreBijective_AndInvertible()
        {
            foreach (PatternRule rule in System.Enum.GetValues(typeof(PatternRule)))
            {
                var outputs = new HashSet<int>();
                for (int grid = 0; grid <= PatternGrid.Full; grid += 7) Assert.That(outputs.Add(PatternGrid.Apply(rule, grid)), Is.True, $"{rule} collides");
            }
            const int sample = 0x1237;
            Assert.That(PatternGrid.Apply(PatternRule.Rotate90, PatternGrid.Apply(PatternRule.Rotate270, sample)), Is.EqualTo(sample));
            Assert.That(PatternGrid.Apply(PatternRule.MirrorX, PatternGrid.Apply(PatternRule.MirrorX, sample)), Is.EqualTo(sample));
            Assert.That(PatternGrid.Apply(PatternRule.Invert, PatternGrid.Apply(PatternRule.Invert, sample)), Is.EqualTo(sample));
            int shifted = sample;
            for (int i = 0; i < PatternGrid.Size; i++) shifted = PatternGrid.Apply(PatternRule.ShiftRight, shifted);
            Assert.That(shifted, Is.EqualTo(sample));
        }

        [Test]
        public void Pattern_DeterministicAndValid_AcrossSeedsAndDifficulties()
        {
            var generator = new PatternPuzzleGenerator();
            var validator = new PatternPuzzleValidator();
            var hashes = new HashSet<string>();
            foreach (PuzzleDifficulty difficulty in System.Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= 300; seed++)
            {
                PatternPuzzleData a = generator.Generate(seed, difficulty, 0);
                PatternPuzzleData b = generator.Generate(seed, difficulty, 0);
                Assert.That(b.Metadata.ContentHash, Is.EqualTo(a.Metadata.ContentHash));
                string violation = PatternPuzzleInvariants.Check(a);
                Assert.That(violation, Is.Null, $"seed={seed} {difficulty}: {violation}");
                int accepted = 0;
                for (int option = -1; option <= a.Options.Count; option++) if (validator.IsCorrect(a, option)) accepted++;
                Assert.That(accepted, Is.EqualTo(1));
                hashes.Add(a.Metadata.ContentHash);
            }
            Assert.That(hashes.Count, Is.EqualTo(900));
        }

        [Test]
        public void Pattern_CorrectOptionPositionIsSpread()
        {
            var counts = new int[PatternPuzzleGenerator.OptionCount];
            var generator = new PatternPuzzleGenerator();
            for (long seed = 1; seed <= 400; seed++) counts[generator.Generate(seed, PuzzleDifficulty.Standard, 0).CorrectOption]++;
            foreach (int count in counts) Assert.That(count, Is.InRange(60, 140));
        }

        [Test]
        public void Logic_DeterministicUniqueAndValid_AcrossSeeds()
        {
            var generator = new LogicPuzzleGenerator();
            foreach (PuzzleDifficulty difficulty in System.Enum.GetValues(typeof(PuzzleDifficulty)))
            {
                int seeds = difficulty == PuzzleDifficulty.Hard ? 40 : 120;
                for (long seed = 1; seed <= seeds; seed++)
                {
                    LogicPuzzleData a = generator.Generate(seed, difficulty, 0);
                    LogicPuzzleData b = generator.Generate(seed, difficulty, 0);
                    Assert.That(b.Metadata.ContentHash, Is.EqualTo(a.Metadata.ContentHash));
                    string violation = LogicPuzzleInvariants.Check(a);
                    Assert.That(violation, Is.Null, $"seed={seed} {difficulty}: {violation}");
                }
            }
        }

        [Test]
        public void LogicValidator_RejectsBrokenPaths()
        {
            LogicPuzzleData data = new LogicPuzzleGenerator().Generate(11, PuzzleDifficulty.Standard, 0);
            var validator = new LogicPuzzleValidator();
            List<int> solution = LogicPuzzleSolver.Solve(data);
            Assert.That(validator.IsCorrect(data, solution), Is.True);

            Assert.That(validator.IsCorrect(data, null), Is.False);
            Assert.That(validator.IsCorrect(data, solution.GetRange(0, solution.Count - 1)), Is.False, "incomplete");

            var reversed = new List<int>(solution);
            reversed.Reverse();
            Assert.That(validator.IsCorrect(data, reversed), Is.False, "numbers out of order");

            var duplicated = new List<int>(solution) { [solution.Count - 1] = solution[0] };
            Assert.That(validator.IsCorrect(data, duplicated), Is.False, "revisits a cell");

            var jumped = new List<int>(solution);
            (jumped[1], jumped[3]) = (jumped[3], jumped[1]);
            Assert.That(validator.IsCorrect(data, jumped), Is.False, "non-adjacent step");
        }

        [Test]
        public void LogicHard_UsesWalls_EasyUsesNumbersOnly()
        {
            var generator = new LogicPuzzleGenerator();
            int hardWalls = 0, easyWalls = 0;
            for (long seed = 1; seed <= 20; seed++)
            {
                hardWalls += generator.Generate(seed, PuzzleDifficulty.Hard, 0).Walls.Count;
                easyWalls += generator.Generate(seed, PuzzleDifficulty.Easy, 0).Walls.Count;
            }
            Assert.That(easyWalls, Is.Zero);
            Assert.That(hardWalls, Is.GreaterThan(20));
        }
    }
}
