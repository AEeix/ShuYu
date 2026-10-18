using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>
    /// 格子的火焰特效（纯程序化，零贴图）：在格子底边排一排小火苗，逐帧随机闪烁、上下跳动、橙↔亮黄渐变。
    /// 灼烧层数越多，火苗越多越大——一眼能看出「这格烧得有多旺」。
    /// 由 BattleScreen.CreateCell 在带灼烧的格子上挂载，随每次 Build 重建，自包含无副作用。
    /// </summary>
    public class CellFlame : MonoBehaviour
    {
        const float MinSizeRatio = 0.10f;   // 火苗占格子宽度的最小比例
        const float MaxSizeRatio = 0.22f;

        Image[] _flames;
        RectTransform[] _rects;
        float[] _phase;      // 每簇火苗的随机相位（错开闪烁节奏）
        Vector2[] _baseSize;
        Vector2[] _basePos;
        float _width;
        float _height;

        public void Setup(RectTransform cell, int layers)
        {
            if (cell == null) return;
            _width = cell.rect.width;
            _height = cell.rect.height;

            int count = Mathf.Clamp(2 + layers, 3, 7);            // 层数越多，火苗越多
            float scale = Mathf.Clamp01(0.55f + layers * 0.15f);  // 层数越多，火苗越大

            _flames = new Image[count];
            _rects = new RectTransform[count];
            _phase = new float[count];
            _baseSize = new Vector2[count];
            _basePos = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var img = UIFactory.CreatePanel("Flame", cell, GameConfig.BurnFlame);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);

                float size = _width * Mathf.Lerp(MinSizeRatio, MaxSizeRatio, Random.value) * scale;
                _baseSize[i] = new Vector2(size * 0.8f, size * 1.7f);   // 竖长条 = 火苗
                _basePos[i] = new Vector2(Random.Range(-_width * 0.42f, _width * 0.42f), -_height * 0.04f);
                _phase[i] = Random.value * 10f;

                rt.sizeDelta = _baseSize[i];
                rt.anchoredPosition = _basePos[i];
                _flames[i] = img;
                _rects[i] = rt;
            }
        }

        void Update()
        {
            if (_flames == null) return;
            float t = Time.time;
            for (int i = 0; i < _flames.Length; i++)
            {
                var img = _flames[i];
                if (img == null || _rects[i] == null) continue;

                // 每簇火苗用不同频率 + 相位跳动，避免整排同步显得假
                float k = Mathf.Sin(t * (5.5f + i * 0.9f) + _phase[i]) * 0.5f + 0.5f;
                float flick = Mathf.Sin(t * (13f + i * 2.1f) + _phase[i] * 1.7f) * 0.5f + 0.5f;

                var size = _baseSize[i];
                float h = size.y * (0.75f + 0.5f * k);
                _rects[i].sizeDelta = new Vector2(size.x * (0.9f + 0.2f * flick), h);
                _rects[i].anchoredPosition = new Vector2(
                    _basePos[i].x + Mathf.Sin(t * 2.3f + _phase[i]) * _width * 0.02f,
                    _basePos[i].y + h * 0.15f * k);

                var c = Color.Lerp(GameConfig.BurnFlame, GameConfig.BurnFlameHot, 0.35f + 0.65f * flick);
                c.a = 0.55f + 0.4f * k;
                img.color = c;
            }
        }
    }
}
