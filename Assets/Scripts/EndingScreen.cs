using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>BOSS 通关结局屏：切除最后瘤块，一局结束。</summary>
    public class EndingScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("EndingRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "通 关", 144, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, 40), new Vector2(0, 220));

            // 气氛文案（占位，后续按碎片化叙事方向补真相揭示）
            var flavor = UIFactory.CreateText("Flavor", _root, "最后的瘤块被切除，棋盘归于寂静。", 36, GameConfig.SubText);
            UIFactory.Place(flavor.rectTransform,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -60), new Vector2(0, 0));

            int hp = GameManager.Instance.Player.health;
            var stat = UIFactory.CreateText("Stat", _root, "剩余生命 " + hp + " / " + GameConfig.InitialHealth, 48, GameConfig.Accent);
            UIFactory.Place(stat.rectTransform,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -140), new Vector2(0, -80));

            var again = UIFactory.CreateButton("Again", _root, "再来一局", 48,
                GameConfig.Accent, GameConfig.Background,
                () => GameManager.Instance.StartRun(GameManager.Instance.PlayerClass));
            UIFactory.Place(again,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230, -300), new Vector2(-20, -200));

            var toTitle = UIFactory.CreateButton("ToTitle", _root, "回到标题", 48,
                GameConfig.CardBase, GameConfig.SubText,
                () => GameManager.Instance.ShowTitle());
            UIFactory.Place(toTitle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(20, -300), new Vector2(230, -200));
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
