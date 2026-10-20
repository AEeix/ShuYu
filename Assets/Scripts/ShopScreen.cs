using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>商店：花血条数字买牌 / 删牌（血也是货币）。删牌由玩家自选一张，不再随机。</summary>
    public class ShopScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _menu;      // 主菜单：买卡 + 删牌 + 离开
        RectTransform _select;    // 选牌删牌界面（进入选择时创建）
        TMP_Text _hpText;
        TMP_Text _result;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("ShopRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var gm = GameManager.Instance;

            var title = UIFactory.CreateText("Title", _root, "商 店", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -50));

            _hpText = UIFactory.CreateText("HP", _root, "HP：" + gm.Player.health, 44, GameConfig.Accent);
            UIFactory.Place(_hpText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-300, -130), new Vector2(-60, -80));

            _result = UIFactory.CreateText("Result", _root, "", 36, GameConfig.SubText);
            _result.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(_result.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 100), new Vector2(0, 180));

            _menu = UIFactory.CreateRect("Menu", _root);
            UIFactory.Stretch(_menu);
            BuildMenu();
        }

        void BuildMenu()
        {
            var gm = GameManager.Instance;

            // 3 张可买卡（每张 4 血）
            var choices = CardLibrary.RewardChoices(3, gm.PlayerClass.passive, gm.PlayerClass.tag);
            const float cardW = 200f, gap = 40f;
            float totalW = choices.Count * cardW + (choices.Count - 1) * gap;
            float startX = -totalW / 2f;
            for (int i = 0; i < choices.Count; i++)
            {
                float cx = startX + cardW / 2f + i * (cardW + gap);
                CreateShopCard(choices[i], cx, 4);
            }

            var remove = UIFactory.CreateButton("Remove", _menu, "删一张牌（3血）", 36, GameConfig.CardBase, GameConfig.SubText, OnRemove);
            UIFactory.Place(remove, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-240, 40), new Vector2(-30, 100));

            var leave = UIFactory.CreateButton("Leave", _menu, "离开", 36, GameConfig.GridLine, GameConfig.SubText, () => gm.ShowRoute());
            UIFactory.Place(leave, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(30, 40), new Vector2(240, 100));
        }

        void CreateShopCard(CardData card, float centerX, int cost)
        {
            var gm = GameManager.Instance;
            const float cardW = 200f, cardH = 240f;

            var rt = UIFactory.CreateRect("Buy_" + card.DisplayText(), _menu);
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(centerX - cardW / 2f, -cardH / 2f - 60f), new Vector2(centerX + cardW / 2f, cardH / 2f - 60f));

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.type = Image.Type.Sliced;
            img.color = GameConfig.CardBase;
            UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            // 名字（顶部）。与大字相同也照画（费用右边那格小字不能空 —— 用户 2026-10-21 反馈）
            if (!string.IsNullOrEmpty(card.cardName))
            {
                var name = UIFactory.CreateText("Name", rt, card.DisplayName(), 26,
                    card.upgraded ? GameConfig.UpgradedName : GameConfig.SubText);
                name.alignment = TextAlignmentOptions.Center;
                UIFactory.Place(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(10, -48), new Vector2(-10, -12));
            }

            var label = UIFactory.CreateText("Label", rt, card.DisplayText(), 64, GameConfig.CardColor(card.op));
            // 大字可能比一个数值长（「+2 cost」「+1 -5」）→ 自动缩字号防溢出
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 64;
            UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10, -160), new Vector2(-10, -52));

            var price = UIFactory.CreateText("Price", rt, cost + "血", 36, GameConfig.Accent);
            UIFactory.Place(price.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(0, 62));

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = card;
            btn.onClick.AddListener(() =>
            {
                if (gm.BuyCard(c, cost))
                {
                    _result.text = "买下「" + c.DisplayText() + "」";
                    RefreshHP();
                    Destroy(rt.gameObject);
                }
                else
                    _result.text = "血量不够";
            });
        }

        // ===== 选择删牌 =====

        void OnRemove()
        {
            var gm = GameManager.Instance;
            var cards = gm.DeckCards;
            if (cards.Count == 0)
            {
                _result.text = "没有牌可删";
                return;
            }
            _menu.gameObject.SetActive(false);
            ShowRemoveSelect(cards);
        }

        void ShowRemoveSelect(List<CardData> cards)
        {
            _select = UIFactory.CreateRect("RemoveSelect", _root);
            UIFactory.Stretch(_select);

            var hint = UIFactory.CreateText("Hint", _select, "选择要删的一张牌", 48, GameConfig.Accent);
            UIFactory.Place(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -70));

            BuildCardGrid(_select, cards, OnPickRemove);

            var cancel = UIFactory.CreateButton("Cancel", _select, "取消", 36, GameConfig.GridLine, GameConfig.SubText, CloseRemoveSelect);
            UIFactory.Place(cancel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 30), new Vector2(120, 90));
        }

        void OnPickRemove(CardData card)
        {
            var gm = GameManager.Instance;
            if (gm.SpendHealth(3))
            {
                gm.RemoveCard(card);
                _result.text = "删掉了「" + card.DisplayText() + "」";
            }
            else
                _result.text = "血量不够";
            RefreshHP();
            CloseRemoveSelect();
        }

        void CloseRemoveSelect()
        {
            if (_select != null) { Destroy(_select.gameObject); _select = null; }
            if (_menu != null) _menu.gameObject.SetActive(true);
        }

        /// <summary>卡组所有牌按网格铺开（每行 5 张），点选走 onPick。</summary>
        void BuildCardGrid(RectTransform parent, List<CardData> cards, UnityAction<CardData> onPick)
        {
            const float cardW = 190f, cardH = 235f, gap = 26f;
            const int perRow = 5;
            int rows = Mathf.CeilToInt(cards.Count / (float)perRow);
            float totalW = perRow * cardW + (perRow - 1) * gap;
            float totalH = rows * cardH + (rows - 1) * gap;
            float startX = -totalW / 2f;
            float topY = totalH / 2f - 20f;   // 整体略上移，给底部按钮留空间

            for (int i = 0; i < cards.Count; i++)
            {
                int r = i / perRow, c = i % perRow;
                float cx = startX + cardW / 2f + c * (cardW + gap);
                float cy = topY - cardH / 2f - r * (cardH + gap);
                var cardRt = UIFactory.CreateCardButton(parent, "Card_" + cards[i].DisplayText(),
                    cards[i], new Vector2(cardW, cardH), onPick);
                cardRt.anchoredPosition = new Vector2(cx, cy);
            }
        }

        void RefreshHP()
        {
            if (_hpText != null) _hpText.text = "HP：" + GameManager.Instance.Player.health;
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
