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

        // Atlases stay Dynamic and empty in git: Unity 6 advanced text rejects Static font assets and
        // clears dynamic glyph data after play mode, so pre-populating them would only churn.

        [MenuItem("RONRIKU/Setup Fonts")]
        public static void Setup()
        {
            Directory.CreateDirectory(TargetDir);
            Create("Silkscreen-Regular.ttf", "Silkscreen");
            Create("Silkscreen-Bold.ttf", "SilkscreenBold");
            Create("PixelifySans.ttf", "PixelifySans");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("RONRIKU_FONTS_OK");
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
