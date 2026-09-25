using UnityEngine;

namespace Ronriku.Presentation
{
    /// <summary>
    /// Designer knobs for game feel, audio and arcade timing, editable in the Inspector
    /// (Assets/Ronriku/Resources/RonrikuTuning.asset, also linked from the RONRIKU object in the
    /// Bootstrap scene). Only presentation-side values live here: puzzle generation, rewards and
    /// anything the server checks stay in code so results remain reproducible.
    /// </summary>
    [CreateAssetMenu(menuName = "RONRIKU/Tuning", fileName = "RonrikuTuning")]
    public sealed class RonrikuTuning : ScriptableObject
    {
        [Header("Feel")]
        [Tooltip("Multiplier for every screen shake. 0 = no shake.")]
        [Range(0f, 2f)] public float shake = 1f;
        [Tooltip("Multiplier for floating damage / score numbers.")]
        [Range(0.5f, 2f)] public float popupScale = 1f;

        [Header("Audio")]
        [Tooltip("Sound-effect loudness (music has its own slider in the Me tab).")]
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Battle")]
        [Tooltip("How far a hero hit pushes back the monster's swing timer (ms).")]
        [Range(0, 6000)] public int staggerMs = 2500;
        [Tooltip("Pause after a right answer before the next card (ms).")]
        [Range(200, 2000)] public int resolveRightMs = 620;
        [Tooltip("Pause after a wrong answer before the next card (ms).")]
        [Range(300, 3000)] public int resolveWrongMs = 1150;

        [Header("Beat Crawl")]
        [Tooltip("Music beats per hero move. 2 = relaxed, 1 = frantic.")]
        [Range(1, 4)] public int musicBeatsPerMove = 2;
        [Tooltip("How close to the beat (fraction of a move) counts as ON BEAT.")]
        [Range(0.1f, 0.5f)] public float beatWindow = 0.28f;
        [Tooltip("Seconds per move when music is off.")]
        [Range(0.4f, 1.5f)] public float fallbackSecondsPerMove = 0.8f;

        private static RonrikuTuning _current;

        /// <summary>The active tuning: the scene-linked asset, else Resources/RonrikuTuning, else defaults.</summary>
        public static RonrikuTuning Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<RonrikuTuning>("RonrikuTuning");
                if (_current == null) _current = CreateInstance<RonrikuTuning>();
                return _current;
            }
            set => _current = value;
        }
    }
}
