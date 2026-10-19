using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShuYu
{
    public enum MusicTrack { Title, Map, Combat, Boss }

    /// <summary>
    /// 背景音乐播放器：标题 / 地图 / 普通战斗 / BOSS 四轨，全部循环。
    /// 标题 ↔ 地图是同长变奏：切换时保持播放时间点无缝衔接（不淡化）；切战斗 / 回标题走交叉淡化。
    /// 单例挂在 GameManager 上。
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        AudioSource _a, _b;
        AudioSource _active, _inactive;
        readonly Dictionary<MusicTrack, AudioClip> _clips = new Dictionary<MusicTrack, AudioClip>();
        MusicTrack _current = MusicTrack.Title;
        Coroutine _fade;

        void Awake()
        {
            Instance = this;
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.playOnAwake = false;
                s.loop = true;
            }
            _active = _a;
            _inactive = _b;

            // 确保有 AudioListener（SfxPlayer 也会补，这里双保险）
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            LoadClips();
        }

        void Start()
        {
            Play(MusicTrack.Title, 0f);   // 开局播标题音乐
        }

        void LoadClips()
        {
            _clips[MusicTrack.Title] = Resources.Load<AudioClip>("Audio/title");
            _clips[MusicTrack.Map] = Resources.Load<AudioClip>("Audio/map");
            _clips[MusicTrack.Combat] = Resources.Load<AudioClip>("Audio/combat");
            _clips[MusicTrack.Boss] = Resources.Load<AudioClip>("Audio/bosscombat");
        }

        /// <summary>切换 BGM。标题↔地图保持时间点无缝；其余从 0 交叉淡化。fadeDur 用于非无缝切换。</summary>
        public void Play(MusicTrack track, float fadeDur = 0.5f)
        {
            if (!_clips.TryGetValue(track, out var clip) || clip == null) return;
            if (_active.isPlaying && _current == track) return;   // 已经在播这条

            bool seamless = IsSeamless(_current, track);
            float t = seamless ? _active.time : 0f;

            if (seamless)
            {
                // 无缝：同长变奏，直接在同一时间点切过去，不淡化（音量连续）
                float v = AudioSettings.Master * AudioSettings.Music;
                _inactive.clip = clip;
                _inactive.time = t;
                _inactive.volume = v;
                _inactive.Play();
                _active.volume = 0f;
                _active.Stop();
                Swap();
            }
            else
            {
                _inactive.clip = clip;
                _inactive.time = 0f;
                _inactive.volume = 0f;
                _inactive.Play();
                if (_fade != null) StopCoroutine(_fade);
                _fade = StartCoroutine(Crossfade(_inactive, _active, fadeDur));
                Swap();
            }
            _current = track;
        }

        void Swap()
        {
            var tmp = _active; _active = _inactive; _inactive = tmp;
        }

        static bool IsSeamless(MusicTrack a, MusicTrack b)
        {
            if (a == b) return false;
            bool aAmbient = a == MusicTrack.Title || a == MusicTrack.Map;
            bool bAmbient = b == MusicTrack.Title || b == MusicTrack.Map;
            return aAmbient && bAmbient;
        }

        IEnumerator Crossfade(AudioSource incoming, AudioSource outgoing, float dur)
        {
            float target = AudioSettings.Master * AudioSettings.Music;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                incoming.volume = target * k;
                outgoing.volume = target * (1f - k);
                yield return null;
            }
            incoming.volume = target;
            outgoing.volume = 0f;
            outgoing.Stop();
        }

        /// <summary>按 AudioSettings 刷新音量（主音量 × 音乐音量）。</summary>
        public void ApplyVolume()
        {
            float v = AudioSettings.Master * AudioSettings.Music;
            _active.volume = v;
        }
    }
}
