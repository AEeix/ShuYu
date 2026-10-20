using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>火堆：休息回血 或 强化一张自选牌，选完继续前进。</summary>
    public class CampScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _select;    // 选牌强化界面（进入选择时创建）
        TMP_Text _result;
        TMP_Text _hpText;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("CampRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "火 堆", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -70));

            _hpText = UIFactory.CreateText("HP", _root, "HP：" + GameManager.Instance.Player.health, 44, GameConfig.Accent);
            UIFactory.Place(_hpText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-300, -130), new Vector2(-60, -80));

            _result = UIFactory.CreateText("Result", _root, "", 40, GameConfig.SubText);
            _result.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(_result.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -40), new Vector2(0, 60));

            var rest = UIFactory.CreateButton("Rest", _root, "休息（回复 30% 生命）", 44, GameConfig.CardGold, GameConfig.Background, OnRest);
            UIFactory.Place(rest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -200), new Vector2(-30, -100));

            var upgrade = UIFactory.CreateButton("Upgrade", _root, "强化一张牌", 44, GameConfig.CardBase, GameConfig.Accent, OnUpgrade);
            UIFactory.Place(upgrade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, -200), new Vector2(300, -100));

            var leave = UIFactory.CreateButton("Leave", _root, "继续前进", 44, GameConfig.GridLine, GameConfig.SubText, () => GameManager.Instance.ShowRoute());
            UIFactory.Place(leave, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 40), new Vector2(150, 120));
        }

        void OnRest()
        {
            var gm = GameManager.Instance;
            int amount = Mathf.CeilToInt(gm.Player.maxHealth * 0.3f);
            gm.Heal(amount);
            _result.text = "休息好了，回复 " + amount + " 点生命";
            RefreshHP();
            HideChoices();
        }

        void RefreshHP()
        {
            if (_hpText != null) _hpText.text = "HP：" + GameManager.Instance.Player.health;
        }

        // ===== 选择强化 =====

        void OnUpgrade()
        {
            var gm = GameManager.Instance;
            var cards = gm.DeckCards;
            if (cards.Count == 0)
            {
                _result.text = "没有可强化的牌";
                return;
            }
            HideChoices();
            var leave = _root.Find("Leave");
            if (leave != null) leave.gameObject.SetActive(false);
            ShowUpgradeSelect(cards);
        }

        void ShowUpgradeSelect(List<CardData> cards)
        {
            _select = UIFactory.CreateRect("UpgradeSelect", _root);
            UIFactory.Stretch(_select);

            var hint = UIFactory.CreateText("Hint", _select, "选择要强化的牌", 48, GameConfig.Accent);
            UIFactory.Place(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -70));

            BuildCardGrid(_select, cards, OnPickUpgrade);

            var cancel = UIFactory.CreateButton("Cancel", _select, "取消", 36, GameConfig.GridLine, GameConfig.SubText, CancelUpgrade);
            UIFactory.Place(cancel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 30), new Vector2(120, 90));
        }

        void OnPickUpgrade(CardData card)
        {
            var gm = GameManager.Instance;
            gm.UpgradeCard(card);
            _result.text = "强化了「" + card.DisplayText() + "」";
            CloseSelect();
            var leave = _root.Find("Leave");
            if (leave != null) leave.gameObject.SetActive(true);
        }

        void CancelUpgrade()
        {
            CloseSelect();
            var rest = _root.Find("Rest");
            var upgrade = _root.Find("Upgrade");
            var leave = _root.Find("Leave");
            if (rest != null) rest.gameObject.SetActive(true);
            if (upgrade != null) upgrade.gameObject.SetActive(true);
            if (leave != null) leave.gameObject.SetActive(true);
        }

        void CloseSelect()
        {
            if (_select != null) { Destroy(_select.gameObject); _select = null; }
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

        void HideChoices()
        {
            var rest = _root.Find("Rest");
            var upgrade = _root.Find("Upgrade");
            if (rest != null) rest.gameObject.SetActive(false);
            if (upgrade != null) upgrade.gameObject.SetActive(false);
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
