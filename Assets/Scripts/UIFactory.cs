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

            // 场景化：场景里已有主 Canvas（SceneBuilder 生成）时直接复用，不再新建。
            // 注意：CRT 扫描线也是 Canvas（Overlay 置顶），但它没有 GraphicRaycaster。
            // 若 FindObjectOfType 误抓扫描线 Canvas，战斗等运行时 UI 会被挂到无 Raycaster 的画布上、点不动。
            _canvas = FindMainCanvas();
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

        /// <summary>找带 GraphicRaycaster 的主 Canvas，跳过扫描线等无 Raycaster 的画布。</summary>
        static Canvas FindMainCanvas()
        {
            var canvases = Object.FindObjectsOfType<Canvas>();
            foreach (var c in canvases)
                if (c.GetComponent<GraphicRaycaster>() != null) return c;
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<string, TMP_FontAsset> _cjkCache
            = new System.Collections.Generic.Dictionary<string, TMP_FontAsset>();

        /// <summary>
        /// 补全中文字体 fallback 链：主字体 → 方舟像素 → 黑体。
        /// 方舟像素只有 24850 字，实测缺 15 个游戏用到的常用字（即然避窗杀热骤斜毁旋执概奖缘施），
        /// 其中就包含「然」，而原来只有它一层 fallback，缺字会整格显示空白。
        /// 黑体 28522 字，实测 100% 覆盖游戏源码里出现的 799 个汉字，因此兜到黑体即可；
        /// 思源黑体（15.8MB）虽然覆盖更全，但体积大、启动解析慢，暂不接入。
        /// 场景化 UI 用的是烘焙进 Main.unity 的字体资产，所以这里把烘焙资产和运行时资产一起补。
        /// </summary>
        public static void EnsureChineseFallbacks()
        {
            // 内部兜异常：本方法会在一些关键路径上被调用（场景加载前 / GameManager.Start 里），
            // 万一字体解析失败也绝不能连带把后头的按钮接线、UI 初始化一起中断。
            try
            {
                var cjkBaked = Resources.Load<TMP_FontAsset>("Fonts/ShuYuCJK SDF");    // 方舟像素（烘焙）
                var monoBaked = Resources.Load<TMP_FontAsset>("Fonts/ShuYuMono SDF");  // VT323（烘焙）
                var simhei = LoadOrCreateCjkAsset("Fonts/SimHei");

                // 方舟像素自身再往下兜一层黑体
                if (cjkBaked != null) SetChain(cjkBaked, simhei);

                // 主字体：方舟像素 → 黑体（烘焙的方舟像素存在就不必再临时创建一个，省启动开销）
                var cjk = cjkBaked != null
                    ? cjkBaked
                    : LoadOrCreateCjkAsset("Fonts/ark-pixel-12px-monospaced-zh_hans");
                if (monoBaked != null) SetChain(monoBaked, cjk, simhei);
                if (_font != null) SetChain(_font, cjk, simhei);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ShuYu] 字体 fallback 链补全失败，可能出现缺字：" + e.Message);
            }
        }

        /// <summary>整体替换 fallback 表（去重、排除自身、保持给定顺序）。</summary>
        static void SetChain(TMP_FontAsset target, params TMP_FontAsset[] chain)
        {
            if (target == null) return;
            var list = new System.Collections.Generic.List<TMP_FontAsset>();
            foreach (var a in chain)
                if (a != null && a != target && !list.Contains(a)) list.Add(a);
            target.fallbackFontAssetTable = list;
        }

        /// <summary>按 Resources 路径加载中文源字体并转成 TMP 资产（缓存，只建一次）。</summary>
        static TMP_FontAsset LoadOrCreateCjkAsset(string resourcePath)
        {
            TMP_FontAsset cached;
            if (_cjkCache.TryGetValue(resourcePath, out cached))
            {
                if (cached == null) _cjkCache.Remove(resourcePath);
                else return cached;
            }

            var font = Resources.Load<Font>(resourcePath);
            if (font == null) return null;

            var asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset != null)
            {
                asset.name = System.IO.Path.GetFileName(resourcePath) + " (fallback)";
                asset.isMultiAtlasTexturesEnabled = true;   // 兜底字体是动态图集，开多图集避免字形挤满后丢字
                _cjkCache[resourcePath] = asset;
            }
            return asset;
        }

        /// <summary>
        /// 场景加载前就补好 fallback 链。必须早于 TMP_Text 首次生成网格，
        /// 否则已渲染的文字不会因为事后补链而刷新。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootstrapFontFallbacks()
        {
            EnsureChineseFallbacks();
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
                    if (_font != null) _font.name = "ShuYuMono";
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

                // 统一补中文 fallback 链（方舟像素 → 黑体）
                if (_font != null)
                    SetChain(_font,
                        LoadOrCreateCjkAsset("Fonts/ark-pixel-12px-monospaced-zh_hans"),
                        LoadOrCreateCjkAsset("Fonts/SimHei"));

                return _font;
            }
        }

        /// <summary>圆角矩形 Sprite（九宫格，程序化生成，零手绘）。</summary>
        static Sprite _rounded;
        public const float RoundedRadius = 18f;

        /// <summary>生成圆角矩形纹理（运行时 Sprite 与编辑器持久化共用，避免两处漂移）。</summary>
        public static Texture2D CreateRoundedTexture(float r = 18f)
        {
            const int size = 64;
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

        static Sprite _roundedSoft;
        /// <summary>圆角更小的 Sprite（卡面用，稍微圆润）。</summary>
        public static Sprite RoundedSoft
        {
            get
            {
                if (_roundedSoft != null) return _roundedSoft;
                const float r = 8f;
                var tex = CreateRoundedTexture(r);
                _roundedSoft = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                _roundedSoft.name = "RoundedRectSoft";
                return _roundedSoft;
            }
        }

        static Sprite _slime;
        /// <summary>史莱姆 Sprite（缓存）：从 Resources 加载已抠白底的透明 PNG（白底由 _process_slime.ps1 预处理成 slime_alpha.png）。</summary>
        public static Sprite Slime
        {
            get
            {
                if (_slime != null) return _slime;
                var tex = Resources.Load<Texture2D>("Sprites/slime_alpha");
                if (tex == null) { Debug.LogWarning("[ShuYu] 找不到素材 Sprites/slime_alpha"); return null; }
                _slime = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _slime.name = "slime";
                return _slime;
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
            rt.gameObject.AddComponent<ButtonClickSound>();  // 按钮点击音（组件可序列化）
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

        /// <summary>
        /// 画一张可点选的卡面（名字 + 大字 + 描述 + 描边），点击回调 onPick(card)。
        /// 返回的 RectTransform 已锚定到 (0.5,0.5)，调用方负责设 anchoredPosition。
        /// 供商店「选择删牌」、火堆「选择强化」等需要玩家自选一张牌的界面复用。
        /// </summary>
        public static RectTransform CreateCardButton(Transform parent, string name, CardData card, Vector2 size, UnityAction<CardData> onPick)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = GameConfig.CardBase;
            AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            if (!string.IsNullOrEmpty(card.cardName))
            {
                var nameText = CreateText("Name", rt, card.cardName, 22, GameConfig.SubText);
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.enableAutoSizing = true;
                nameText.fontSizeMin = 12;
                nameText.fontSizeMax = 22;
                Place(nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -42), new Vector2(-8, -8));
            }

            var label = CreateText("Label", rt, card.DisplayText(), 56, GameConfig.CardColor(card.op));
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 56;
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -132), new Vector2(-8, -46));

            if (!string.IsNullOrEmpty(card.description))
            {
                var desc = CreateText("Desc", rt, card.description, 20, GameConfig.SubText);
                desc.alignment = TextAlignmentOptions.Center;
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 11;
                desc.fontSizeMax = 20;
                Place(desc.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 10), new Vector2(-8, 120));
            }

            var c = card;
            if (onPick != null) btn.onClick.AddListener(() => onPick(c));
            return rt;
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
