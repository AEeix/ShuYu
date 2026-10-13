using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>战斗胜利战利品：三张卡横排，点选一张加入卡组（可跳过），选完进路线。</summary>
    public class CardRewardScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("RewardRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "战利品 · 选一张牌", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -190), new Vector2(0, -70));

            var choices = CardLibrary.RewardChoices(3);

            const float cardW = 220f;
            const float cardH = 320f;
            const float gap = 60f;
            float totalW = choices.Count * cardW + (choices.Count - 1) * gap;
            float startX = -totalW / 2f;

            for (int i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                float centerX = startX + cardW / 2f + i * (cardW + gap);

                var cardRt = UIFactory.CreateRect("Card_" + card.DisplayText(), _root);
                UIFactory.Place(cardRt,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(centerX - cardW / 2f, -cardH / 2f - 30f), new Vector2(centerX + cardW / 2f, cardH / 2f - 30f));

                var img = cardRt.gameObject.AddComponent<Image>();
                img.sprite = UIFactory.Rounded;
                img.type = Image.Type.Sliced;
                img.color = GameConfig.CardBase;
                UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

                var btn = cardRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;

                var label = UIFactory.CreateText("Label", cardRt, card.DisplayText(), 96, GameConfig.CardColor(card.op));
                UIFactory.Stretch(label.rectTransform);

                var cost = UIFactory.CreateText("Cost", cardRt, card.cost.ToString(), 40, GameConfig.Accent);
                UIFactory.Place(cost.rectTransform,
                    new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(10, -52), new Vector2(60, -10));

                var c = card;
                btn.onClick.AddListener(() => GameManager.Instance.TakeRewardCard(c));
            }

            var skip = UIFactory.CreateButton("Skip", _root, "跳过", 40,
                GameConfig.CardBase, GameConfig.SubText, () => GameManager.Instance.ShowRoute());
            UIFactory.Place(skip,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 60), new Vector2(120, 140));
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
