using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>精英胜利：获得遗物。</summary>
    public class RelicRewardScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("RelicRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var gm = GameManager.Instance;
            var relic = gm.AddRandomRelic();

            string title = "获得遗物";
            string desc = relic != null ? "「" + relic.name + "」：" + relic.description : "没有新遗物了";

            var titleText = UIFactory.CreateText("Title", _root, title, 64, GameConfig.Accent);
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -240), new Vector2(0, -120));

            var descText = UIFactory.CreateText("Desc", _root, desc, 44, GameConfig.CardGold);
            descText.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(descText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -60), new Vector2(0, 60));

            var cont = UIFactory.CreateButton("Continue", _root, "继续前进", 44, GameConfig.Accent, GameConfig.Background, () => gm.ShowRoute());
            UIFactory.Place(cont, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 60), new Vector2(150, 140));
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
