using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 地图问号：随机剧情事件。每个事件有一段前置剧情 + 两个选项（「做」或「离开」），
    /// 选项括号里标注后果。选完显示结果，继续前进。
    /// </summary>
    public class MysteryScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _choicesRoot;   // 选项区（选完后销毁）

        /// <summary>一个选项：按钮文本 + 执行效果（返回结果描述）。</summary>
        class MysteryChoice
        {
            public string text;
            public Func<string> apply;
        }

        /// <summary>一个事件：标题 + 剧情 + 选项列表。</summary>
        class MysteryEvent
        {
            public string title;
            public string story;
            public List<MysteryChoice> choices = new List<MysteryChoice>();
        }

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("MysteryRoot", canvas.transform);
            UIFactory.Stretch(_root);

            var ev = BuildEvent(UnityEngine.Random.Range(0, 6));

            var titleText = UIFactory.CreateText("Title", _root, ev.title, 64, GameConfig.Accent);
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -220), new Vector2(0, -100));

            var storyText = UIFactory.CreateText("Story", _root, ev.story, 36, GameConfig.SubText);
            storyText.alignment = TextAlignmentOptions.Center;
            storyText.enableAutoSizing = true;
            storyText.fontSizeMin = 22;
            storyText.fontSizeMax = 36;
            UIFactory.Place(storyText.rectTransform, new Vector2(0, 0.6f), new Vector2(1, 0.85f), new Vector2(60, 0), new Vector2(-60, 0));

            _choicesRoot = UIFactory.CreateRect("Choices", _root);
            UIFactory.Stretch(_choicesRoot);
            BuildChoices(_choicesRoot, ev);
        }

        /// <summary>垂直排列选项按钮，点选后结算。</summary>
        void BuildChoices(RectTransform parent, MysteryEvent ev)
        {
            const float btnW = 1000f, btnH = 90f, gap = 30f;
            float totalH = ev.choices.Count * btnH + (ev.choices.Count - 1) * gap;
            for (int i = 0; i < ev.choices.Count; i++)
            {
                var ch = ev.choices[i];
                var btn = UIFactory.CreateButton("Choice" + i, parent, ch.text, 34, GameConfig.CardBase, GameConfig.SubText, null);
                btn.GetComponent<Button>().onClick.AddListener(() => OnChoose(ch));
                float cy = totalH / 2f - btnH / 2f - i * (btnH + gap);
                UIFactory.Place(btn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-btnW / 2f, cy - btnH / 2f), new Vector2(btnW / 2f, cy + btnH / 2f));
            }
        }

        /// <summary>点选一个选项：执行效果 → 隐藏选项 → 显示结果 + 继续按钮。</summary>
        void OnChoose(MysteryChoice ch)
        {
            var gm = GameManager.Instance;
            string result = ch.apply();

            if (_choicesRoot != null) { Destroy(_choicesRoot.gameObject); _choicesRoot = null; }

            // 扣血选项可能致死：血量归零即清档回标题
            bool dead = gm.Player.health <= 0;
            if (dead) { gm.Player.health = 0; SaveSystem.Clear(); result += "\n你失去了所有生命。"; }

            var resultText = UIFactory.CreateText("Result", _root, result, 40, GameConfig.SubText);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 22;
            resultText.fontSizeMax = 40;
            UIFactory.Place(resultText.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 0.65f), new Vector2(60, 0), new Vector2(-60, 0));

            var cont = UIFactory.CreateButton("Continue", _root, dead ? "回到标题" : "继续前进", 44,
                GameConfig.Accent, GameConfig.Background, () => { if (dead) gm.ShowTitle(); else gm.ShowRoute(); });
            UIFactory.Place(cont, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 60), new Vector2(150, 140));
        }

        /// <summary>构建一个事件（标题 + 剧情 + 选项）。</summary>
        MysteryEvent BuildEvent(int id)
        {
            var gm = GameManager.Instance;
            var ev = new MysteryEvent();
            switch (id)
            {
                case 0:   // 旅行者的帐篷
                    ev.title = "旅行者的帐篷";
                    ev.story = "你拨开灌木，发现一顶废弃的帐篷，是此前某个旅行者留下的。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "翻找补给（回复 6 点生命）",
                        apply = () => { gm.Heal(6); return "你在帐篷里找到了干粮和药膏，回复 6 点生命。"; }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "继续赶路（什么都不会发生）",
                        apply = () => "你绕开帐篷，继续赶路。"
                    });
                    break;

                case 1:   // 数字祭坛
                    ev.title = "数字祭坛";
                    ev.story = "一座刻满数字的石祭坛矗立在路中央，微光沿着刻痕流转。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "触碰祭坛（回复 3 点生命，并强化一张牌）",
                        apply = () =>
                        {
                            gm.Heal(3);
                            var c = gm.UpgradeRandomCard();
                            return c != null ? "祭坛回应了你，回复 3 点生命，强化了「" + c.DisplayText() + "」。" : "祭坛回应了你，回复 3 点生命。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "绕开它（什么都不会发生）",
                        apply = () => "你绕开了祭坛。"
                    });
                    break;

                case 2:   // 血之祭坛
                    ev.title = "血之祭坛";
                    ev.story = "一座暗红色的祭坛，槽中干涸的血迹诉说着某种古老的交易。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "割血献祭（扣 4 点生命，获得一张牌）",
                        apply = () =>
                        {
                            gm.Damage(4);
                            var added = gm.AddRandomCard();
                            return added != null ? "血液渗入祭坛，你获得「" + added.DisplayText() + "」。" : "血液渗入祭坛，却什么也没发生。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "远离它（什么都不会发生）",
                        apply = () => "你远离了祭坛。"
                    });
                    break;

                case 3:   // 遗物祭坛
                    ev.title = "遗物祭坛";
                    ev.story = "迷雾中浮着一座祭坛，一件发光的器物静静躺在上面。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "取走器物（获得一件随机遗物）",
                        apply = () =>
                        {
                            var relic = gm.AddRandomRelic();
                            return relic != null ? "你取走了「" + relic.name + "」。\n" + relic.description : "你已经集齐了所有遗物，器物在你眼前消散。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "离开（什么都不会发生）",
                        apply = () => "你离开了祭坛。"
                    });
                    break;

                case 4:   // 贪婪神像
                    ev.title = "贪婪神像";
                    ev.story = "一尊咧嘴的神像摊开手掌，仿佛在向过客索要代价。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "献上鲜血（扣 6 点生命，获得一件随机遗物）",
                        apply = () =>
                        {
                            var relic = gm.AddRandomRelic();
                            if (relic != null)
                            {
                                gm.Damage(6);
                                return "神像收下代价，你获得「" + relic.name + "」。";
                            }
                            return "神像对你毫无反应，什么都没发生。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "离开（什么都不会发生）",
                        apply = () => "你离开了神像。"
                    });
                    break;

                default:   // 净化之火
                    ev.title = "净化之火";
                    ev.story = "一簇苍白的火焰凭空燃烧，仿佛能烧去一些「多余」的东西。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "踏入火焰（删除一张随机牌）",
                        apply = () =>
                        {
                            var removed = gm.RemoveRandomCard();
                            return removed != null ? "火焰掠过，删掉了「" + removed.DisplayText() + "」。" : "火焰掠过，却无牌可烧。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "离开（什么都不会发生）",
                        apply = () => "你离开了火焰。"
                    });
                    break;
            }
            return ev;
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
