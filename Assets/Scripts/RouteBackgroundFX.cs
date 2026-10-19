using System.Collections;
using UnityEngine;
using TMPro;

namespace ShuYu
{
    /// <summary>路线背景动画：漂浮的数学符号/数字缓慢上移并淡入淡出（极低透明度，衬托「数字」主题）。</summary>
    public class RouteBackgroundFX : MonoBehaviour
    {
        static readonly string[] Symbols = { "+", "-", "×", "÷", "=", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        static readonly System.Random _rng = new System.Random();

        void Start()
        {
            for (int i = 0; i < 12; i++)
                StartCoroutine(Loop(i * 0.9f));
        }

        IEnumerator Loop(float delay)
        {
            yield return new WaitForSeconds(delay);
            while (true)
                yield return Drift();
        }

        IEnumerator Drift()
        {
            string sym = Symbols[_rng.Next(Symbols.Length)];
            float fontSize = 36 + _rng.Next(32);
            var t = UIFactory.CreateText("BgNum", transform, sym, fontSize, new Color(1f, 1f, 1f, 0.06f));
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = new Vector2((float)_rng.NextDouble() * 1920f, -60f - (float)_rng.NextDouble() * 400f);
            rt.sizeDelta = new Vector2(140f, 80f);

            float speed = 28f + (float)_rng.NextDouble() * 40f;      // 上移速度
            float life = 7f + (float)_rng.NextDouble() * 6f;         // 存活时长
            float t0 = 0f;
            while (t0 < life)
            {
                t0 += Time.deltaTime;
                rt.anchoredPosition += new Vector2(0f, speed * Time.deltaTime);
                float alpha = Mathf.Sin(Mathf.Clamp01(t0 / life) * Mathf.PI) * 0.07f;   // 淡入淡出
                t.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            Destroy(t.gameObject);
        }
    }
}
