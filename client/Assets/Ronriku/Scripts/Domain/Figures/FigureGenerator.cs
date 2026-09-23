using System;

namespace Ronriku.Domain.Figures
{
    /// <summary>
    /// Procedural voxel people (generator version 1). Deterministic in (seed, size, kind); the TypeScript
    /// port must follow this file exactly: same tables, same RNG call order, same build order.
    ///
    /// RNG: new DeterministicRandom(seed * 31 + size + 1000 * kind)   (unchecked ulong arithmetic)
    /// Call order: skin r(8), outfit r(16), accent r(12), hair r(10), pants r(6), shoes r(3),
    ///             headwear r(6), eyes r(4), outfitStyle r(4), accessory r(5), glowRoll r(100),
    ///             glowColor r(4), name r(16) ×3.
    /// Monsters (kind 1) use the monster skin and outfit tables instead.
    /// </summary>
    public static class FigureGenerator
    {
        public const int Version = 1;

        // Palette slots (voxel values).
        public const byte Skin = 1, SkinShade = 2, Outfit = 3, OutfitShade = 4, Accent = 5, Eye = 6, Hair = 7,
            Hat = 8, Pants = 9, Shoes = 10, Mouth = 11, Glow = 12, Cheek = 13, Metal = 14, White = 15;

        public static readonly uint[] Skins = { 0xFFD9B3, 0xF1C27D, 0xE0AC69, 0xC68642, 0x8D5524, 0x5C3A21, 0x9BE35A, 0x8FE3FF };
        public static readonly uint[] MonsterSkins = { 0x9BE35A, 0x7B5CFF, 0xFF6B5A, 0x3A6BFF, 0xFF4FD8, 0x2F8F4E, 0xFFB347, 0x5B2A86 };
        public static readonly uint[] Outfits =
        {
            0x11C5B3, 0x135B73, 0xE8DA37, 0xFF4FD8, 0x7B5CFF, 0xFFB347, 0xFF6B5A, 0x9BE35A,
            0x2F8F4E, 0x8FE3FF, 0x3A6BFF, 0xFF3B5C, 0xFFC83D, 0xE7E8E5, 0x2A2F3B, 0x5B2A86
        };
        public static readonly uint[] MonsterOutfits =
        {
            0x2A2F3B, 0x17181C, 0x3A0A14, 0x10301A, 0x0C2250, 0x2A1450, 0x4A1E12, 0x135B73,
            0x5B2A86, 0x2F8F4E, 0xFF3B5C, 0x7B5CFF, 0x1B1E27, 0x3B2A20, 0x0B3B4A, 0x8D5524
        };
        public static readonly uint[] Accents =
            { 0xE8DA37, 0x11C5B3, 0xFF4FD8, 0xFFB347, 0x8FE3FF, 0xFF3B5C, 0x9BE35A, 0xFFFFFF, 0xFFC83D, 0x7B5CFF, 0x3A6BFF, 0x17181C };
        public static readonly uint[] Hairs =
            { 0x2B1B12, 0x5A3825, 0xA8672E, 0xE8C07D, 0xF4F1E8, 0x17181C, 0xFF4FD8, 0x3A6BFF, 0x9BE35A, 0xFF6B5A };
        public static readonly uint[] PantsColours = { 0x2A2F3B, 0x1B1E27, 0x135B73, 0x3B2A20, 0x5B2A86, 0x17181C };
        public static readonly uint[] ShoeColours = { 0x17181C, 0xE7E8E5, 0x8D5524 };
        public static readonly uint[] GlowColours = { 0x8FE3FF, 0xFF4FD8, 0x9BE35A, 0xFFC83D };
        public static readonly string[] Syllables =
            { "KA", "ZU", "MI", "RO", "NI", "TA", "SU", "KE", "YO", "RI", "HA", "MO", "NE", "TO", "LU", "VI" };

