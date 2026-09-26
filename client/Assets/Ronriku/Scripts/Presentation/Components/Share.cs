using System.Collections.Generic;
using System.Text;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using UnityEngine;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Wordle-style share: a one-line result with coloured squares per trial. Android opens the system
    /// share sheet; everywhere else the text goes to the clipboard.
    /// </summary>
    public static class Share
    {
        public static string DailyText(DailyResult result, int day, string url)
        {
            var sb = new StringBuilder();
            sb.Append("RONRIKU Daily #").Append(day.ToString("000")).Append(' ');
            IReadOnlyList<TrialOutcome> outcomes = result.Outcomes;
            if (outcomes != null)
                foreach (TrialOutcome o in outcomes)
                    sb.Append(!o.Solved ? "⬛" : o.Kind == TrialKind.Pattern ? "🟪" : o.Kind == TrialKind.Spatial ? "🟦" : "🟧");
            int seconds = result.ElapsedMilliseconds / 1000;
            sb.Append(' ').Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"));
            if (result.Counted) sb.Append(" · ").Append(result.RatingAfter).Append(result.RatingDelta >= 0 ? " (+" : " (").Append(result.RatingDelta).Append(')');
            if (result.StreakAfter > 1) sb.Append(" · 🔥").Append(result.StreakAfter);
            if (!string.IsNullOrEmpty(url)) sb.Append('\n').Append(url);
            return sb.ToString();
        }

        /// <summary>Returns true if a native share sheet opened; false means the text was copied.</summary>
        public static bool Send(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share RONRIKU");
                activity.Call("startActivity", chooser);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("RONRIKU share failed, copying instead: " + e.Message);
            }
#endif
            GUIUtility.systemCopyBuffer = text;
            return false;
        }
    }
}
