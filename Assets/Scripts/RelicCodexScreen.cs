using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 遗物图鉴：把所有遗物一行一个列出来 —— 左边遗物名称、中间留白、右边遗物效果。
    /// <para>
    /// **内容不写死**：每次打开都重新遍历 <see cref="RelicCatalog.Entries"/>，
    /// 所以以后往 <c>Core/Relic.cs</c> 的遗物库里加一件遗物，这里会自动多一行，不用改本文件。
    /// </para>
    /// <para>
    /// 入口在图鉴主页（<see cref="CodexHomeScreen"/>），返回也回到图鉴主页。
    /// 布局照 <see cref="KeywordCodexScreen"/> 抄的（纵向滚动 + 卡片行），两个屏看起来是一套东西。
    /// </para>
    /// </summary>
    public class RelicCodexScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _content;

        const float RowHeight = 112f;    // 每行遗物卡片的高度
        const float NameColumnW = 380f;  // 名称列宽度：右边全部留给效果说明

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("RelicCodexRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "遗 物 图 鉴", 76, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(0, 940), new Vector2(1920, 1040));

            _content = BuildScrollArea(_root);

            // ★ 唯一的"内容来源"：遍历遗物库。加遗物只改 Core/Relic.cs，这里一行都不用动。
            foreach (var relic in RelicCatalog.Entries)
                CreateRow(relic);

            var back = UIFactory.CreateButton("Back", _root, "返 回", 44,
                GameConfig.Accent, GameConfig.Background, () => GameManager.Instance.ShowCodexHome());
            UIFactory.Place(back, Vector2.zero, Vector2.zero,
                new Vector2(860, 40), new Vector2(1060, 120));

            // 嵌套布局（VerticalLayoutGroup + ContentSizeFitter）首帧尺寸可能没算准，强制重建一次滚动范围
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>纵向滚动区（与关键词图鉴 / 卡牌图鉴结构一致，方便对照着学）。</summary>
        RectTransform BuildScrollArea(RectTransform parent)
        {
            var scrollGo = new GameObject("RelicScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            UIFactory.Place(scrollRt, Vector2.zero, Vector2.zero,
                new Vector2(180, 150), new Vector2(1740, 930));   // 1560 × 780

            // Viewport：透明 Image 接拖拽 + RectMask2D 裁剪
            var viewport = UIFactory.CreateRect("Viewport", scrollRt);
            UIFactory.Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            // Content：顶对齐，宽度撑满，高度由内容决定
            var content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 20, 40);
            vlg.spacing = 16;
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

        /// <summary>一行遗物：左边名称、右边效果（名称列是固定宽度，所以名字长短不会把效果文字挤歪）。</summary>
        void CreateRow(Relic relic)
        {
            var rt = UIFactory.CreateRect("Relic_" + relic.name, _content);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = GameConfig.CardBase;
            bg.raycastTarget = false;
            UIFactory.AddOutline(bg, GameConfig.SubText, new Vector2(2f, -2f));

            // 名称（左侧固定列，纵向居中）
            var name = UIFactory.CreateText("Name", rt, relic.name, 48, GameConfig.Accent);
            name.alignment = TextAlignmentOptions.Left;
            UIFactory.Place(name.rectTransform, Vector2.zero, new Vector2(0, 1),
                new Vector2(32, 0), new Vector2(NameColumnW, 0));

            // 效果（名称列之后的全部区域；字号自动缩放，长说明也不会溢出）
            // 关键词（格挡 / 冻结 / 灼烧 / 标记）由 KeywordCatalog 自动加粗，和卡牌图鉴同一套规则
            var desc = UIFactory.CreateText("Desc", rt, KeywordCatalog.Highlight(relic.description), 30, GameConfig.SubText);
            desc.alignment = TextAlignmentOptions.Left;
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 18;
            desc.fontSizeMax = 30;
            UIFactory.Place(desc.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(NameColumnW + 24, 16), new Vector2(-130, -16));

            // 职业初始遗物不进随机掉落池，标一下，免得玩家一直在奖励里等它
            if (!relic.startingOnly) return;
            var tag = UIFactory.CreateText("Tag", rt, "初始遗物", 24, GameConfig.CardGold);
            tag.alignment = TextAlignmentOptions.Right;
            UIFactory.Place(tag.rectTransform, new Vector2(1, 0), Vector2.one,
                new Vector2(-120, 0), new Vector2(-28, 0));
        }
    }
}
