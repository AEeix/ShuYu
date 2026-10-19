using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShuYu
{
    public enum SfxType { Click, Hover, Card, Hit, Overflow, Burn, Draw, Win, Lose }

    /// <summary>音效播放器：程序化生成简单音效（无外部素材），单例挂在 GameManager 上。</summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        AudioSource _src;
        readonly Dictionary<SfxType, AudioClip> _clips = new Dictionary<SfxType, AudioClip>();
        static readonly System.Random _rng = new System.Random();

        void Awake()
        {
            Instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            ApplyVolume();

            // 确保有 AudioListener（场景相机没挂，否则听不到声音）
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            Generate();
        }

        public void Play(SfxType type)
        {
            if (_clips.TryGetValue(type, out var clip) && clip != null)
                _src.PlayOneShot(clip);
        }

        /// <summary>按 AudioSettings 刷新音量（主音量 × 音效音量）。</summary>
        public void ApplyVolume()
        {
            if (_src != null) _src.volume = AudioSettings.Master * AudioSettings.Sfx;
        }

        void Generate()
        {
            _clips[SfxType.Click] = Make("click", 0.05f, t => Mathf.Sin(t * 1000f) * (1f - t / 0.05f) * 0.6f);
            _clips[SfxType.Hover] = Make("hover", 0.04f, t => Mathf.Sin(t * 900f) * (1f - t / 0.04f) * 0.35f);
            _clips[SfxType.Card] = Make("card", 0.12f, t => (Noise() * 0.6f + Mathf.Sin(t * 500f) * 0.4f) * (1f - t / 0.12f));
            _clips[SfxType.Hit] = Make("hit", 0.15f, t => (Mathf.Sin(t * 120f) * 0.7f + Noise() * 0.3f) * (1f - t / 0.15f));
            _clips[SfxType.Overflow] = Make("overflow", 0.35f, t => (Mathf.Sin(t * 55f) * 0.8f + Noise() * 0.4f) * Mathf.Pow(1f - t / 0.35f, 2f));
            _clips[SfxType.Burn] = Make("burn", 0.2f, t => Noise() * 0.5f * (1f - t / 0.2f));
            _clips[SfxType.Draw] = Make("draw", 0.06f, t => Mathf.Sin(t * 800f) * (1f - t / 0.06f) * 0.5f);
            _clips[SfxType.Win] = Make("win", 0.6f, t => Mathf.Sin(t * (400f + 300f * t / 0.6f)) * (1f - t / 0.6f) * 0.5f);
            _clips[SfxType.Lose] = Make("lose", 0.5f, t => Mathf.Sin(t * 220f) * (1f - t / 0.5f) * 0.5f);
        }

        static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        AudioClip Make(string name, float duration, Func<float, float> f)
        {
            const int rate = 44100;
            int n = (int)(duration * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Clamp(f(t), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
