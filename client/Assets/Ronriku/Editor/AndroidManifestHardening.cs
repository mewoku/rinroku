using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Ronriku.Editor
{
    /// <summary>
    /// Unity 6.6's player throws "UnityFoldingFeaturesWrapper.init() should be called only once" when
    /// its activity is recreated inside a live process. Unity already declares most config changes;
    /// this adds the ones it misses so the system never recreates the activity for them.
    /// </summary>
    internal sealed class AndroidManifestHardening : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly string[] ExtraConfigChanges = { "colorMode", "fontWeightAdjustment", "grammaticalGender" };

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var document = new XmlDocument();
            document.Load(manifestPath);
            var ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("android", AndroidNs);

            int patched = 0;
            foreach (XmlElement activity in document.SelectNodes("/manifest/application/activity", ns))
            {
                string current = activity.GetAttribute("configChanges", AndroidNs);
                if (string.IsNullOrEmpty(current)) continue;
                string updated = current;
                foreach (string change in ExtraConfigChanges)
                    if (!("|" + updated + "|").Contains("|" + change + "|")) updated += "|" + change;
                if (updated == current) continue;
                activity.SetAttribute("configChanges", AndroidNs, updated);
                patched++;
            }

            if (patched == 0) return;
            document.Save(manifestPath);
            Debug.Log($"RONRIKU manifest: extended configChanges on {patched} activity element(s).");
        }
    }
}
