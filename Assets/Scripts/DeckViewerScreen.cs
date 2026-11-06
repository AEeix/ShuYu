using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 「我的卡组」查看器（覆盖层）：把当前卡组铺成卡面网格，随时看一眼，关掉就回去。
    /// <para>
    /// 故意**不走 <c>Switch()</c> 切屏流程**：它是盖在当前界面之上的一层，打开/关闭都是瞬间的，
    /// 不会销毁路线界面、不会播淡入淡出、关掉后地图还停在你刚才的位置。
    /// </para>
    /// <para>
    /// 内容是 <see cref="GameManager.DeckCards"/>（抽牌堆 + 弃牌堆 + 手牌，且不含衍生牌），
    /// 每次打开都重新读，所以打完一场、买过牌之后再看就是新的。
    /// </para>
    /// </summary>
    public class DeckViewerScreen : MonoBehaviour
    {
        RectTransform _root;

        const float CardW = 200f, CardH = 260f;   // 与战斗里「牌堆查看」同一尺寸，卡面走 UIFactory.CreateCardFace
        const float Gap = 24f;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("DeckViewerRoot", canvas.transform);
            UIFactory.Stretch(_root);
            _root.SetAsLastSibling();   // 盖在路线界面等一切 UI 之上

            // 半透明遮罩：点背景任意位置也能关掉
            var bg = UIFactory.CreatePanel("BG", _root, new Color(0f, 0f, 0f, 0.88f));
            UIFactory.Stretch(bg.rectTransform);
            var bgBtn = bg.gameObject.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(Close);

            var cards = GameManager.Instance.DeckCards;

            var title = UIFactory.CreateText("Title", _root, "我的卡组 · " + cards.Count + " 张", 64, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -60));

            BuildGrid(_root, cards);

            var close = UIFactory.CreateButton("Close", _root, "关 闭", 44,
                GameConfig.Accent, GameConfig.Background, () => Close());
            UIFactory.Place(close, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 40), new Vector2(120, 120));
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        void Close()
        {
            Destroy(this);   // OnDestroy 里销毁自己那棵树
        }

        /// <summary>纵向滚动区 + 卡面网格（每行 5 张，行内居中）。卡组可能几十张，所以必须能滚。</summary>
        void BuildGrid(RectTransform parent, System.Collections.Generic.List<CardData> cards)
        {
            var scrollGo = new GameObject("DeckScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            UIFactory.Place(scrollRt, Vector2.zero, Vector2.zero,
                new Vector2(160, 150), new Vector2(1760, 900));

            var viewport = UIFactory.CreateRect("Viewport", scrollRt);
            UIFactory.Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 10, 30);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var gridRt = UIFactory.CreateRect("Grid", content);
            var glg = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(CardW, CardH);
            glg.spacing = new Vector2(Gap, Gap);
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.constraint = GridLayoutGroup.Constraint.Flexible;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            for (int i = 0; i < cards.Count; i++)
                UIFactory.CreateCardFace(gridRt, cards[i], new Vector2(CardW, CardH));   // 一次画全：费用 + 卡名 + 大字 + 描述

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