        private static readonly int[] HeadwearPoints = { 0, 0, 1, 1, 3, 2 };
        private static readonly int[] EyePoints = { 0, 0, 2, 1 };
        private static readonly int[] OutfitStylePoints = { 0, 0, 0, 1 };
        private static readonly int[] AccessoryPoints = { 0, 1, 1, 1, 0 };

        public static Figure Generate(ulong seed, int size, bool monster = false)
        {
            if (size < 3 || size > 5) throw new ArgumentOutOfRangeException(nameof(size));
            int kind = monster ? 1 : 0;
            var r = new DeterministicRandom(unchecked(seed * 31UL + (ulong)size + 1000UL * (ulong)kind));

            int skin = r.NextInt(8), outfit = r.NextInt(16), accent = r.NextInt(12), hair = r.NextInt(10);
            int pants = r.NextInt(6), shoes = r.NextInt(3), headwear = r.NextInt(6), eyes = r.NextInt(4);
            int outfitStyle = r.NextInt(4), accessory = r.NextInt(5);
            bool glow = r.NextInt(100) < 6;
            int glowColour = r.NextInt(4);
            string name = Syllables[r.NextInt(16)] + Syllables[r.NextInt(16)] + Syllables[r.NextInt(16)];

            uint skinRgb = (monster ? MonsterSkins : Skins)[skin];
            uint outfitRgb = (monster ? MonsterOutfits : Outfits)[outfit];
            var palette = new uint[Figure.PaletteSize];
            palette[Skin - 1] = skinRgb;
            palette[SkinShade - 1] = Shade(skinRgb);
            palette[Outfit - 1] = outfitRgb;
            palette[OutfitShade - 1] = Shade(outfitRgb);
            palette[Accent - 1] = Accents[accent];
            palette[Eye - 1] = glow ? GlowColours[glowColour] : 0x17181C;
            palette[Hair - 1] = Hairs[hair];
            palette[Hat - 1] = Accents[(accent + 5) % Accents.Length];
            palette[Pants - 1] = PantsColours[pants];
            palette[Shoes - 1] = ShoeColours[shoes];
            palette[Mouth - 1] = 0x8D3B3B;
            palette[Glow - 1] = GlowColours[glowColour];
            palette[Cheek - 1] = 0xFF8A8A;
            palette[Metal - 1] = 0xFFC83D;
            palette[White - 1] = 0xE7E8E5;

            int s = size, h = size * 2;
            var v = new byte[s * s * h];
            int Idx(int x, int y, int z) => x + s * (y + s * z);
            void Set(int x, int y, int z, byte value) => v[Idx(x, y, z)] = value;
            bool Corner(int x, int y) => (x == 0 || x == s - 1) && (y == 0 || y == s - 1);

            int legsH = s - 2, headStart = 2 * s - 3, top = h - 1;

            // Legs.
            int legW = s >= 5 ? 2 : 1;
            int yFrom = s == 3 ? 1 : 1, yTo = s == 3 ? 1 : s - 2;
            for (int z = 0; z < legsH; z++)
            for (int y = yFrom; y <= yTo; y++)
            for (int x = 0; x < s; x++)
                if (x < legW || x >= s - legW) Set(x, y, z, z == 0 ? Shoes : Pants);

            // Torso with sleeves and hands.
            for (int z = legsH; z < headStart; z++)
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (s >= 4 && Corner(x, y)) continue;
                bool side = x == 0 || x == s - 1;
                Set(x, y, z, side ? (z == legsH ? Skin : OutfitShade) : Outfit);
            }

            // Head.
            for (int z = headStart; z < h; z++)
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (s >= 4 && z == top && Corner(x, y)) continue;
                Set(x, y, z, Skin);
            }

