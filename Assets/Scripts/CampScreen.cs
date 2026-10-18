using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>火堆：休息回血 或 强化一张随机牌，选完继续前进。</summary>
    public class CampScreen : MonoBehaviour
    {
        RectTransform _root;
        TMP_Text _result;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("CampRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "火 堆", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -70));

            _result = UIFactory.CreateText("Result", _root, "", 40, GameConfig.SubText);
            _result.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(_result.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -40), new Vector2(0, 60));

            var rest = UIFactory.CreateButton("Rest", _root, "休息（回 5 血）", 44, GameConfig.CardGold, GameConfig.Background, OnRest);
            UIFactory.Place(rest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -200), new Vector2(-30, -100));

            var upgrade = UIFactory.CreateButton("Upgrade", _root, "强化一张牌", 44, GameConfig.CardBase, GameConfig.Accent, OnUpgrade);
            UIFactory.Place(upgrade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, -200), new Vector2(300, -100));

            var leave = UIFactory.CreateButton("Leave", _root, "继续前进", 44, GameConfig.GridLine, GameConfig.SubText, () => GameManager.Instance.ShowRoute());
            UIFactory.Place(leave, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 40), new Vector2(150, 120));
        }

        void OnRest()
        {
            GameManager.Instance.Heal(5);
            _result.text = "休息好了，回复 5 点生命";
            HideChoices();
        }

        void OnUpgrade()
        {
            var card = GameManager.Instance.UpgradeRandomCard();
            _result.text = card != null
                ? "强化了「" + card.DisplayText() + "」"
                : "没有可强化的牌";
            HideChoices();
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
