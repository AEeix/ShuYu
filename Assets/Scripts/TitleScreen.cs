using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>标题界面（运行时回退版）：主按钮按存档状态动态切换 + 开始新游戏 + 设置 + 退出。</summary>
    public class TitleScreen : MonoBehaviour
    {
        const float OriginX = 150f;

        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("TitleRoot", canvas.transform);
            UIFactory.Stretch(_root);

            BuildMenu();
        }

        void BuildMenu()
        {
            const float width = 360f;
            bool hasSave = SaveSystem.HasSave;
            float shift = hasSave ? 0f : 80f;   // 无存档：开始新游戏隐藏，下方按钮上移 80 补位

            var title = UIFactory.CreateText("Title", _root, "数域", 144, GameConfig.Accent);
            title.alignment = TextAlignmentOptions.Left;
            PlaceAbs(title.rectTransform, OriginX + 60, 640, OriginX + width, 820);

            // 主按钮：无存档「开始游戏」，有存档「继续游戏」
            var startBtn = UIFactory.CreateButton("StartButton", _root, hasSave ? "继续游戏" : "开始游戏", 48,
                GameConfig.Accent, GameConfig.Background,
                () => { if (hasSave) GameManager.Instance.ContinueGame(); else GameManager.Instance.ShowClassSelect(); });
            PlaceAbs(startBtn, OriginX + 60, 540, OriginX + width, 630);

            // 开始新游戏（有存档才显示）
            var newGameBtn = UIFactory.CreateButton("NewGameButton", _root, "开始新游戏", 40,
                GameConfig.CardBase, GameConfig.Accent, () => GameManager.Instance.ShowClassSelect());
            PlaceAbs(newGameBtn, OriginX + 60, 460, OriginX + width, 530);
            if (!hasSave) newGameBtn.gameObject.SetActive(false);

            // 设置
            var settingsBtn = UIFactory.CreateButton("SettingsButton", _root, "设置", 36,
                GameConfig.CardBase, GameConfig.SubText, () => GameManager.Instance.ShowSettings());
            PlaceAbs(settingsBtn, OriginX + 60, 380 + shift, OriginX + width, 450 + shift);

            // 卡牌图鉴（设置下面）
            var codexBtn = UIFactory.CreateButton("CodexButton", _root, "卡牌图鉴", 38,
                GameConfig.CardBase, GameConfig.Accent, () => GameManager.Instance.ShowCodex());
            PlaceAbs(codexBtn, OriginX + 60, 300 + shift, OriginX + width, 370 + shift);

            // 退出游戏
            var quitBtn = UIFactory.CreateButton("QuitButton", _root, "退出游戏", 36,
                GameConfig.CardBase, GameConfig.SubText, QuitGame);
            PlaceAbs(quitBtn, OriginX + 60, 220 + shift, OriginX + width, 290 + shift);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>绝对定位：锚点左下角 (0,0)，offset 即相对父左下的绝对坐标。</summary>
        static void PlaceAbs(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
