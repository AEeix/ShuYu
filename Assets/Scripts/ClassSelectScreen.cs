using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>职业选择界面：三职业卡片，点选即开局。</summary>
    public class ClassSelectScreen : MonoBehaviour
    {
        RectTransform _root;

        void Start() { Build(); }
        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }

        void Build()
        {
            var canvas = UIFactory.EnsureCanvas();
            var go = new GameObject("ClassSelectRoot", typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            UIFactory.Stretch(_root);

            var title = UIFactory.CreateText("Title", _root, "选择职业", 72, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -200), new Vector2(0, -90));

            var classes = ClassCatalog.All();
            const float cardW = 840f;
            const float cardH = 160f;
            const float gap = 30f;
            float totalH = classes.Length * cardH + (classes.Length - 1) * gap;
            float topY = totalH / 2f;   // 相对屏幕中心

            for (int i = 0; i < classes.Length; i++)
            {
                var cls = classes[i];
                float yCenter = topY - cardH / 2f - i * (cardH + gap);

                var cardRt = UIFactory.CreateRect("Class_" + cls.id, _root);
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

                var c = cls;
                btn.onClick.AddListener(() => GameManager.Instance.StartRun(c));
            }

            var back = UIFactory.CreateButton("Back", _root, "返回", 40,
                GameConfig.CardBase, GameConfig.SubText, () => GameManager.Instance.ShowTitle());
            UIFactory.Place(back,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 40), new Vector2(120, 120));
        }
    }
}
