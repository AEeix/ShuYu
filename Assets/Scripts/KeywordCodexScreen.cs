using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 关键词图鉴：把地块状态关键词（标记 / 灼烧 / 冻结）一个个摊开讲清楚。
    /// <para>
    /// 入口在卡牌图鉴界面的右上角按钮（<see cref="CodexScreen"/>），返回也回到卡牌图鉴，
    /// 这样不用改动场景里烘焙好的标题界面。
    /// </para>
    /// <para>
    /// 内容全部来自 <see cref="KeywordCatalog"/>：想加关键词只改那个文件，这里会自动多出一张卡。
    /// </para>
    /// </summary>
    public class KeywordCodexScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _content;

        const float EntryHeight = 200f;   // 每个关键词一张卡片的高度

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("KeywordCodexRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "关 键 词 图 鉴", 76, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(0, 940), new Vector2(1920, 1040));

            _content = BuildScrollArea(_root);

            foreach (var def in KeywordCatalog.Entries)
                CreateEntry(def);

            var back = UIFactory.CreateButton("Back", _root, "返 回", 44,
                GameConfig.Accent, GameConfig.Background, () => GameManager.Instance.ShowCodex());
            UIFactory.Place(back, Vector2.zero, Vector2.zero,
                new Vector2(860, 40), new Vector2(1060, 120));

            // 嵌套布局首帧尺寸可能没算准，强制重建一次，保证滚动范围正确
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>纵向滚动区（与卡牌图鉴结构一致，方便对照着学）。</summary>
        RectTransform BuildScrollArea(RectTransform parent)
        {
            var scrollGo = new GameObject("KeywordScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            UIFactory.Place(scrollRt, Vector2.zero, Vector2.zero,
                new Vector2(260, 150), new Vector2(1660, 930));   // 1400 × 780

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
            vlg.padding = new RectOffset(40, 40, 20, 40);
            vlg.spacing = 24;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            return content;
        }

        /// <summary>画一个关键词卡片：左边关键词名，右边说明（说明里的关键词自动加粗）。</summary>
        void CreateEntry(KeywordDef def)
        {
            var rt = UIFactory.CreateRect("Keyword_" + def.name, _content);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = EntryHeight;
            le.preferredHeight = EntryHeight;

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = GameConfig.CardBase;
            bg.raycastTarget = false;
            UIFactory.AddOutline(bg, GameConfig.SubText, new Vector2(2f, -2f));

            // 关键词名（左侧竖排区域）
            var name = UIFactory.CreateText("Name", rt, def.name, 52, GameConfig.Accent);
            name.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(name.rectTransform,
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(20, 20), new Vector2(240, -20));

            // 说明（右侧；字号自动缩放，长文本不会溢出）
            var desc = UIFactory.CreateText("Desc", rt, KeywordCatalog.Highlight(def.description), 30, GameConfig.SubText);
            desc.alignment = TextAlignmentOptions.Left;
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 18;
            desc.fontSizeMax = 30;
            UIFactory.Place(desc.rectTransform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(260, 24), new Vector2(-32, -24));
        }
    }
}
