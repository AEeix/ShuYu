using UnityEngine;

namespace ShuYu
{
    /// <summary>音频设置：主音量 / 音乐 / 音效（0~1），持久化到 PlayerPrefs，改动后自动刷新两个播放器。</summary>
    public static class AudioSettings
    {
        const string KMaster = "audio.master";
        const string KMusic = "audio.music";
        const string KSfx = "audio.sfx";

        public static float Master { get { return PlayerPrefs.GetFloat(KMaster, 1f); } set { PlayerPrefs.SetFloat(KMaster, Mathf.Clamp01(value)); Refresh(); } }
        public static float Music { get { return PlayerPrefs.GetFloat(KMusic, 1f); } set { PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); Refresh(); } }
        public static float Sfx { get { return PlayerPrefs.GetFloat(KSfx, 1f); } set { PlayerPrefs.SetFloat(KSfx, Mathf.Clamp01(value)); Refresh(); } }

        /// <summary>改完任一音量后刷新音乐与音效播放器的实际音量。</summary>
        public static void Refresh()
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.ApplyVolume();
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.ApplyVolume();
        }
    }
}
