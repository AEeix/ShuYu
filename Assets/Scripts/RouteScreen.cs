using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ShuYu
{
    /// <summary>路线界面：在坐标轴上画节点 + 连线，点可到达的节点前进。静态框架（标题/返回）可场景化。</summary>
    public class RouteScreen : MonoBehaviour
    {
        RectTransform _root;      // 静态框架根（场景或运行时）
        RectTransform _dynamic;   // 动态节点/连线容器（每次进路线重建）
        bool _ownsRoot = true;

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

            // 动态节点/连线放独立容器，场景复用 _root 时不残留上一局的旧节点
            _dynamic = UIFactory.CreateRect("RouteDynamic", _root);
            UIFactory.Stretch(_dynamic);

            StartCoroutine(Reveal(gm.Route));
        }

        void OnDestroy()
        {
            if (_dynamic != null) Destroy(_dynamic.gameObject);
            if (_ownsRoot && _root != null) Destroy(_root.gameObject);
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

        Vector2 NodePos(MapNode node)
        {
            // 路线从原点(150,150)向正方向移动：向右（col）再向上（row）
            const float ox = 150f, oy = 150f;
            int levels = RouteMap.Rows + 2;
            float x = ox + node.col * 1000f + node.row * 50f;
            float y = oy + node.row * (800f / (levels - 1));
            return new Vector2(x, y);
        }

        /// <summary>路线从原点逐层长出来：先画边、再让本层节点弹入。</summary>
        IEnumerator Reveal(RouteMap map)
        {
            int maxRow = RouteMap.Rows + 1;
            for (int row = 0; row <= maxRow; row++)
            {
                if (row > 0)
                    DrawEdgesToRow(map, row);

                for (int i = 0; i < map.nodes.Count; i++)
                {
                    if (map.nodes[i].row != row) continue;
                    var rt = CreateNode(i);
                    rt.localScale = Vector3.zero;
                    StartCoroutine(PopIn(rt));
                }
                yield return new WaitForSeconds(0.14f);
            }
        }

        void DrawEdgesToRow(RouteMap map, int row)
        {
            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (map.nodes[i].row != row - 1) continue;
                Vector2 a = NodePos(map.nodes[i]);
                foreach (int j in map.nodes[i].next)
                    if (map.nodes[j].row == row)
                        DrawLine(a, NodePos(map.nodes[j]));
            }
        }

        void DrawLine(Vector2 a, Vector2 b)
        {
            var line = UIFactory.CreatePanel("Edge", _dynamic, GameConfig.GridLine);
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            Vector2 mid = (a + b) * 0.5f;
            Vector2 diff = b - a;
            float len = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(len, 4f);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
        }

        IEnumerator PopIn(RectTransform rt)
        {
            const float dur = 0.14f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float s = 1f - (1f - t / dur) * (1f - t / dur);   // ease-out 弹入
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        RectTransform CreateNode(int i)
        {
            var map = GameManager.Instance.Route;
            var node = map.nodes[i];
            Vector2 pos = NodePos(node);
            bool current = (i == map.currentIndex);

            var rt = UIFactory.CreateRect("Node_" + i, _dynamic);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(68f, 68f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.color = NodeColor(node.type);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnNodeClicked(idx));

            var label = UIFactory.CreateText("Label", rt, NodeLabel(node.type), 30, GameConfig.Background);
            UIFactory.Stretch(label.rectTransform);

            if (current)
                UIFactory.AddOutline(img, GameConfig.Accent, new Vector2(4f, -4f));
            else
                UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            return rt;
        }

        bool IsReachable(RouteMap map, int i)
        {
            return map.nodes[map.currentIndex].next.Contains(i);
        }

        void OnNodeClicked(int idx)
        {
            var map = GameManager.Instance.Route;
            if (!IsReachable(map, idx)) return;
            GameManager.Instance.EnterNode(idx);
        }

        static Color NodeColor(NodeType type)
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

        static string NodeLabel(NodeType type)
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
