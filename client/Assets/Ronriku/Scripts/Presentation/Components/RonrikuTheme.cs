using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>A world/trial colour scheme: accent pair for glows and gradients, ambient hue for the background.</summary>
    public readonly struct Palette
    {
        public readonly string Name;
        public readonly Color Accent;
        public readonly Color Accent2;
        public readonly Color Ambient;

        public Palette(string name, Color accent, Color accent2, Color ambient)
        {
            Name = name;
            Accent = accent;
            Accent2 = accent2;
            Ambient = ambient;
        }
    }

    /// <summary>
    /// "Pixel Ambient" design tokens (docs/PLAN_V2.md §2). Sizes are in dp: the panel is scaled so a
    /// 1080-px-wide phone is 432 dp wide.
    /// </summary>
    public static class RonrikuTheme
    {
        // Base.
        public static readonly Color Background = Hex("07080B");
        public static readonly Color Background2 = Hex("0E1016");
        public static readonly Color Surface = Hex("14161D");
        public static readonly Color Surface2 = Hex("1B1E27");
        public static readonly Color Line = Hex("2A2F3B");
        public static readonly Color Text = Hex("E7E8E5");
        public static readonly Color Muted = Hex("8A94A6");

        // Brand (kept from v1) and legacy names used by puzzle renderers.
        public static readonly Color Teal = Hex("11C5B3");
        public static readonly Color Yellow = Hex("E8DA37");
        public static readonly Color DeepBlue = Hex("135B73");
        public static readonly Color BlueGrey = Hex("536971");
        public static readonly Color Graphite = Hex("14161D");
        public static readonly Color NearBlack = Hex("0E1016");
        public static readonly Color Black = Hex("07080B");
        public static readonly Color OffWhite = Text;
        public static readonly Color Red = Hex("FF3B5C");
        public static readonly Color Gold = Hex("FFC83D");

        // World palettes.
        public static readonly Palette Lab = new Palette("LAB", Hex("11C5B3"), Hex("135B73"), Hex("0B3B4A"));
        public static readonly Palette Pattern = new Palette("PATTERN", Hex("FF4FD8"), Hex("7B5CFF"), Hex("2A1450"));
        public static readonly Palette Link = new Palette("LINK", Hex("FFB347"), Hex("FF6B5A"), Hex("4A1E12"));
        public static readonly Palette Forest = new Palette("FOREST", Hex("9BE35A"), Hex("2F8F4E"), Hex("10301A"));
        public static readonly Palette Frost = new Palette("FROST", Hex("8FE3FF"), Hex("3A6BFF"), Hex("0C2250"));
        public static readonly Palette Boss = new Palette("BOSS", Hex("FF3B5C"), Hex("FFC83D"), Hex("3A0A14"));
        public static readonly Palette[] Worlds = { Lab, Pattern, Link, Forest, Frost };

        public static readonly Color Common = Hex("A7B2B5");
        public static readonly Color Rare = Hex("3A9BFF");
        public static readonly Color Epic = Hex("B45CFF");
        public static readonly Color Legendary = Hex("FFC83D");

        // Spacing (dp).
        public const float Unit = 8f;
        public const float Gutter = 16f;
        public const float TouchTarget = 48f;

        private static FontAsset _display, _displayBold, _body;

        public static FontDefinition Display => Font(ref _display, "Fonts/Silkscreen");
        public static FontDefinition DisplayBold => Font(ref _displayBold, "Fonts/SilkscreenBold");
        public static FontDefinition Body => Font(ref _body, "Fonts/PixelifySans");

        private static FontDefinition Font(ref FontAsset cache, string path)
        {
            if (cache == null) cache = Resources.Load<FontAsset>(path);
            return cache != null ? FontDefinition.FromSDFFont(cache) : default;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color : Color.magenta;
    }
}
