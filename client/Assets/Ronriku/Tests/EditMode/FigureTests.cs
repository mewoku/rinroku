using System.Collections.Generic;
using NUnit.Framework;
using Ronriku.Domain.Figures;

namespace Ronriku.Tests
{
    public sealed class FigureTests
    {
        [Test]
        public void Generator_IsDeterministic_AndEncodingRoundTrips()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            for (int size = 3; size <= 5; size++)
            {
                Figure a = FigureGenerator.Generate(seed, size);
                Figure b = FigureGenerator.Generate(seed, size);
                string encoded = a.Encode();
                Assert.That(b.Encode(), Is.EqualTo(encoded));
                Assert.That(a.Name, Is.EqualTo(b.Name));
                Figure decoded = Figure.Decode(encoded);
                Assert.That(decoded.Encode(), Is.EqualTo(encoded), $"seed {seed} size {size}");
                Assert.That(decoded.Voxels, Is.EqualTo(a.Voxels));
            }
        }

        [Test]
        public void Figures_AreVaried_AndEveryRarityAppears()
        {
            var encodings = new HashSet<string>();
            var rarities = new Dictionary<FigureRarity, int>();
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                Figure f = FigureGenerator.Generate(seed, 4);
                encodings.Add(f.Encode());
                rarities[f.Rarity] = rarities.TryGetValue(f.Rarity, out int c) ? c + 1 : 1;
            }
            Assert.That(encodings.Count, Is.GreaterThan(950));
            foreach (FigureRarity r in System.Enum.GetValues(typeof(FigureRarity)))
                Assert.That(rarities.ContainsKey(r), Is.True, $"no {r} in 1000 figures");
            Assert.That(rarities[FigureRarity.Common], Is.GreaterThan(rarities[FigureRarity.Legendary]));
        }

        [Test]
        public void Figures_HaveHeadTorsoAndFeet()
        {
            for (int size = 3; size <= 5; size++)
            {
                Figure f = FigureGenerator.Generate(7, size);
                bool top = false, bottom = false;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    top |= f.Get(x, y, f.Height - 1) != 0;
                    bottom |= f.Get(x, y, 0) != 0;
                }
                Assert.That(top && bottom, Is.True);
                Assert.That(FigureGenerator.Generate(7, size, true).Encode(), Is.Not.EqualTo(f.Encode()), "monster differs");
            }
        }

        [Test]
        public void Vox_HasMagicaVoxelHeaderAndChunks()
        {
            byte[] vox = FigureGenerator.Generate(3, 5).ToVox();
            Assert.That(System.Text.Encoding.ASCII.GetString(vox, 0, 4), Is.EqualTo("VOX "));
            Assert.That(System.BitConverter.ToInt32(vox, 4), Is.EqualTo(150));
            string text = System.Text.Encoding.ASCII.GetString(vox);
            Assert.That(text.Contains("MAIN") && text.Contains("SIZE") && text.Contains("XYZI") && text.Contains("RGBA"), Is.True);
        }

        [Test]
        public void Decode_RejectsGarbage()
        {
            Assert.Throws<System.FormatException>(() => Figure.Decode("RF2.4.xx.yy"));
            Assert.Throws<System.FormatException>(() => Figure.Decode("hello"));
        }
    }
}
