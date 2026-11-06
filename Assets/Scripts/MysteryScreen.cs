using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 地图问号：随机剧情事件。每个事件有一段前置剧情 + 两个选项（括号里标注后果），
    /// 选完显示结果文本，再「继续前进」。
    /// <para>
    /// 两种特殊流程：
    /// ① <see cref="MysteryChoice.cardRewardResult"/> 非空 = 这个选项先开「战利品三选一」，
    ///    选完（或跳过）再回到本界面显示那段结果文本（见 <see cref="GameManager.ShowCardRewardThenMystery"/>）；
    /// ② <see cref="SetResultOnly"/> = 只显示结果态，就是上面那种「领完奖励回到事件」的情形。
    /// </para>
    /// <para>
    /// 加事件：在 <see cref="BuildEvent"/> 里加一个 case，并把 <see cref="Start"/> 里的
    /// <c>Random.Range(0, 6)</c> 的数字同步 +1（忘了同步 = 新事件永远抽不到）。
    /// 现在的 id 5 走 <c>default</c>，加新事件前记得把它改成显式的 <c>case 5</c>。
    /// </para>
    /// </summary>
    public class MysteryScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _choicesRoot;   // 选项区（选完后销毁）
        string _resultOnly;           // 非空 = 只显示结果态（由 GameManager.ShowMysteryResult 传进来）

        /// <summary>一个选项：按钮文本 + 执行效果（返回结果描述）+ 可选的「先开卡牌奖励」标记。</summary>
        class MysteryChoice
        {
            public string text;
            public Func<string> apply;
            public string cardRewardResult;   // 非空 = 先开战利品三选一，选完回到事件显示这段文本
        }

        /// <summary>一个事件：标题 + 剧情 + 选项列表。</summary>
        class MysteryEvent
        {
            public string title;
            public string story;
            public List<MysteryChoice> choices = new List<MysteryChoice>();
        }

        /// <summary>
        /// 只显示结果态（事件里领完卡牌奖励后回到这里，而不是重新抽一个事件）。
        /// 必须在 <c>Start</c> 之前调用。
        /// </summary>
        public void SetResultOnly(string text) { _resultOnly = text; }

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("MysteryRoot", canvas.transform);
            UIFactory.Stretch(_root);

            // 从卡牌奖励回来：只显示结果，不再抽事件
            if (_resultOnly != null)
            {
                ShowResult(_resultOnly);
                return;
            }

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

        /// <summary>点选一个选项：要么转去卡牌奖励，要么执行效果并显示结果。</summary>
        void OnChoose(MysteryChoice ch)
        {
            var gm = GameManager.Instance;

            // 这个选项是「领一份战利品」：先开三选一，选完再回到事件显示结果（界面先别销毁）
            if (ch.cardRewardResult != null)
            {
                gm.ShowCardRewardThenMystery(ch.cardRewardResult);
                return;
            }

            ShowResult(ch.apply());
        }

        /// <summary>显示结果文本（选项区销毁）+「继续前进」；扣血致死时改文案、清档。</summary>
        void ShowResult(string result)
        {
            var gm = GameManager.Instance;

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
                        text = "搜索武器箱（获得一份卡牌奖励）",
                        cardRewardResult = "你找到了遗落的武器，你相信这可以帮助到你"
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
                        text = "在祭坛旁休息（恢复 8 点生命）",
                        apply = () =>
                        {
                            gm.Heal(8);
                            return "这个祭坛给你一股宁静感，你在祭坛下好好地睡了一觉";
                        }
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
                        text = "砸碎祭坛（砸烂它）",
                        apply = () =>
                        {
                            // 条件：生命 ≥ 12 且卡组里还有「削减 / 重削」→ 换命提纯卡组；否则只回血
                            if (gm.Player.health >= 12 && gm.HasBasicSubtractCard())
                            {
                                gm.Damage(6);
                                var removed = gm.RemoveBasicSubtractCard();
                                return "你对这个祭坛感到不安，你砸碎了这个祭坛，其中出现的灵体嘲笑了你，“赐予”了你力量"
                                     + RemovedDetail(removed);
                            }
                            gm.Heal(4);
                            return "你对这个祭坛感到不安，你砸碎了这个祭坛，其中出现的灵体对你的勇敢感到敬佩，它将收集到的些许血液“分享”给你";
                        }
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
                        text = "献上祭品（向祭坛献上一张你的卡牌）",
                        apply = () =>
                        {
                            // 条件：卡组里有「非起手卡组」的牌 → 两件遗物 + 永久删掉其中一张；否则只给下一场 1 层格挡
                            if (gm.HasNonStartingCard())
                            {
                                var gained = gm.AddRandomRelics(2);
                                var removed = gm.RemoveRandomNonStartingCard();

                                // 拿到什么、删了什么都写进结果文本（不然玩家不知道发生了什么）
                                string detail = "\n（获得 ";
                                detail += gained.Count > 0
                                    ? string.Join("、", gained.ConvertAll(r => "「" + r.name + "」"))
                                    : "无新遗物";
                                if (removed != null) detail += "，从卡组中删除了「" + removed.DisplayName() + "」";
                                detail += "）";
                                return "你向祭坛虔诚地献上你的战利品，祭坛散发光芒，出现了两件强大的器物" + detail;
                            }
                            gm.GrantBlockNextBattle(1);
                            return "你翻遍背包，放上了你带来的卡牌，祭坛回应了你的虔诚，并没有带走你的卡牌，但是你感受到了他的庇佑"
                                 + "\n（下一场战斗开始时获得 1 层 格挡）";
                        }
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
                        text = "砸碎神像（砸碎他，拒绝贪婪）",
                        apply = () =>
                        {
                            gm.Heal(6);
                            return "你拒绝了贪婪，虽然说神像的面容也有一些原因，但你砸碎了它，利用了它收集的鲜血";
                        }
                    });
                    break;

                case 5:   // 净化之火
                    ev.title = "净化之火";
                    ev.story = "一簇苍白的火焰凭空燃烧，仿佛能烧去一些「多余」的东西。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "踏入火焰（删除一张随机牌）",
                        apply = () =>
                        {
                            var removed = gm.RemoveRandomCard();
                            return removed != null ? "火焰掠过，删掉了「" + removed.DisplayName() + "」。" : "火焰掠过，却无牌可烧。";
                        }
                    });
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "传承火焰（取出一团火焰）",
                        apply = () =>
                        {
                            // 薪火：事件限定牌，一局游戏里越用越强（见 CardData.CardGrowth）
                            gm.AddCard(CardLibrary.Kindling());
                            return "你从中引导，取出了一团弱小的火焰，你眼中突然有无数的英雄闪过，但在一瞬之间，火焰和画面全部消失，有一个声音告诉你必须将他传承下去";
                        }
                    });
                    break;

                default:   // 兜底：id 越界时给一个安全事件（不要写会崩的东西）
                    ev.title = "空无一物";
                    ev.story = "这里什么也没有。";
                    ev.choices.Add(new MysteryChoice
                    {
                        text = "继续赶路（什么都不会发生）",
                        apply = () => "你继续赶路。"
                    });
                    break;
            }
            return ev;
        }

        /// <summary>被删掉的牌写进结果文本的后缀（用户要求：玩家必须知道删了哪张牌）。</summary>
        static string RemovedDetail(CardData removed)
        {
            return removed != null ? "\n（从卡组中删除了「" + removed.DisplayName() + "」）" : "";
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
