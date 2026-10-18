using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>标题界面背景数学动效：漂浮数字 + X 轴上的流动正弦波 + 数域大字右上角的旋转黄金螺旋（波与螺旋都是点状）。</summary>
    public class TitleMathFX : MonoBehaviour
    {
        RectTransform _root;

        // 漂浮数字
        class Particle
        {
            public RectTransform rt;
            public float x, y, speed, swayAmp, swaySpeed, phase;
        }
        readonly List<Particle> _numbers = new List<Particle>();

        // 正弦波（点）
        readonly List<RectTransform> _curveDots = new List<RectTransform>();
        float _curvePhase;

        // 黄金螺旋（点）
        readonly List<RectTransform> _spiralDots = new List<RectTransform>();
        float _spiralAngle;

        // 右侧布局区（避开左侧菜单 x<560）
        const float Left = 560f;
        const float Right = 1900f;
        const float Top = 1000f;
        const float Bottom = 60f;

        void Awake()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("TitleMathFX", canvas.transform);
            UIFactory.Stretch(_root);

            BuildNumbers();
            BuildCurve();
            BuildSpiral();
        }

        public void SetVisible(bool v)
        {
            if (_root != null) _root.gameObject.SetActive(v);
        }

        void BuildNumbers()
        {
            string[] glyphs = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "+", "−", "×", "÷", "=" };
            for (int i = 0; i < 20; i++)
            {
                var t = UIFactory.CreateText("Num" + i, _root, glyphs[i % glyphs.Length], 40, GameConfig.SubText);
                Color c = t.color; c.a = 0.2f + Random.value * 0.3f; t.color = c;

                var p = new Particle();
                p.rt = t.rectTransform;
                p.x = Left + Random.value * (Right - Left);
                p.y = Bottom + Random.value * (Top - Bottom);
                p.speed = 15f + Random.value * 40f;
                p.swayAmp = 20f + Random.value * 40f;
                p.swaySpeed = 0.3f + Random.value * 0.7f;
                p.phase = Random.value * Mathf.PI * 2f;
                _numbers.Add(p);
                SetAbs(p.rt, p.x, p.y);
            }
        }

        void BuildCurve()
        {
            for (int i = 0; i < 60; i++)
                _curveDots.Add(MakeDot("Curve" + i, GameConfig.Accent, 0.5f));
        }

        void BuildSpiral()
        {
            for (int i = 0; i < 100; i++)
                _spiralDots.Add(MakeDot("Spiral" + i, GameConfig.Accent, 0.4f));
        }

        RectTransform MakeDot(string name, Color color, float alpha)
        {
            var img = UIFactory.CreatePanel(name, _root, color);
            img.raycastTarget = false;
            Color c = img.color; c.a = alpha; img.color = c;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(3f, 3f);
            return rt;
        }

        void Update()
        {
            UpdateNumbers();
            UpdateCurve();
            UpdateSpiral();
        }

        void UpdateNumbers()
        {
            float dt = Time.deltaTime;
            foreach (var p in _numbers)
            {
                p.y += p.speed * dt;
                if (p.y > Top + 60f) { p.y = Bottom - 60f; p.x = Left + Random.value * (Right - Left); }
                float sway = Mathf.Sin(Time.time * p.swaySpeed + p.phase) * p.swayAmp;
                SetAbs(p.rt, p.x + sway, p.y);
            }
        }

        void UpdateCurve()
        {
            _curvePhase += Time.deltaTime * 1.5f;
            float baseY = 150f;   // X 轴
            float amp = 80f;
            float step = (Right - Left) / (_curveDots.Count - 1);
            for (int i = 0; i < _curveDots.Count; i++)
            {
                float x = Left + i * step;
                float y = baseY + Mathf.Sin(x * 0.02f + _curvePhase) * amp;
                _curveDots[i].anchoredPosition = new Vector2(x, y);
            }
        }

        void UpdateSpiral()
        {
            _spiralAngle += Time.deltaTime * 0.4f;
            float cx = 720f, cy = 760f;   // 数域大字右上角
            const float phi = 1.618f;
            const float a = 12f;
            for (int i = 0; i < _spiralDots.Count; i++)
            {
                float theta = i / (float)_spiralDots.Count * Mathf.PI * 3f;
                float r = a * Mathf.Pow(phi, theta / (Mathf.PI * 0.5f));
                float ang = theta + _spiralAngle;
                _spiralDots[i].anchoredPosition = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
        }

        static void SetAbs(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
