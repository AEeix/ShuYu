using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>路线界面：在坐标系里画节点 + 连线，点可到达的节点前进。</summary>
    public class RouteScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start() { Build(); }
        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }

        void Build()
        {
            var canvas = UIFactory.EnsureCanvas();
            var go = new GameObject("RouteRoot", typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "选 路", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -50));

            var map = GameManager.Instance.Route;
            DrawEdges(map);
            DrawNodes(map);

            var back = UIFactory.CreateButton("Back", _root, "回到标题", 40,
                GameConfig.CardBase, GameConfig.SubText, () => GameManager.Instance.ShowTitle());
            UIFactory.Place(back, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 30), new Vector2(-40, 100));
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

        void DrawEdges(RouteMap map)
        {
            for (int i = 0; i < map.nodes.Count; i++)
            {
                Vector2 a = NodePos(map.nodes[i]);
                foreach (int j in map.nodes[i].next)
                    DrawLine(a, NodePos(map.nodes[j]));
            }
        }

        void DrawLine(Vector2 a, Vector2 b)
        {
            var line = UIFactory.CreatePanel("Edge", _root, GameConfig.GridLine);
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

        void DrawNodes(RouteMap map)
        {
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                Vector2 pos = NodePos(node);
                bool current = (i == map.currentIndex);
                bool reachable = IsReachable(map, i);

                var rt = UIFactory.CreateRect("Node_" + i, _root);
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
            }
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
                case NodeType.Battle: return GameConfig.CardBlue;
                case NodeType.Elite: return GameConfig.CardOrange;
                case NodeType.Camp: return GameConfig.CardGold;
                case NodeType.Boss: return GameConfig.Hex("#FF2222");
                default: return GameConfig.GridLine;
            }
        }

        static string NodeLabel(NodeType type)
        {
            switch (type)
            {
                case NodeType.Battle: return "战";
                case NodeType.Elite: return "精";
                case NodeType.Camp: return "火";
                case NodeType.Boss: return "王";
                default: return "?";
            }
        }
    }
}
