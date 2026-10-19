using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>路线界面：全屏网格地图，节点在交汇处，缩圈（边界线+圈外变蓝），滚轮缩放，点击相邻节点移动。</summary>
    public class RouteScreen : MonoBehaviour
    {
        const float Spacing = 160f;                 // 网格节点间距（像素）
        const float MinZoom = 0.5f, MaxZoom = 1.5f, ZoomStep = 0.1f;

        RectTransform _root;
        RectTransform _dynamic;
        TMP_Text _hpText;
        bool _ownsRoot = true;
        float _zoom = 1f;
        float _targetZoom = 1f;
        float _zoomVel = 0f;

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm.RouteRoot != null)
            {
                _ownsRoot = false;
                _root = gm.RouteRoot.GetComponent<RectTransform>();
            }
            else
            {
                _ownsRoot = true;
                _root = BuildStaticFrame(UIFactory.EnsureCanvas().transform);
            }
            WireButtons();

            // 背景漂浮数字动画（在网格下层）
            var bgFx = UIFactory.CreateRect("RouteBgFx", _root);
            UIFactory.Stretch(bgFx);
            bgFx.gameObject.AddComponent<RouteBackgroundFX>();

            _dynamic = UIFactory.CreateRect("RouteDynamic", _root);
            UIFactory.Stretch(_dynamic);

            // HP 显示（放 _root，不随地图缩放）
            _hpText = UIFactory.CreateText("HP", _root, "HP：" + gm.Player.health, 44, GameConfig.Accent);
            UIFactory.Place(_hpText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -170), new Vector2(340, -120));

            Build();
        }

        void OnDestroy()
        {
            if (_hpText != null) Destroy(_hpText.gameObject);
            if (_dynamic != null) Destroy(_dynamic.gameObject);
            if (_ownsRoot && _root != null) Destroy(_root.gameObject);
            // 复位坐标轴缩放
            if (GameManager.Instance != null && GameManager.Instance.WorldAxis != null)
                GameManager.Instance.WorldAxis.localScale = Vector3.one;
        }

        void Update()
        {
            // 鼠标滚轮缩放地图：目标值 + SmoothDamp 平滑（带惯性），坐标轴同步
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
                _targetZoom = Mathf.Clamp(_targetZoom + scroll * ZoomStep, MinZoom, MaxZoom);

            _zoom = Mathf.SmoothDamp(_zoom, _targetZoom, ref _zoomVel, 0.1f);
            if (_dynamic != null) _dynamic.localScale = Vector3.one * _zoom;
            if (GameManager.Instance != null && GameManager.Instance.WorldAxis != null)
                GameManager.Instance.WorldAxis.localScale = Vector3.one * _zoom;
        }

        /// <summary>建静态框架（标题 + 返回），不接线。SceneBuilder 与运行时回退共用。</summary>
        public static RectTransform BuildStaticFrame(Transform parent)
        {
            var root = UIFactory.CreateRect("RouteRoot", parent);
            UIFactory.Stretch(root);

            var title = UIFactory.CreateText("Title", root, "选 路", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -50));

            var back = UIFactory.CreateButton("Back", root, "回到标题", 40,
                GameConfig.CardBase, GameConfig.SubText, null);
            UIFactory.Place(back, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 30), new Vector2(-40, 100));

            return root;
        }

        void WireButtons()
        {
            var back = _root.Find("Back");
            if (back != null)
            {
                var btn = back.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => GameManager.Instance.ShowTitle());
            }
        }

        // ===== 渲染 =====

        Vector2 NodePos(int col, int row)
        {
            float totalW = (RouteMap.GridCols - 1) * Spacing;
            float totalH = (RouteMap.GridRows - 1) * Spacing;
            float ox = (1920f - totalW) * 0.5f;
            float oy = (1080f - totalH) * 0.5f - 40f;   // 略下移避开标题
            return new Vector2(ox + col * Spacing, oy + row * Spacing);
        }

        void Build()
        {
            for (int i = _dynamic.childCount - 1; i >= 0; i--)
                Destroy(_dynamic.GetChild(i).gameObject);

            var map = GameManager.Instance.Route;

            DrawShrink(map);       // 缩圈（第 3 步起才出现）
            DrawEdges(map);        // 虚线网格 + 实线轨迹
            for (int i = 0; i < map.nodes.Count; i++)
                CreateNode(i);
        }

        /// <summary>缩圈：第 3 次移动起出现圈边界线；圈外节点在 CreateNode 里变蓝。</summary>
        void DrawShrink(RouteMap map)
        {
            if (map.movesTaken <= RouteMap.ShrinkStartMoves) return;   // 还没开始缩圈

            var boss = map.nodes[map.bossIndex];
            Vector2 center = NodePos(boss.col, boss.row);
            float radiusPx = map.ShrinkRadius() * Spacing;
            DrawCircleOutline(center, radiusPx, GameConfig.Hex("#4DA6FF"));
        }

        /// <summary>圈边界线：用 64 段短线拼成一个圆。</summary>
        void DrawCircleOutline(Vector2 center, float radiusPx, Color color)
        {
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector2 p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radiusPx;
                Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radiusPx;
                DrawLine(p0, p1, color);
            }
        }

        /// <summary>画边：正交虚线网格 + 走过的实线（金）。</summary>
        void DrawEdges(RouteMap map)
        {
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                foreach (int j in map.Neighbors(i))
                {
                    var other = map.nodes[j];
                    if (other.col < node.col || (other.col == node.col && other.row < node.row)) continue; // 去重

                    bool diagonal = (other.col != node.col && other.row != node.row);
                    bool trail = node.reached && other.reached;
                    if (!trail && diagonal) continue;   // 非轨迹的对角线不画，保持方格干净

                    Vector2 a = NodePos(node.col, node.row);
                    Vector2 b = NodePos(other.col, other.row);
                    if (trail) DrawLine(a, b, GameConfig.Accent);
                    else DrawDashedLine(a, b, GameConfig.GridLine);
                }
            }
        }

        void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var line = UIFactory.CreatePanel("Edge", _dynamic, color);
            line.raycastTarget = false;
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            Vector2 diff = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(diff.magnitude, 5f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        }

        /// <summary>虚线：沿线段铺一排短横（不细不短）。</summary>
        void DrawDashedLine(Vector2 a, Vector2 b, Color color)
        {
            Vector2 diff = b - a;
            float len = diff.magnitude;
            if (len <= 0.01f) return;
            Vector2 unit = diff / len;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            const float dashLen = 24f, gapLen = 14f, thick = 6f;
            float pos = 0f;
            while (pos < len)
            {
                float segLen = Mathf.Min(dashLen, len - pos);
                Vector2 segCenter = a + unit * (pos + segLen * 0.5f);
                var line = UIFactory.CreatePanel("Dash", _dynamic, color);
                line.raycastTarget = false;
                var rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = segCenter;
                rt.sizeDelta = new Vector2(segLen, thick);
                rt.localRotation = Quaternion.Euler(0, 0, angle);
                pos += dashLen + gapLen;
            }
        }

        RectTransform CreateNode(int i)
        {
            var map = GameManager.Instance.Route;
            var node = map.nodes[i];
            Vector2 pos = NodePos(node.col, node.row);
            bool current = (i == map.currentIndex);
            bool visited = node.reached && !current;

            var rt = UIFactory.CreateRect("Node_" + i, _dynamic);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(60f, 60f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.color = visited ? VisitedColor : NodeColor(node.type);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnNodeClicked(idx));

            var label = UIFactory.CreateText("Label", rt, NodeLabel(node.type), 28, GameConfig.Background);
            UIFactory.Stretch(label.rectTransform);

            // 圈外节点：缩圈开始后变蓝（危险），BOSS 不标
            if (map.movesTaken > RouteMap.ShrinkStartMoves && !map.IsSafe(i) && node.type != NodeType.Boss)
            {
                var danger = UIFactory.CreatePanel("Danger", rt, new Color(0.3f, 0.6f, 1f, 0.28f));
                danger.raycastTarget = false;
                UIFactory.Stretch(danger.rectTransform);
                danger.rectTransform.SetAsFirstSibling();
            }

            if (current)
                UIFactory.AddOutline(img, GameConfig.Accent, new Vector2(4f, -4f));
            else if (IsReachable(map, i))
                UIFactory.AddOutline(img, new Color(1f, 0.75f, 0.1f, 0.7f), new Vector2(3f, -3f));
            else
                UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            return rt;
        }

        bool IsReachable(RouteMap map, int i)
        {
            return map.Neighbors(map.currentIndex).Contains(i);
        }

        void OnNodeClicked(int idx)
        {
            var map = GameManager.Instance.Route;
            if (!IsReachable(map, idx)) return;
            GameManager.Instance.EnterNode(idx);
        }

        // ===== 颜色 / 标签 =====

        static readonly Color VisitedColor = new Color(0.42f, 0.42f, 0.48f, 0.85f);

        public static Color NodeColor(NodeType type)
        {
            switch (type)
            {
                case NodeType.Start: return GameConfig.SubText;
                case NodeType.Battle: return GameConfig.CardBlue;
                case NodeType.Elite: return GameConfig.CardOrange;
                case NodeType.Camp: return GameConfig.CardGold;
                case NodeType.Treasure: return GameConfig.Hex("#3DE0A0");
                case NodeType.Mystery: return GameConfig.Hex("#B47CFF");
                case NodeType.Shop: return GameConfig.Hex("#FF6EC7");
                case NodeType.Boss: return GameConfig.Hex("#FF2222");
                default: return GameConfig.GridLine;
            }
        }

        public static string NodeLabel(NodeType type)
        {
            switch (type)
            {
                case NodeType.Start: return "起";
                case NodeType.Battle: return "战";
                case NodeType.Elite: return "精";
                case NodeType.Camp: return "火";
                case NodeType.Treasure: return "宝";
                case NodeType.Mystery: return "?";
                case NodeType.Shop: return "店";
                case NodeType.Boss: return "王";
                default: return "?";
            }
        }
    }
}
