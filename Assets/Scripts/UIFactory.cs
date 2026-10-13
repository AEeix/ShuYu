using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>程序化 UI 工厂（零手绘：颜色 + 字体）。</summary>
    public static class UIFactory
    {
        static Canvas _canvas;
        static TMP_FontAsset _font;

        /// <summary>编辑器持久化场景时注入已存盘的字体资产，避免运行时重建。</summary>
        public static void SetFont(TMP_FontAsset font) { _font = font; }

        /// <summary>编辑器持久化场景时注入已存盘的圆角 Sprite。</summary>
        public static void SetRoundedSprite(Sprite sprite) { _rounded = sprite; }

        public static Canvas EnsureCanvas()
        {
            if (_canvas != null) return _canvas;

            // 场景化：场景里已有 Canvas（SceneBuilder 生成）时直接复用，不再新建
            _canvas = Object.FindObjectOfType<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = go.GetComponent<Canvas>();
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                // 必须有 EventSystem，否则按钮无法响应点击（程序化创建 Canvas 时不会自动生成）
                if (Object.FindObjectOfType<EventSystem>() == null)
                {
                    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }
            }

            // CRT 辉光：UI 走相机渲染（camera 模式）才能吃 URP 后处理 Bloom；Overlay 模式不吃后处理
            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.planeDistance = 100f;
            }
            if (_canvas.worldCamera == null)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    var camGo = new GameObject("Main Camera", typeof(Camera));
                    camGo.tag = "MainCamera";
                    cam = camGo.GetComponent<Camera>();
                }
                _canvas.worldCamera = cam;
            }
            return _canvas;
        }

        static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;

                // 主字体：VT323（像素终端，数字/拉丁）
                var mono = Resources.Load<Font>("Fonts/VT323-Regular");
                if (mono != null)
                {
                    _font = TMP_FontAsset.CreateFontAsset(mono);
                    if (_font != null)
                    {
                        _font.name = "ShuYuMono";

                        // 中文 fallback：方舟像素字体（简体）
                        var cjk = Resources.Load<Font>("Fonts/ark-pixel-12px-monospaced-zh_hans");
                        if (cjk != null)
                        {
                            var cjkAsset = TMP_FontAsset.CreateFontAsset(cjk);
                            if (cjkAsset != null)
                            {
                                cjkAsset.name = "ShuYuCJK";
                                _font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { cjkAsset };
                            }
                        }
                    }
                }

                // 兜底：SimHei / 内置字体
                if (_font == null)
                {
                    var f = Resources.Load<Font>("Fonts/SimHei");
                    if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (f != null)
                    {
                        _font = TMP_FontAsset.CreateFontAsset(f);
                        if (_font != null) _font.name = "ShuYuRuntimeFont";
                    }
                }
                return _font;
            }
        }

        /// <summary>圆角矩形 Sprite（九宫格，程序化生成，零手绘）。</summary>
        static Sprite _rounded;
        public const float RoundedRadius = 18f;

        /// <summary>生成圆角矩形纹理（运行时 Sprite 与编辑器持久化共用，避免两处漂移）。</summary>
        public static Texture2D CreateRoundedTexture()
        {
            const int size = 64;
            const int r = 18;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(r - x, x - (size - 1 - r), 0f);
                    float dy = Mathf.Max(r - y, y - (size - 1 - r), 0f);
                    bool inside = (dx * dx + dy * dy) <= (r * r);
                    cols[y * size + x] = inside ? (Color32)Color.white : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(cols);
            tex.Apply();
            return tex;
        }

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                var tex = CreateRoundedTexture();
                float r = RoundedRadius;
                _rounded = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                _rounded.name = "RoundedRect";
                return _rounded;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        public static RectTransform CreateButton(string name, Transform parent, string label, float fontSize, Color bgColor, Color textColor, UnityAction onClick)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bgColor;
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var labelText = CreateText("Label", rt, label, fontSize, textColor);
            Stretch(labelText.rectTransform);
            if (onClick != null) btn.onClick.AddListener(onClick);
            return rt;
        }

        /// <summary>铺满父节点。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>按锚点 + 偏移定位。</summary>
        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>给 UI 元素加描边（CRT 风格）。</summary>
        public static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var o = graphic.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = color;
            o.effectDistance = distance;
        }

        /// <summary>生成一张坐标系纹理（X/Y 轴 + 等距刻度线），像素级绘制，供单个 Image 显示。</summary>
        public static Texture2D CreateAxisTexture(int width, int height, Vector2 origin, float negLen, float tickSpacing, float tickLen, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var cols = new Color32[width * height];
            for (int i = 0; i < cols.Length; i++) cols[i] = clear;
            tex.SetPixels32(cols);

            Color32 c = color;
            int ox = (int)origin.x, oy = (int)origin.y;
            int th = 3;   // 轴线粗细（3px，1px 看不见）

            // X 轴：负半轴 → 右边缘
            for (int x = ox - (int)negLen; x < width; x++)
                for (int t = 0; t < th; t++) tex.SetPixel(x, oy + t, c);
            // Y 轴：负半轴 → 上边缘
            for (int y = oy - (int)negLen; y < height; y++)
                for (int t = 0; t < th; t++) tex.SetPixel(ox + t, y, c);

            // 刻度线（X 轴向上、Y 轴向右）
            int step = Mathf.Max(1, (int)tickSpacing);
            int len = (int)tickLen;
            for (int x = ox; x < width; x += step)
                for (int t = 0; t < len; t++) tex.SetPixel(x, oy + th + t, c);
            for (int y = oy; y < height; y += step)
                for (int t = 0; t < len; t++) tex.SetPixel(ox + th + t, y, c);

            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return tex;
        }

        /// <summary>生成扫描线纹理（CRT 隔行暗线），每 spacing 像素一条水平暗线，供全屏 Image 用。</summary>
        public static Texture2D CreateScanlineTexture(int width, int height, int spacing, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var cols = new Color32[width * height];
            Color32 c = color;
            Color32 clear = new Color32(0, 0, 0, 0);
            int step = Mathf.Max(1, spacing);
            for (int y = 0; y < height; y++)
            {
                Color32 rowColor = (y % step == 0) ? c : clear;
                int rowStart = y * width;
                for (int x = 0; x < width; x++) cols[rowStart + x] = rowColor;
            }
            tex.SetPixels32(cols);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return tex;
        }

    }
}
