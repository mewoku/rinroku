using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ronriku.Domain.Figures
{
    public enum FigureRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    /// <summary>
    /// A voxel person: Size (x) × Size (y, front = 0) × 2·Size (z, up). Voxel values 0 = empty,
    /// 1–15 = index into <see cref="Palette"/> (RGB, 15 entries).
    /// </summary>
    public sealed class Figure
    {
        public const int PaletteSize = 15;

        public int Size { get; }
        public int Height => Size * 2;
        public uint[] Palette { get; }
        public byte[] Voxels { get; }
        public string Name { get; }
        public FigureRarity Rarity { get; }
        public FigureTraits Traits { get; }

        public Figure(int size, uint[] palette, byte[] voxels, string name, FigureRarity rarity, FigureTraits traits)
        {
            if (size < 3 || size > 5) throw new ArgumentOutOfRangeException(nameof(size));
            if (palette == null || palette.Length != PaletteSize) throw new ArgumentException("Palette must have 15 colours.");
            if (voxels == null || voxels.Length != size * size * size * 2) throw new ArgumentException("Voxel count mismatch.");
            Size = size;
            Palette = palette;
            Voxels = voxels;
            Name = name ?? string.Empty;
            Rarity = rarity;
            Traits = traits;
        }

        public int Index(int x, int y, int z) => x + Size * (y + Size * z);
        public byte Get(int x, int y, int z) => Voxels[Index(x, y, z)];

        public int FilledCount
        {
            get
            {
                int count = 0;
                foreach (byte v in Voxels) if (v != 0) count++;
                return count;
            }
        }

        /// <summary><c>RF1.{S}.{palette 90 hex}.{base64url nibble-packed voxels}</c>. See docs/PLAN_V2.md §5.</summary>
        public string Encode()
        {
            var sb = new StringBuilder("RF1.");
            sb.Append(Size).Append('.');
            foreach (uint rgb in Palette) sb.Append((rgb & 0xFFFFFF).ToString("X6"));
            sb.Append('.');
            var packed = new byte[(Voxels.Length + 1) / 2];
            for (int i = 0; i < Voxels.Length; i++)
            {
                int nibble = Voxels[i] & 0xF;
                if (i % 2 == 0) packed[i / 2] |= (byte)(nibble << 4);
                else packed[i / 2] |= (byte)nibble;
            }
            sb.Append(Convert.ToBase64String(packed).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
            return sb.ToString();
        }

        /// <summary>Decodes geometry and palette. Name, rarity and traits are not part of the encoding.</summary>
        public static Figure Decode(string encoding)
        {
            string[] parts = (encoding ?? string.Empty).Split('.');
            if (parts.Length != 4 || parts[0] != "RF1") throw new FormatException("Not an RF1 figure.");
            int size = int.Parse(parts[1]);
            if (size < 3 || size > 5 || parts[2].Length != PaletteSize * 6) throw new FormatException("Bad figure header.");
            var palette = new uint[PaletteSize];
            for (int i = 0; i < PaletteSize; i++) palette[i] = Convert.ToUInt32(parts[2].Substring(i * 6, 6), 16);
            string b64 = parts[3].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            byte[] packed = Convert.FromBase64String(b64);
            var voxels = new byte[size * size * size * 2];
            if (packed.Length != (voxels.Length + 1) / 2) throw new FormatException("Voxel payload length mismatch.");
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = (byte)(i % 2 == 0 ? packed[i / 2] >> 4 : packed[i / 2] & 0xF);
            return new Figure(size, palette, voxels, string.Empty, FigureRarity.Common, default);
        }

        /// <summary>MagicaVoxel .vox (version 150): SIZE, XYZI and RGBA chunks. Palette index n maps to vox colour n.</summary>
        public byte[] ToVox()
        {
            var voxels = new List<byte[]>();
            for (int z = 0; z < Height; z++)
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                byte v = Get(x, y, z);
                // MagicaVoxel's y axis points away from the viewer; our y = 0 is the front.
                if (v != 0) voxels.Add(new[] { (byte)x, (byte)(Size - 1 - y), (byte)z, v });
            }

            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            byte[] Chunk(string id, byte[] content)
            {
                using var m = new MemoryStream();
                using var cw = new BinaryWriter(m);
                cw.Write(Encoding.ASCII.GetBytes(id));
                cw.Write(content.Length);
                cw.Write(0);
                cw.Write(content);
                return m.ToArray();
            }

            byte[] size;
            using (var m = new MemoryStream())
            using (var cw = new BinaryWriter(m)) { cw.Write(Size); cw.Write(Size); cw.Write(Height); size = m.ToArray(); }
            byte[] xyzi;
            using (var m = new MemoryStream())
            using (var cw = new BinaryWriter(m))
            {
                cw.Write(voxels.Count);
                foreach (byte[] v in voxels) cw.Write(v);
                xyzi = m.ToArray();
            }
            var rgba = new byte[256 * 4];
            for (int i = 0; i < 255; i++)
            {
                uint rgb = i < PaletteSize ? Palette[i] : 0x808080u;
                rgba[i * 4] = (byte)(rgb >> 16);
                rgba[i * 4 + 1] = (byte)(rgb >> 8);
                rgba[i * 4 + 2] = (byte)rgb;
                rgba[i * 4 + 3] = 255;
            }

            byte[] children;
            using (var m = new MemoryStream())
            {
                foreach (byte[] c in new[] { Chunk("SIZE", size), Chunk("XYZI", xyzi), Chunk("RGBA", rgba) }) m.Write(c, 0, c.Length);
                children = m.ToArray();
            }
            w.Write(Encoding.ASCII.GetBytes("VOX "));
            w.Write(150);
            w.Write(Encoding.ASCII.GetBytes("MAIN"));
            w.Write(0);
            w.Write(children.Length);
            w.Write(children);
            return stream.ToArray();
        }
    }

    public readonly struct FigureTraits
    {
        public readonly int Skin, Outfit, Accent, Hair, Pants, Shoes, Headwear, Eyes, OutfitStyle, Accessory;
        public readonly bool Glow;

        public FigureTraits(int skin, int outfit, int accent, int hair, int pants, int shoes, int headwear, int eyes,
            int outfitStyle, int accessory, bool glow)
        {
            Skin = skin; Outfit = outfit; Accent = accent; Hair = hair; Pants = pants; Shoes = shoes;
            Headwear = headwear; Eyes = eyes; OutfitStyle = outfitStyle; Accessory = accessory; Glow = glow;
        }
    }
}
