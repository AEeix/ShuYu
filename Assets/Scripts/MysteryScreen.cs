using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>地图问号：随机事件，看结果继续前进。</summary>
    public class MysteryScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("MysteryRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var gm = GameManager.Instance;

            string title, desc;
            switch (Random.Range(0, 3))
            {
                case 0:
                    title = "补给箱";
                    desc = "回复 6 点生命";
                    gm.Heal(6);
                    break;
                case 1:
                    {
                        title = "数字祭坛";
                        desc = "回复 3 点生命，并强化一张牌";
                        gm.Heal(3);
                        var c = gm.UpgradeRandomCard();
                        if (c != null) desc += "（「" + c.DisplayText() + "」）";
                        break;
                    }
                default:
                    {
                        title = "献祭";
                        desc = "扣 4 点生命，获得一张牌";
                        gm.Damage(4);
                        var added = gm.AddRandomCard();
                        if (added != null) desc += "（获得「" + added.DisplayText() + "」）";
                        break;
                    }
            }

            var titleText = UIFactory.CreateText("Title", _root, title, 64, GameConfig.Accent);
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -240), new Vector2(0, -120));

            var descText = UIFactory.CreateText("Desc", _root, desc, 40, GameConfig.SubText);
            descText.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(descText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -60), new Vector2(0, 60));

            var cont = UIFactory.CreateButton("Continue", _root, "继续前进", 44, GameConfig.Accent, GameConfig.Background, () => gm.ShowRoute());
            UIFactory.Place(cont, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 60), new Vector2(150, 140));
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
