using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>单张卡牌的视图：渲染 + 悬停抬起/倾斜 + 选中弹跳 + 抽牌弹入 + 出牌飞出。</summary>
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public RectTransform Rect => (RectTransform)transform;
        public CardData Card { get; private set; }

        Image _bg;
        TMP_Text _label;
        TMP_Text _desc;
        TMP_Text _name;
        CanvasGroup _group;
        Outline _outline;   // 卡牌描边（附魔特效用）
        bool _enchanted;    // 是否处于附魔特效（下一张攻击带灼烧）
        Vector2 _shakeOffset; // 出牌失败（能量不足）抖动偏移
        Vector2 _dealOffset;  // 抽牌飞入的位移（从下方飞入手牌，播完归零）
        Action<CardData> _onClick;
        Camera _cam;

        bool _selected;
        bool _hovered;
        bool _animating;     // 出牌飞出时暂停悬停跟随
        float _tiltDeg;      // 目标倾斜角（跟随鼠标）
        float _lift;         // 当前抬起量
        float _curTilt;      // 当前倾斜角（平滑）
        Vector2 _basePos;    // 无抬起时的基准位置
        readonly Vector3[] _corners = new Vector3[4];

        const float LiftPx = 26f;       // 抬起高度
        const float MaxTiltDeg = 12f;   // 最大倾斜角
        const float Smooth = 16f;       // 平滑跟随速度
        const float HoverSlop = 4f;     // 悬停判定的宽容边（防抖）

        public void Setup(CardData card, Action<CardData> onClick, bool selected)
        {
            Card = card;
            _onClick = onClick;
            _selected = selected;
            _cam = GetComponentInParent<Canvas>()?.worldCamera;

            _group = gameObject.AddComponent<CanvasGroup>();

            _bg = gameObject.AddComponent<Image>();
            _bg.sprite = UIFactory.RoundedSoft;   // 稍微圆润的卡面
            _bg.type = Image.Type.Sliced;
            _bg.color = selected ? GameConfig.Accent : GameConfig.CardBase;
            UIFactory.AddOutline(_bg, GameConfig.SubText, new Vector2(3f, -3f)); // 加粗边框
            _outline = _bg.GetComponent<Outline>();

            var btn = gameObject.AddComponent<Button>();
            btn.targetGraphic = _bg;
            var c = card;
            btn.onClick.AddListener(() => _onClick(c));

            _label = UIFactory.CreateText("Label", Rect, card.DisplayText(), 64,
                selected ? GameConfig.Background : GameConfig.CardColor(card.op));
            _label.alignment = TextAlignmentOptions.Center;
            // 大字不一定只有一个数字了（「+2 cost」「+1 -5」「蓄力」），自动缩字号，
            // 宁可小一点也不让字溢出卡面；文字本来就短时仍是 64 号，视觉不变。
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 24;
            _label.fontSizeMax = 64;
            UIFactory.Place(_label.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10, -150), new Vector2(-10, -55));

            // 卡牌名字（顶部小字）。就算和卡面大字完全一样也照画——
            // 费用右边那格小字是玩家认牌的位置，空着看着像漏了东西（用户 2026-10-21 反馈）
            if (!string.IsNullOrEmpty(card.cardName))
            {
                _name = UIFactory.CreateText("Name", Rect, card.DisplayName(), 26,
                    selected ? GameConfig.Background : (card.upgraded ? GameConfig.UpgradedName : GameConfig.SubText));
                _name.alignment = TextAlignmentOptions.Center;
                UIFactory.Place(_name.rectTransform,
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(10, -55), new Vector2(-10, -15));
            }

            // 描述（职业专属卡有，基础卡没有）。字号自动缩放，长描述（如菌种培育三行）不会被截断。
            if (!string.IsNullOrEmpty(card.description))
            {
                _desc = UIFactory.CreateText("Desc", Rect, KeywordCatalog.Highlight(card.description), 28,
                    selected ? GameConfig.Background : GameConfig.SubText);
                _desc.alignment = TextAlignmentOptions.Center;
                _desc.enableAutoSizing = true;
                _desc.fontSizeMin = 16;
                _desc.fontSizeMax = 28;
                UIFactory.Place(_desc.rectTransform,
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(10, 20), new Vector2(-10, 128));
            }

            var cost = UIFactory.CreateText("Cost", Rect, card.cost.ToString(), 40, GameConfig.Accent);
            UIFactory.Place(cost.rectTransform,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, -52), new Vector2(60, -10));

            _basePos = Rect.anchoredPosition;
        }

        public void SetSelected(bool selected)
        {
            bool changed = _selected != selected;
            _selected = selected;
            _bg.color = selected ? GameConfig.Accent : GameConfig.CardBase;
            _label.color = selected ? GameConfig.Background : GameConfig.CardColor(Card.op);
            if (_desc != null) _desc.color = selected ? GameConfig.Background : GameConfig.SubText;
            if (_name != null) _name.color = selected ? GameConfig.Background : (Card.upgraded ? GameConfig.UpgradedName : GameConfig.SubText);
            if (changed && selected) StartCoroutine(Pop());
        }

        /// <summary>附魔特效：下一张攻击带灼烧时，边框脉冲火焰色；取消则还原灰色。</summary>
        public void SetEnchanted(bool enchanted)
        {
            _enchanted = enchanted;
            if (_outline == null) return;
            if (!enchanted) _outline.effectColor = GameConfig.SubText;
        }

        public void PlayDeal(float delay) { StartCoroutine(DealAnim(delay)); }

        public void PlayCard(float duration, Action onComplete) { StartCoroutine(PlayCardAnim(duration, onComplete)); }

        /// <summary>能量不足无法出牌：卡片抖动 + 红闪。</summary>
        public void PlayCantAfford()
        {
            StartCoroutine(CantAffordAnim());
        }

        IEnumerator CantAffordAnim()
        {
            var flash = UIFactory.CreatePanel("CantAfford", Rect, new Color(1f, 0.2f, 0.15f, 0.45f));
            flash.raycastTarget = false;
            UIFactory.Stretch(flash.rectTransform);
            flash.rectTransform.SetAsFirstSibling();

            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur;
                _shakeOffset = new Vector2(Mathf.Sin(t * 45f) * 8f * k, 0f);
                Color c = flash.color; c.a = 0.45f * k; flash.color = c;
                yield return null;
            }
            _shakeOffset = Vector2.zero;
            Destroy(flash.gameObject);
        }

        IEnumerator DealAnim(float delay)
        {
            Rect.localScale = Vector3.zero;
            _dealOffset = new Vector2(0f, -170f);   // 起始在下方（抽牌堆方向）
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            const float dur = 0.32f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float ease = 1f - (1f - k) * (1f - k);   // easeOutQuad：先快后慢飞入
                Rect.localScale = Vector3.one * Overshoot(k);
                _dealOffset = new Vector2(0f, -170f * (1f - ease));
                yield return null;
            }
            Rect.localScale = Vector3.one;
            _dealOffset = Vector2.zero;
        }

        IEnumerator Pop()
        {
            float t = 0f;
            const float dur = 0.16f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                Rect.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            Rect.localScale = Vector3.one;
        }

        IEnumerator PlayCardAnim(float duration, Action onComplete)
        {
            _animating = true;
            Vector2 from = Rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                Rect.localScale = Vector3.one * (1f + 0.6f * k);
                Rect.anchoredPosition = from + new Vector2(0f, 90f * k);
                _group.alpha = 1f - k;
                yield return null;
            }
            _group.alpha = 0f;
            onComplete?.Invoke();
        }

        static float Overshoot(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            // 悬停状态改由 Update 里的位置判定驱动（见 UpdateHoverByPointer），这里不再改状态
        }

        public void OnPointerExit(PointerEventData e)
        {
            // 同上：Unity 的 Exit 会因为卡牌抬起而误触发（判定区跟着走），不能用来放下卡牌
        }

        public void OnPointerMove(PointerEventData e)
        {
            if (_cam == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, _cam, out Vector2 local);
            float halfW = Rect.rect.width * 0.5f;
            _tiltDeg = Mathf.Clamp(local.x / halfW, -1f, 1f) * MaxTiltDeg;
        }

        /// <summary>
        /// 悬停判定（修「鼠标停在卡片下侧时卡牌反复抽搐」的 bug）。
        /// 卡牌抬起后自身矩形也上移，如果按**当前**矩形判定：鼠标在下侧边缘 → 判定区已经抬走 → 判为离开 →
        /// 卡牌落下 → 鼠标又回到卡牌上 → 判为悬停 → 抬起 …… 每帧来回抖。
        /// 这里改成按**未抬起时的基准矩形**判定（当前包围盒还原掉 _lift），判定区固定不动，抖动消失。
        /// </summary>
        void UpdateHoverByPointer()
        {
            if (_cam == null) return;
            var parent = Rect.parent as RectTransform;
            if (parent == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, _cam, out Vector2 p))
            {
                SetHovered(false);
                return;
            }

            Rect.GetWorldCorners(_corners);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 lp = parent.InverseTransformPoint(_corners[i]);
                if (lp.x < minX) minX = lp.x;
                if (lp.x > maxX) maxX = lp.x;
                if (lp.y < minY) minY = lp.y;
                if (lp.y > maxY) maxY = lp.y;
            }
            minY -= _lift;   // 还原到未抬起的位置 → 得到固定不动的判定区
            maxY -= _lift;

            bool inside = p.x >= minX - HoverSlop && p.x <= maxX + HoverSlop
                       && p.y >= minY - HoverSlop && p.y <= maxY + HoverSlop;
            SetHovered(inside);
        }

        void SetHovered(bool hovered)
        {
            if (hovered == _hovered) return;
            _hovered = hovered;
            if (hovered) SfxPlayer.Instance?.Play(SfxType.Hover);
            else _tiltDeg = 0f;
        }

        void Update()
        {
            if (_animating) return;
            UpdateHoverByPointer();
            float targetLift = (_hovered || _selected) ? LiftPx : 0f;
            float targetTilt = _hovered ? _tiltDeg : 0f;
            _lift = Mathf.Lerp(_lift, targetLift, Time.deltaTime * Smooth);
            _curTilt = Mathf.Lerp(_curTilt, targetTilt, Time.deltaTime * Smooth);
            Rect.anchoredPosition = _basePos + new Vector2(0f, _lift) + _shakeOffset + _dealOffset;
            Rect.localRotation = Quaternion.Euler(0f, 0f, _curTilt);

            // 附魔特效：火焰边框脉冲（深橙红 ↔ 亮橙黄）
            if (_enchanted && _outline != null)
            {
                float k = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                _outline.effectColor = Color.Lerp(new Color(1f, 0.3f, 0.1f), new Color(1f, 0.8f, 0.2f), k);
            }
        }
    }
}