            // Face.
            int eyeL = s == 3 ? 0 : 1, eyeR = s - 1 - eyeL, eyeZ = headStart + 1;
            switch (eyes)
            {
                case 0:
                    Set(eyeL, 0, eyeZ, Eye); Set(eyeR, 0, eyeZ, Eye);
                    break;
                case 1:
                    Set(eyeL, 0, eyeZ, Eye); Set(eyeR, 0, eyeZ, Eye);
                    Set(eyeL, 0, eyeZ + 1, Eye); Set(eyeR, 0, eyeZ + 1, Eye);
                    break;
                case 2:
                    for (int x = 0; x < s; x++) Set(x, 0, eyeZ, Glow);
                    break;
                default:
                    Set(eyeL, 0, eyeZ, Eye); Set(eyeR, 0, eyeZ, Eye);
                    Set(eyeL, 0, headStart, Cheek); Set(eyeR, 0, headStart, Cheek);
                    break;
            }
            if (s >= 4 && eyes != 2) Set(s / 2, 0, headStart, Mouth);

            // Headwear.
            switch (headwear)
            {
                case 1:
                case 2:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, y, top)] != 0) Set(x, y, top, Hair);
                    for (int z = headStart; z < h; z++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, s - 1, z)] != 0) Set(x, s - 1, z, Hair);
                    if (headwear == 2)
                        for (int z = headStart + 1; z < h; z++)
                        for (int y = 1; y < s; y++)
                        {
                            if (v[Idx(0, y, z)] != 0) Set(0, y, z, Hair);
                            if (v[Idx(s - 1, y, z)] != 0) Set(s - 1, y, z, Hair);
                        }
                    break;
                case 3:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, y, top)] != 0) Set(x, y, top, y == 0 ? Outfit : Hat);
                    break;
                case 4:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        if (v[Idx(x, y, top)] == 0) continue;
                        bool rim = x == 0 || y == 0 || x == s - 1 || y == s - 1;
                        Set(x, y, top, rim ? Metal : Hair);
                    }
                    break;
                case 5:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, y, top)] != 0) Set(x, y, top, Hair);
                    Set(s / 2, s / 2, top, Glow);
                    break;
            }

            // Outfit detail.
            int torsoH = headStart - legsH;
            switch (outfitStyle)
            {
                case 1:
                    for (int x = 1; x < s - 1; x++) Set(x, 0, legsH + torsoH / 2, Accent);
                    break;
                case 2:
                    for (int z = legsH; z < headStart; z++) Set(s / 2, 0, z, Accent);
                    break;
                case 3:
                    Set(s / 2, 0, headStart - 2 >= legsH ? headStart - 2 : legsH, Metal);
                    break;
            }

            // Accessory.
            switch (accessory)
            {
                case 1:
                    for (int z = legsH; z < headStart; z++)
                    for (int x = 1; x < s - 1; x++) Set(x, s - 1, z, Accent);
                    break;
                case 2:
                    for (int z = legsH; z < headStart; z++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, s - 1, z)] != 0) Set(x, s - 1, z, Hat);
                    break;
                case 3:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, y, legsH)] != 0 && (x == 0 || y == 0 || x == s - 1 || y == s - 1)) Set(x, y, legsH, Metal);
                    break;
                case 4:
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        if (v[Idx(x, y, headStart - 1)] != 0 && (x == 0 || y == 0 || x == s - 1 || y == s - 1)) Set(x, y, headStart - 1, Accent);
                    break;
            }

            int points = HeadwearPoints[headwear] + EyePoints[eyes] + OutfitStylePoints[outfitStyle] +
                         AccessoryPoints[accessory] + (glow ? 3 : 0);
            FigureRarity rarity = points >= 6 ? FigureRarity.Legendary
                : points >= 4 ? FigureRarity.Epic
                : points >= 2 ? FigureRarity.Rare
                : FigureRarity.Common;

            var traits = new FigureTraits(skin, outfit, accent, hair, pants, shoes, headwear, eyes, outfitStyle, accessory, glow);
            return new Figure(size, palette, v, name, rarity, traits);
        }

        /// <summary>Each channel × 3/4 (integer).</summary>
        public static uint Shade(uint rgb)
        {
            uint r = ((rgb >> 16) & 0xFF) * 3 / 4, g = ((rgb >> 8) & 0xFF) * 3 / 4, b = (rgb & 0xFF) * 3 / 4;
            return (r << 16) | (g << 8) | b;
        }
    }
}
