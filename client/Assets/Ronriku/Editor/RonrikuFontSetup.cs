using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Ronriku.Editor
{
    /// <summary>
    /// Creates UI Toolkit font assets for the OFL pixel fonts under Resources so the runtime can load
    /// them with <c>Resources.Load&lt;FontAsset&gt;</c>. Idempotent.
    /// </summary>
    public static class RonrikuFontSetup
    {
        private const string SourceDir = "Assets/Ronriku/Fonts";
        private const string TargetDir = "Assets/Ronriku/Resources/Fonts";

        /// <summary>Every character the UI uses: printable ASCII plus the few symbols in labels.</summary>
        public const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×·→…★";

        [MenuItem("RONRIKU/Setup Fonts")]
        public static void Setup()
        {
            Directory.CreateDirectory(TargetDir);
            Create("Silkscreen-Regular.ttf", "Silkscreen");
            Create("Silkscreen-Bold.ttf", "SilkscreenBold");
            Create("PixelifySans.ttf", "PixelifySans");
            foreach (string name in new[] { "Silkscreen", "SilkscreenBold", "PixelifySans" }) Freeze($"{TargetDir}/{name}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("RONRIKU_FONTS_OK");
        }

        /// <summary>
        /// Pre-populates the charset so play mode never adds glyphs and rewrites the asset (churn in git).
        /// Stays Dynamic: Unity 6's advanced text system refuses Static font assets.
        /// </summary>
        private static void Freeze(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (asset == null) return;
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.TryAddCharacters(Charset, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"RONRIKU fonts: {asset.name} lacks '{missing}' (fallback used)");
            EditorUtility.SetDirty(asset);
        }

        private static void Create(string file, string assetName)
        {
            string target = $"{TargetDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(target) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{SourceDir}/{file}");
            if (font == null) throw new FileNotFoundException($"Font not imported: {SourceDir}/{file}");

            FontAsset asset = FontAsset.CreateFontAsset(font, 64, 8, GlyphRenderMode.SDFAA, 512, 512,
                AtlasPopulationMode.Dynamic, true);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, target);
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                atlas.name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
        }
    }
}
