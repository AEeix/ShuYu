using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 卡牌图鉴：把三个职业的职业牌 + 全职业战利品池全部摊开给玩家看。
    /// 「削减 / 重削」这类全职业共有的基础牌只在「全职业卡」段列一次，不在各职业段重复出现。
    /// 卡片外观与战斗中一致（圆角底 + 描边 + 费用 + 名字 + 大字数值 + 描述），但不可点击。
    /// 布局：按职业分段；每段有段名、玩法说明、卡片网格（自动换行、两侧留白），段间画一条分隔线。
    /// 内容总高超出屏幕，所以整块放进 ScrollRect 纵向滚动（鼠标滚轮 / 拖拽）。
    /// </summary>
    public class CodexScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _content;

        static readonly Vector2 CardSize = new Vector2(150f, 220f);   // 比战斗中的 180×280 小一圈，一屏能多放几张
        const float CardGap = 16f;
        const float MarginX = 120f;                                   // 滚动区左右外边距
        const float ContentPadX = 40f;                                // 内容左右内边距（配合行内居中 = 两侧留白）

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("CodexRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "卡 牌 图 鉴", 76, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(0, 940), new Vector2(1920, 1040));

            _content = BuildScrollArea(_root);

            var warrior = ClassCatalog.Warrior();
            var mage = ClassCatalog.Mage();
            var reaper = ClassCatalog.Reaper();

            // 分段完全由卡牌的 `owner` 成员标记决定：职业段 = 该职业专属池，全职业段 = 标了「全职业」的牌。
            // 每段只显示段名 + 卡牌，不再写玩法说明。
            AddSection(warrior.displayName, CardLibrary.ClassPool(warrior.tag));
            AddSection(mage.displayName, CardLibrary.ClassPool(mage.tag));
            AddSection(reaper.displayName, CardLibrary.ClassPool(reaper.tag));
            AddSection("全职业卡", CardLibrary.AllUniversalCards());

            // 衍生牌：只由卡牌/遗物产生（不进卡组、不进奖励），有才显示这一段
            var tokens = CardLibrary.AllTokenCards();
            if (tokens.Count > 0) AddSection("衍生牌", tokens);

            var back = UIFactory.CreateButton("Back", _root, "返 回", 44,
                GameConfig.Accent, GameConfig.Background, () => GameManager.Instance.ShowTitle());
            UIFactory.Place(back, Vector2.zero, Vector2.zero,
                new Vector2(860, 40), new Vector2(1060, 120));

            // 右上角「关键词图鉴」入口（关键词说明单独一屏，内容来自 KeywordCatalog）
            var keywords = UIFactory.CreateButton("Keywords", _root, "关键词图鉴", 40,
                GameConfig.CardBase, GameConfig.Accent, () => GameManager.Instance.ShowKeywords());
            UIFactory.Place(keywords,
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-460, 40), new Vector2(-40, 120));

            // 嵌套布局（VerticalLayoutGroup + GridLayoutGroup + ContentSizeFitter）首帧尺寸可能没算准，
            // 这里强制重建一次，保证滚动范围正确。
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        // ---------------- 滚动区 ----------------

        RectTransform BuildScrollArea(RectTransform parent)
        {
            var scrollGo = new GameObject("CodexScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            UIFactory.Place(scrollRt, Vector2.zero, Vector2.zero,
                new Vector2(MarginX, 150), new Vector2(1920f - MarginX, 930));   // 1680 × 780

            // Viewport：透明 Image 用来接收拖拽，RectMask2D 负责裁剪
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
            vlg.padding = new RectOffset((int)ContentPadX, (int)ContentPadX, 20, 40);
            vlg.spacing = 28;
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

        // ---------------- 分段 ----------------

        void AddSection(string name, List<CardData> deck)
        {
            var section = UIFactory.CreateRect("Section_" + name, _content);

            var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            AddLine(section);                       // 段首分隔线
            AddLabel(section, name, 44, GameConfig.Accent, 54);

            var grid = UIFactory.CreateRect("Grid", section);
            var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = CardSize;
            glg.spacing = new Vector2(CardGap, CardGap);
            glg.childAlignment = TextAnchor.UpperCenter;   // 行内居中 → 两侧留白对称
            glg.constraint = GridLayoutGroup.Constraint.Flexible;
            glg.padding = new RectOffset(0, 0, 6, 6);

            foreach (var c in Distinct(deck)) CreateCardFace(grid, c);

            AddLine(section);                       // 段尾分隔线
        }

        void AddLabel(RectTransform parent, string text, float size, Color color, float height)
        {
            var t = UIFactory.CreateText("Label", parent, text, size, color);
            t.alignment = TextAlignmentOptions.Center;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        void AddLine(RectTransform parent)
        {
            var img = UIFactory.CreatePanel("Line", parent, GameConfig.GridLine);
            img.raycastTarget = false;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 3f;
            le.preferredHeight = 3f;
        }

        // ---------------- 卡片 ----------------

        /// <summary>去重：同一张牌在卡组里有多份时只展示一次。</summary>
        static List<CardData> Distinct(List<CardData> cards)
        {
            var seen = new HashSet<string>();
            var list = new List<CardData>();
            foreach (var c in cards)
            {
                string key = c.cardName + "|" + c.DisplayText() + "|" + c.cost + "|" + c.description
                    + "|" + c.magnitude + "|" + c.heal + "|" + c.induce
                    + "|" + c.toxinGoal + "|" + c.toxinDamage + "|" + c.nextEnergyDelta + "|" + c.nextDrawDelta
                    + "|" + c.delayedValueDelta + "|" + c.noCore + "|" + c.label + "|" + c.burnLayers
                    + "|" + c.freezeLayers + "|" + c.gainEnergy + "|" + c.condition + "|" + c.bonusMagnitude
                    + "|" + c.isToken + "|" + c.noTarget + "|" + c.otherCellsDelta + "|" + c.preGrowth
                    + "|" + c.onHurtAllDelta + "|" + c.onHurtHeal;
                if (seen.Add(key)) list.Add(c);
            }
            return list;
        }

        /// <summary>画一张静态卡面（视觉与战斗中的 CardView 一致，但不带点击/悬停）。</summary>
        void CreateCardFace(RectTransform parent, CardData card)
        {
            var rt = UIFactory.CreateRect("Card_" + card.DisplayText(), parent);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.RoundedSoft;
            bg.type = Image.Type.Sliced;
            bg.color = GameConfig.CardBase;
            bg.raycastTarget = false;
            UIFactory.AddOutline(bg, GameConfig.SubText, new Vector2(2f, -2f));

            // 费用（左上）
            var cost = UIFactory.CreateText("Cost", rt, card.cost.ToString(), 28, GameConfig.Accent);
            UIFactory.Place(cost.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -40), new Vector2(44, -8));

            // 名字（顶部右侧；与大字重复时不显示，避免「诱导 / 诱导」堆叠）
            if (!string.IsNullOrEmpty(card.cardName) && card.cardName != card.DisplayText())
            {
                var name = UIFactory.CreateText("Name", rt, card.DisplayName(), 20,
                    card.upgraded ? GameConfig.UpgradedName : GameConfig.SubText);
                name.alignment = TextAlignmentOptions.Center;
                UIFactory.Place(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(46, -38), new Vector2(-8, -8));
            }

            // 大字（运算 + 目标范围）
            var label = UIFactory.CreateText("Label", rt, card.DisplayText(), 42, GameConfig.CardColor(card.op));
            label.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(8, -96), new Vector2(-8, -46));

            // 描述（下半区；字号自动缩放，长描述不会被截断。文本里的关键词由 KeywordCatalog 自动加粗）
            if (!string.IsNullOrEmpty(card.description))
            {
                var desc = UIFactory.CreateText("Desc", rt, KeywordCatalog.Highlight(card.description), 18, GameConfig.SubText);
                desc.alignment = TextAlignmentOptions.Center;
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 10;
                desc.fontSizeMax = 18;
                UIFactory.Place(desc.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(8, 12), new Vector2(-8, 116));
            }
        }
    }
}
