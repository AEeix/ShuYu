using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>职业选择界面：三职业卡片，点选即开局。静态框架可场景化，动态接线在运行时。</summary>
    public class ClassSelectScreen : MonoBehaviour
    {
        RectTransform _root;
        bool _ownsRoot = true; // 场景框架复用时不销毁；运行时回退时销毁

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm.ClassSelectRoot != null)
            {
                _ownsRoot = false;
                _root = gm.ClassSelectRoot.GetComponent<RectTransform>();
            }
            else
            {
                _ownsRoot = true;
                _root = BuildStaticFrame(UIFactory.EnsureCanvas().transform);
            }
            WireButtons();
        }

        void OnDestroy() { if (_ownsRoot && _root != null) Destroy(_root.gameObject); }

        /// <summary>建静态框架（标题 + 三张职业卡 + 返回），不接线。SceneBuilder 与运行时回退共用。</summary>
        public static RectTransform BuildStaticFrame(Transform parent)
        {
            var root = UIFactory.CreateRect("ClassSelectRoot", parent);
            UIFactory.Stretch(root);

            var title = UIFactory.CreateText("Title", root, "选择职业", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -200), new Vector2(0, -90));

            var classes = ClassCatalog.All();
            const float cardW = 840f;
            const float cardH = 160f;
            const float gap = 30f;
            float totalH = classes.Length * cardH + (classes.Length - 1) * gap;
            float topY = totalH / 2f;

            for (int i = 0; i < classes.Length; i++)
            {
                var cls = classes[i];
                float yCenter = topY - cardH / 2f - i * (cardH + gap);

                var cardRt = UIFactory.CreateRect("ClassCard_" + i, root);
                UIFactory.Place(cardRt,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-cardW / 2f, yCenter - cardH / 2f), new Vector2(cardW / 2f, yCenter + cardH / 2f));

                var img = cardRt.gameObject.AddComponent<Image>();
                img.color = GameConfig.CardBase;
                var btn = cardRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;

                var name = UIFactory.CreateText("Name", cardRt, cls.displayName, 56, GameConfig.Accent);
                UIFactory.Place(name.rectTransform,
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -110), new Vector2(0, -30));

                var desc = UIFactory.CreateText("Desc", cardRt, cls.description, 30, GameConfig.SubText);
                UIFactory.Place(desc.rectTransform,
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 25), new Vector2(0, 75));
            }

            var back = UIFactory.CreateButton("Back", root, "返回", 40,
                GameConfig.CardBase, GameConfig.SubText, null);
            UIFactory.Place(back,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 40), new Vector2(120, 120));

            return root;
        }

        /// <summary>按名字找框架里的按钮并接线（幂等，场景框架重复进入不会累加监听）。</summary>
        void WireButtons()
        {
            var classes = ClassCatalog.All();
            for (int i = 0; i < classes.Length; i++)
            {
                var t = _root.Find("ClassCard_" + i);
                if (t == null) continue;
                var c = classes[i];
                var btn = t.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => GameManager.Instance.StartRun(c));
            }
            var back = _root.Find("Back");
            if (back != null)
            {
                var btn = back.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => GameManager.Instance.ShowTitle());
            }
        }
    }
}
