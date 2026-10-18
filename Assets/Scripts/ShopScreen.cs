using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>商店：花血条数字买牌 / 删牌（血也是货币）。</summary>
    public class ShopScreen : MonoBehaviour
    {
        RectTransform _root;
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

            // 3 张可买卡（每张 4 血）
            var choices = CardLibrary.RewardChoices(3, gm.PlayerClass.passive);
            const float cardW = 200f, gap = 40f;
            float totalW = choices.Count * cardW + (choices.Count - 1) * gap;
            float startX = -totalW / 2f;
            for (int i = 0; i < choices.Count; i++)
            {
                float cx = startX + cardW / 2f + i * (cardW + gap);
                CreateShopCard(choices[i], cx, 4);
            }

            var remove = UIFactory.CreateButton("Remove", _root, "删一张牌（3血）", 36, GameConfig.CardBase, GameConfig.SubText, OnRemove);
            UIFactory.Place(remove, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-240, 40), new Vector2(-30, 100));

            var leave = UIFactory.CreateButton("Leave", _root, "离开", 36, GameConfig.GridLine, GameConfig.SubText, () => gm.ShowRoute());
            UIFactory.Place(leave, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(30, 40), new Vector2(240, 100));
        }

        void CreateShopCard(CardData card, float centerX, int cost)
        {
            var gm = GameManager.Instance;
            const float cardW = 200f, cardH = 240f;

            var rt = UIFactory.CreateRect("Buy_" + card.DisplayText(), _root);
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(centerX - cardW / 2f, -cardH / 2f - 60f), new Vector2(centerX + cardW / 2f, cardH / 2f - 60f));

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.type = Image.Type.Sliced;
            img.color = GameConfig.CardBase;
            UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            var label = UIFactory.CreateText("Label", rt, card.DisplayText(), 72, GameConfig.CardColor(card.op));
            UIFactory.Stretch(label.rectTransform);

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

        void OnRemove()
        {
            var gm = GameManager.Instance;
            if (gm.SpendHealth(3))
            {
                var card = gm.RemoveRandomCard();
                _result.text = card != null ? "删掉了「" + card.DisplayText() + "」" : "没有牌可删";
                RefreshHP();
            }
            else
                _result.text = "血量不够";
        }

        void RefreshHP()
        {
            if (_hpText != null) _hpText.text = "HP：" + GameManager.Instance.Player.health;
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
