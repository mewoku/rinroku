using UnityEngine;

namespace Ronriku.Presentation.Components
{
    public static class RonrikuTheme
    {
        public static readonly Color Graphite = Hex("28292F");
        public static readonly Color NearBlack = Hex("17181C");
        public static readonly Color Black = Hex("090A0C");
        public static readonly Color BlueGrey = Hex("536971");
        public static readonly Color DeepBlue = Hex("135B73");
        public static readonly Color Teal = Hex("11C5B3");
        public static readonly Color Yellow = Hex("E8DA37");
        public static readonly Color OffWhite = Hex("E7E8E5");
        public static readonly Color Muted = Hex("A7B2B5");

        private static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color : Color.magenta;
    }
}

