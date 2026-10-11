using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>标题界面：深蓝黑底 + 纯直角坐标系（原点左下、负轴微露、X 轴向右延长）+ 沿 Y 轴竖排菜单。</summary>
    public class TitleScreen : MonoBehaviour
    {
        // ---- 坐标系布局（原点在屏幕左下角）----
        const float OriginX = 150f;    // 原点 x
        const float OriginY = 150f;    // 原点 y
        const float NegLen = 100f;     // 负半轴露出长度

        RectTransform _root;
        RectTransform _settings;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("TitleRoot", canvas.transform);
            UIFactory.Stretch(_root);

            BuildMenu();
        }

        /// <summary>沿 Y 轴（x=0）从上往下：标题 → 开始新游戏 → 设置 → 退出游戏（整体贴近 X 轴）。</summary>
        void BuildMenu()
        {
            const float width = 360f;

            // 标题（最大）
            var title = UIFactory.CreateText("Title", _root, "数域", 144, GameConfig.Accent);
            title.alignment = TextAlignmentOptions.Left;
            PlaceAbs(title.rectTransform, OriginX + 60, 480, OriginX + width, 680);

            // 开始新游戏（第二大）
            var startBtn = UIFactory.CreateButton("StartButton", _root, "开始游戏", 48,
                GameConfig.Accent, GameConfig.Background,
                () => GameManager.Instance.ShowClassSelect());
            PlaceAbs(startBtn, OriginX + 60, 380, OriginX + width, 470);

            // 设置
            var settingsBtn = UIFactory.CreateButton("SettingsButton", _root, "设置", 36,
                GameConfig.CardBase, GameConfig.SubText, ShowSettings);
            PlaceAbs(settingsBtn, OriginX + 60, 300, OriginX + width, 370);

            // 退出游戏
            var quitBtn = UIFactory.CreateButton("QuitButton", _root, "退出游戏", 36,
                GameConfig.CardBase, GameConfig.SubText, QuitGame);
            PlaceAbs(quitBtn, OriginX + 60, 220, OriginX + width, 290);
        }

        /// <summary>打开设置面板（占位：内容待补）。</summary>
        void ShowSettings()
        {
            if (_settings != null) return;
            _settings = UIFactory.CreateRect("SettingsPanel", _root);
            UIFactory.Stretch(_settings);

            var mask = UIFactory.CreatePanel("Mask", _settings, new Color(0, 0, 0, 0.6f));
            UIFactory.Stretch(mask.rectTransform);

            var panel = UIFactory.CreatePanel("Panel", _settings, GameConfig.CardBase);
            UIFactory.Place(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320, -220), new Vector2(320, 220));

            var title = UIFactory.CreateText("SettingsTitle", panel.transform, "设 置", 60, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -100), new Vector2(0, -20));

            var hint = UIFactory.CreateText("SettingsHint", panel.transform,
                "设置项待补充（音量 / 画质等）", 36, GameConfig.SubText);
            UIFactory.Place(hint.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -200), new Vector2(0, -130));

            var back = UIFactory.CreateButton("BackButton", panel.transform, "返 回", 36,
                GameConfig.Accent, GameConfig.Background, HideSettings);
            UIFactory.Place(back,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 30), new Vector2(120, 100));
        }

        void HideSettings()
        {
            if (_settings != null) { Destroy(_settings.gameObject); _settings = null; }
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
