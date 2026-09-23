using System.Collections.Generic;
using NUnit.Framework;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Tests
{
    public sealed class SpatialPuzzleTests
    {
        private const int SeedCount = 500;

        [Test]
        public void Orientations_FormClosedGroupOfTwentyFour()
        {
            var seen = new HashSet<string>();
            for (int o = 0; o < CubeOrientations.Count; o++)
            {
                Assert.That(seen.Add(string.Join(",", CubeOrientations.Matrix(o))), Is.True, $"duplicate matrix {o}");
                for (int m = 0; m < CubeOrientations.MoveCount; m++)
                {
                    var move = (SpatialMove)m;
                    int next = CubeOrientations.Apply(o, move);
                    Assert.That(next, Is.InRange(0, CubeOrientations.Count - 1));
                    Assert.That(CubeOrientations.Apply(next, CubeOrientations.Inverse(move)), Is.EqualTo(o));
                }
            }
        }

        [Test]
        public void Rotation_KeepsBoxCellsInsideBox()
        {
            for (int o = 0; o < CubeOrientations.Count; o++)
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            for (int z = 0; z < 3; z++)
            {
                GridPoint p = CubeOrientations.Rotate(o, new GridPoint(x, y, z));
                Assert.That(p.X, Is.InRange(0, 2));
                Assert.That(p.Y, Is.InRange(0, 2));
                Assert.That(p.Z, Is.InRange(0, 2));
            }
        }

        [Test]
        public void Generator_IsDeterministic_AcrossSeedsAndDifficulties()
        {
            var generator = new SpatialPuzzleGenerator();
            foreach (PuzzleDifficulty difficulty in System.Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= SeedCount; seed++)
            {
                var first = generator.Generate(seed, difficulty, seed * 31);
                var second = generator.Generate(seed, difficulty, seed * 31);
                Assert.That(second.Metadata.ContentHash, Is.EqualTo(first.Metadata.ContentHash));
                Assert.That(second.Cubes, Is.EqualTo(first.Cubes));
                Assert.That(second.TargetShadow, Is.EqualTo(first.TargetShadow));
                Assert.That(second.StartOrientation, Is.EqualTo(first.StartOrientation));
            }
        }

        [Test]
        public void EveryGeneratedPuzzle_SatisfiesInvariants()
        {
            var generator = new SpatialPuzzleGenerator();
            foreach (PuzzleDifficulty difficulty in System.Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= SeedCount; seed++)
            {
                var data = generator.Generate(seed, difficulty, seed * 47);
                string violation = SpatialPuzzleInvariants.Check(data);
                Assert.That(violation, Is.Null, $"seed={seed}, difficulty={difficulty}: {violation}");
            }
        }

        [Test]
        public void Generator_ProducesVariedContent()
        {
            var generator = new SpatialPuzzleGenerator();
            var hashes = new HashSet<string>();
            var targets = new HashSet<int>();
            for (long seed = 1; seed <= SeedCount; seed++)
            {
                var data = generator.Generate(seed, PuzzleDifficulty.Standard, 0);
                hashes.Add(data.Metadata.ContentHash);
                targets.Add(data.TargetShadow);
            }
            Assert.That(hashes.Count, Is.EqualTo(SeedCount));
            Assert.That(targets.Count, Is.GreaterThan(20));
        }

        [Test]
        public void Validator_ReplaysMoves_AndRejectsBadInput()
        {
            var data = new SpatialPuzzleGenerator().Generate(7, PuzzleDifficulty.Hard, 0);
            var validator = new SpatialPuzzleValidator();
            var solution = new List<SpatialMove>(SpatialPuzzleSolver.Solve(data));

            Assert.That(validator.IsCorrect(data, solution), Is.True);
            Assert.That(validator.IsCorrect(data, new List<SpatialMove>()), Is.False);
            Assert.That(validator.IsCorrect(data, null), Is.False);
            Assert.That(validator.IsCorrect(data, new List<SpatialMove> { (SpatialMove)9 }), Is.False);

            var padded = new List<SpatialMove>(solution);
            padded.Add(SpatialMove.TurnLeft);
            padded.Add(SpatialMove.TurnRight);
            Assert.That(validator.IsCorrect(data, padded), Is.True, "net-zero detour still solves");

            var tooLong = new List<SpatialMove>();
            for (int i = 0; i < SpatialPuzzleValidator.MaxMoves + 1; i++) tooLong.Add(SpatialMove.TurnLeft);
            Assert.That(validator.IsCorrect(data, tooLong), Is.False);
        }

        [Test]
        public void Scorer_IsDeterministic_AndRewardsPar()
        {
            var data = new SpatialPuzzleGenerator().Generate(5, PuzzleDifficulty.Standard, 9);
            var scorer = new SpatialPuzzleScorer();
            var atPar = scorer.Calculate(data, true, 12000, data.Par, 0);
            var overPar = scorer.Calculate(data, true, 12000, data.Par + 4, 0);
            var reset = scorer.Calculate(data, true, 12000, data.Par, 1);

            Assert.That(atPar.AtPar, Is.True);
            Assert.That(overPar.AtPar, Is.False);
            Assert.That(atPar.Points, Is.GreaterThan(overPar.Points));
            Assert.That(atPar.Points, Is.GreaterThan(reset.Points));
            Assert.That(scorer.Calculate(data, false, 1000, 1, 0).Points, Is.Zero);
            Assert.That(scorer.Calculate(data, true, 12000, data.Par, 0).Points, Is.EqualTo(atPar.Points));
        }
    }
}
