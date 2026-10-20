using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>战斗时查看地图：全屏半透明遮罩 + 只读网格快照（节点类型 + 缩圈 + 当前位置）。按 M/Tab 开关。</summary>
    public class MapOverlay : MonoBehaviour
    {
        const float Spacing = 160f;
        RectTransform _root;

        void Start() { Build(); }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        void Build()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("MapOverlay", canvas.transform);
            UIFactory.Stretch(_root);
            _root.SetAsLastSibling();

            var mask = UIFactory.CreatePanel("Mask", _root, new Color(0, 0, 0, 0.82f));
            UIFactory.Stretch(mask.rectTransform);

            var title = UIFactory.CreateText("Title", _root, "地 图", 48, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, -30));

            DrawMap();

            var hint = UIFactory.CreateText("Hint", _root, "按 M 或 Tab 关闭", 28, GameConfig.SubText);
            UIFactory.Place(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 80));
        }

        Vector2 NodePos(int col, int row)
        {
            float totalW = (RouteMap.GridCols - 1) * Spacing;
            float totalH = (RouteMap.GridRows - 1) * Spacing;
            float ox = (1920f - totalW) * 0.5f;
            float oy = (1080f - totalH) * 0.5f - 40f;
            return new Vector2(ox + col * Spacing, oy + row * Spacing);
        }

        void DrawMap()
        {
            var map = GameManager.Instance.Route;

            // 缩圈（第 5 次移动起才出现）
            if (map.movesTaken > RouteMap.ShrinkStartMoves)
            {
                var boss = map.nodes[map.bossIndex];
                Vector2 center = NodePos(boss.col, boss.row);
                DrawSquare(center, map.ShrinkRadius() * Spacing);
            }

            for (int i = 0; i < map.nodes.Count; i++)
                DrawNode(i);
        }

        void DrawSquare(Vector2 center, float halfSide)
        {
            Vector2 tl = center + new Vector2(-halfSide, halfSide);
            Vector2 tr = center + new Vector2(halfSide, halfSide);
            Vector2 br = center + new Vector2(halfSide, -halfSide);
            Vector2 bl = center + new Vector2(-halfSide, -halfSide);
            DrawLine(tl, tr, GameConfig.Hex("#4DA6FF"));
            DrawLine(tr, br, GameConfig.Hex("#4DA6FF"));
            DrawLine(br, bl, GameConfig.Hex("#4DA6FF"));
            DrawLine(bl, tl, GameConfig.Hex("#4DA6FF"));
        }

        void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var line = UIFactory.CreatePanel("Edge", _root.transform, color);
            line.raycastTarget = false;
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            Vector2 diff = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(diff.magnitude, 5f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        }

        void DrawNode(int i)
        {
            var map = GameManager.Instance.Route;
            var node = map.nodes[i];
            Vector2 pos = NodePos(node.col, node.row);
            bool current = (i == map.currentIndex);

            var rt = UIFactory.CreateRect("Node_" + i, _root.transform);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(60f, 60f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.color = RouteScreen.NodeColor(node.type);

            var label = UIFactory.CreateText("Label", rt, RouteScreen.NodeLabel(node.type), 28, GameConfig.Background);
            UIFactory.Stretch(label.rectTransform);

            if (current)
                UIFactory.AddOutline(img, GameConfig.Accent, new Vector2(4f, -4f));
            else
                UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));
        }
    }
}
