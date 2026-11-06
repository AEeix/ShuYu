using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 图鉴占位屏：遗物图鉴 / 怪物图鉴还没做内容时先由它占住入口，
    /// 这样「标题主页 → 图鉴主页 → 四个子页 → 返回」的骨架从第一天就是通的，
    /// 以后填内容只需要替换这一屏的实现（<see cref="GameManager.ShowRelicCodex"/> 那一行），
    /// 不用再去动图鉴主页和跳转逻辑。
    /// </summary>
    public class CodexPlaceholderScreen : MonoBehaviour
    {
        RectTransform _root;
        string _title = "图 鉴";
        string _body = "占位";
        string _note = "";

        /// <summary>
        /// 进入前填内容（MonoBehaviour 不能带参构造，所以用「AddComponent 后立刻 Setup」的写法，
        /// Setup 在 Start 之前调用，内容来得及生效）。
        /// </summary>
        /// <param name="title">顶部大标题，如「遗 物 图 鉴」。</param>
        /// <param name="body">中间说明：这一屏以后放什么。</param>
        /// <param name="note">底部小字：数据从哪个文件来（方便以后接手的人直接去填）。</param>
        public void Setup(string title, string body, string note)
        {
            _title = title;
            _body = body;
            _note = note;
        }

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("CodexPlaceholderRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, _title, 76, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(0, 940), new Vector2(1920, 1040));

            // 中间一块卡片样式的占位牌（和关键词图鉴的卡片同一套视觉）
            var panel = UIFactory.CreatePanel("Panel", _root, GameConfig.CardBase);
            panel.sprite = UIFactory.Rounded;
            panel.type = Image.Type.Sliced;
            UIFactory.AddOutline(panel, GameConfig.SubText, new Vector2(3f, -3f));
            UIFactory.Place(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-560, -230), new Vector2(560, 230));

            var body = UIFactory.CreateText("Body", panel.rectTransform, _body, 34, GameConfig.SubText);
            body.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(body.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(40, 96), new Vector2(-40, -30));

            if (!string.IsNullOrEmpty(_note))
            {
                var note = UIFactory.CreateText("Note", panel.rectTransform, _note, 24, GameConfig.CardGold);
                note.alignment = TextAlignmentOptions.Center;
                UIFactory.Place(note.rectTransform, Vector2.zero, new Vector2(1, 0),
                    new Vector2(40, 28), new Vector2(-40, 88));
            }

            var back = UIFactory.CreateButton("Back", _root, "返 回", 44,
                GameConfig.Accent, GameConfig.Background, () => GameManager.Instance.ShowCodexHome());
            UIFactory.Place(back, Vector2.zero, Vector2.zero,
                new Vector2(860, 40), new Vector2(1060, 120));
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
