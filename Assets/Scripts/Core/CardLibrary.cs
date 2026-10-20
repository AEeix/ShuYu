using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 卡牌工厂 + 各职业起始卡组（SPEC 6.1 基础牌，不含「交换」）。
    /// 卡组组成是占位数值，后续按 SPEC 平衡调整。
    /// </summary>
    public static class CardLibrary
    {
        public static CardData Make(Operation op, int magnitude, int cost = 1)
        {
            // Make() 造的都是通用牌 → 一律标「全职业」
            return new CardData(op, TargetType.Single, magnitude, cost, null, false, AutoName(op, magnitude)) { owner = ClassTag.All };
        }

        /// <summary>基础卡自动命名（按运算 + 数值）。</summary>
        static string AutoName(Operation op, int magnitude)
        {
            switch (op)
            {
                case Operation.Subtract:
                    return magnitude >= 3 ? "重击" : (magnitude >= 2 ? "重削" : "削减");
                case Operation.Add:
                    return magnitude >= 5 ? "引爆" : "催爆";
                case Operation.Divide:
                    return magnitude == 2 ? "折半" : "分割";
                case Operation.Multiply:
                    return magnitude == 2 ? "倍增" : "倍化";
                default:
                    return "运算";
            }
        }

        /// <summary>战士专属卡：铁腕重击（2 费，-5，配被动「铁腕」实际 -6）。</summary>
        public static CardData WarriorClassCard()
        {
            return new CardData(Operation.Subtract, TargetType.Single, 5, 2, "削减一个数字", false, "铁腕重击") { owner = ClassTag.Warrior };
        }

        /// <summary>战士专属卡：横扫（2 费，对一整行 -2，配被动 -3）。</summary>
        public static CardData WarriorSweep()
        {
            return new CardData(Operation.Subtract, TargetType.Row, 2, 2, "削减一整行数字", false, "横扫") { owner = ClassTag.Warrior };
        }

        /// <summary>战士专属卡：火焰附魔（1 费，下一次数值攻击命中时给格子叠加 2 层灼烧）。</summary>
        /// <remarks>灼烧层数是固定值：不吃铁腕、铁腕护符等任何增幅（卡面写 2 就是 2）。</remarks>
        public static CardData WarriorEnchant()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "下一次数值攻击附带 灼烧+2", true, "火焰附魔") { owner = ClassTag.Warrior };
            c.burnLayers = 2;   // 火焰附魔的灼烧层数 = 2
            return c;
        }

        /// <summary>法师专属卡：烈焰（2 费，对一个数字 -2 并叠加 1 层灼烧）。</summary>
        public static CardData MageFire()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 2, 2,
                "对一个数字 -2，并附带 灼烧+1", false, "烈焰") { owner = ClassTag.Mage };
            c.burnLayers = 1;
            return c;
        }

        /// <summary>法师专属卡：冰霜（2 费，冰冻一个数字，跳过它下一轮自然成长）。</summary>
        public static CardData MageIce()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 2,
                "冰冻一个数字，跳过它下一轮自然成长", false, "冰霜") { owner = ClassTag.Mage };
            c.freezes = true;
            return c;
        }

        /// <summary>法师专属卡：解析风暴（2 费，对一列所有数字 ÷2，配被动「解析」切干净）。</summary>
        public static CardData MageStorm()
        {
            return new CardData(Operation.Divide, TargetType.Column, 2, 2, "对一整列 ÷2", false, "解析风暴") { owner = ClassTag.Mage };
        }

        // ---- 旧收割者牌（加法催爆流，2026-10 重构为「非自然成长 + 毒性催化」后停用，保留待查）----
        // /// <summary>收割者专属卡：催爆（2 费，+5 单个数字，推向溢出）。</summary>
        // public static CardData ReaperDetonate()
        // {
        //     return new CardData(Operation.Add, TargetType.Single, 5, 2, "催爆：给一个数字 +5，推向溢出");
        // }
        //
        // /// <summary>收割者专属卡：收割（2 费，对一整行 +2，推向溢出）。</summary>
        // public static CardData ReaperReap()
        // {
        //     return new CardData(Operation.Add, TargetType.Row, 2, 2, "收割：给一整行 +2，推向溢出");
        // }
        //
        // /// <summary>收割者专属卡：嗜血（2 费，回复 3 点生命）。</summary>
        // public static CardData ReaperBlood()
        // {
        //     var c = new CardData(Operation.Add, TargetType.Single, 0, 2, "嗜血：回复 3 点生命");
        //     c.heal = 3;
        //     return c;
        // }

        /// <summary>收割者专属卡：诱导（1 费，目标格 +2 非自然成长，并锁住下回合自然成长）。</summary>
        public static CardData ReaperInduce()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "+2\n锁定该格下一回合的自然成长", false, "诱导") { owner = ClassTag.Reaper };
            c.induce = 2;
            c.label = "+2";   // 蓝色大字优先显示数值 +2，名字「诱导」走右上角小字
            return c;
        }

        /// <summary>收割者专属卡：毒性催化（1 费，标记一个格子：累计 2 点非自然成长后，对其造成 8 点伤害）。</summary>
        public static CardData ReaperToxicCatalyst()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "标记该格：该格累计 2 点非自然成长后，造成 8 点伤害", false, "毒性催化") { owner = ClassTag.Reaper };
            c.toxinGoal = 2;
            c.toxinDamage = 8;
            return c;
        }

        /// <summary>收割者专属卡：菌种培育（1 费，+2 非自然成长 + 封锁该格下回合自然成长 + 下个回合该格 -4）。</summary>
        public static CardData ReaperSeed()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "+2\n封锁该格下回合自然成长\n下个回合该格 -4", false, "菌种培育") { owner = ClassTag.Reaper };
            c.induce = 2;                 // 立即 +2 非自然成长，并把该格下回合的自然成长冻掉
            c.delayedValueDelta = -4;     // 下个回合开始时该格 -4
            c.label = "+2";               // 卡面大字（不然会显示成「诱导」）
            return c;
        }

        /// <summary>收割者专属卡：收获（2 费，标记该格：该格接下来累计 3 点非自然成长后，造成 16 点伤害）。</summary>
        public static CardData ReaperHarvest()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 2,
                "该格接下来累计 3 点非自然成长后，造成 16 点伤害", false, "收获") { owner = ClassTag.Reaper };
            c.toxinGoal = 3;
            c.toxinDamage = 16;
            return c;
        }

        /// <summary>收割者专属卡：镰刀横扫（1 费，单格 -5，不能对核心使用）。</summary>
        public static CardData ReaperScytheSweep()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 5, 1,
                "单格 -5，不能对核心使用", false, "镰刀横扫") { owner = ClassTag.Reaper };
            c.noCore = true;
            return c;
        }

        // ---- 战利品牌：带延迟代价的强力牌（把代价并入下一回合，多张叠加）----

        /// <summary>战利品牌：折半（1 费，对一个数字 ÷2；下一回合能量 -1）。</summary>
        public static CardData RewardHalve()
        {
            var c = new CardData(Operation.Divide, TargetType.Single, 2, 1, "下一回合能量 -1", false, "折半") { owner = ClassTag.All };
            c.nextEnergyDelta = -1;
            return c;
        }

        /// <summary>战利品牌：全力进攻（1 费，所有数字 -1；下一回合少抽 1 张牌）。</summary>
        public static CardData RewardAllOut()
        {
            var c = new CardData(Operation.Subtract, TargetType.All, 1, 1, "所有数字 -1，下回合少抽 1 张牌", false, "全力进攻") { owner = ClassTag.All };
            c.nextDrawDelta = -1;
            return c;
        }

        static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 全职业**起手基础牌**：削减(-1) / 重削(-2)。它们属于「全职业」（图鉴里照样显示），
        /// 但**不进战利品池**——每个职业开局就有，再当奖励发出来只是浪费奖励位。
        /// </summary>
        public static List<CardData> UniversalBasics()
        {
            return new List<CardData>
            {
                Make(Operation.Subtract, 1),   // 削减 -1
                Make(Operation.Subtract, 2),   // 重削 -2
            };
        }

        /// <summary>
        /// 战利品池全量（只剩真正的战利品牌；不含职业专属牌，也不含 -1/-2 起手基础牌）。
        /// 图鉴与 RewardChoices 共用这一份定义，避免两处漂移。
        /// </summary>
        public static List<CardData> RewardPool()
        {
            return new List<CardData>
            {
                Make(Operation.Subtract, 3),                            // 重击 -3
                RewardHalve(),                                          // 折半 ÷2，延迟费用
                Make(Operation.Divide, 3, 2),                           // 分割 ÷3
                Make(Operation.Multiply, 2),                            // 倍增 ×2
                Make(Operation.Multiply, 3),                            // 倍化 ×3
                new CardData(Operation.Subtract, TargetType.Row, 1, 1, null, false, "削减一整行") { owner = ClassTag.All },
                new CardData(Operation.Subtract, TargetType.Column, 1, 1, null, false, "削减一整列") { owner = ClassTag.All },
                RewardAllOut(),                                         // 全力进攻：全体 -1，下回合少抽 1 张
            };
        }

        /// <summary>图鉴「全职业卡」段的完整内容 = 战利品池 + 起手基础牌（都标了 `ClassTag.All`）。</summary>
        public static List<CardData> AllUniversalCards()
        {
            var list = RewardPool();
            list.AddRange(UniversalBasics());
            return list;
        }

        /// <summary>
        /// 所有「职业专属牌」的唯一清单。**新增职业牌只要在这里加一行、并在工厂里标上 `owner` 就行**——
        /// 职业牌池（战利品 40%）和图鉴职业段都是从这张表和 `owner` 标记自动推导的，不用再去改别处。
        /// </summary>
        public static List<CardData> AllClassCards()
        {
            return new List<CardData>
            {
                // 战士
                WarriorClassCard(),   // 铁腕重击
                WarriorSweep(),       // 横扫
                WarriorEnchant(),     // 火焰附魔
                // 法师
                MageFire(),           // 烈焰
                MageIce(),            // 冰霜
                MageStorm(),          // 解析风暴
                // 收割者
                ReaperInduce(),          // 诱导
                ReaperToxicCatalyst(),   // 毒性催化
                ReaperSeed(),            // 菌种培育
                ReaperHarvest(),         // 收获
                ReaperScytheSweep(),     // 镰刀横扫
            };
        }

        /// <summary>
        /// 某个职业的专属牌池（战利品里占 40% 权重的那一份，也是图鉴职业段的内容）。
        /// 完全由卡牌自己的 `owner` 成员标记决定：标了该职业的牌就会自动进池。
        /// 传 `ClassTag.None` / `ClassTag.All` 返回空池——「全职业」不是一个可单独抽取的职业，它那一份用 `AllUniversalCards()`。
        /// 注意它和「起始卡组」是两回事——起始卡组只有一部分职业牌，这个池是配发战利品用的全量职业牌。
        /// </summary>
        public static List<CardData> ClassPool(ClassTag tag)
        {
            var pool = new List<CardData>();
            if (tag == ClassTag.None || tag == ClassTag.All) return pool;
            foreach (var c in AllClassCards())
                if ((c.owner & tag) != 0) pool.Add(c);
            return pool;
        }

        // ---- 战利品权重：全职业牌 60%，本职业专属牌 40% ----
        public const float UniversalLootWeight = 0.6f;
        public const float ClassLootWeight = 0.4f;

        /// <summary>战斗胜利战利品：抽 count 张不重复的牌。每张牌 60% 概率来自全职业池、40% 来自本职业专属池（按 `owner` 标记推导）。</summary>
        public static List<CardData> RewardChoices(int count, PassiveType passive = PassiveType.None, ClassTag tag = ClassTag.None)
        {
            var universal = RewardPool();
            if (passive == PassiveType.Bloodthirst || passive == PassiveType.Harvest)
                universal.Add(Make(Operation.Add, 2));        // 加法牌是嗜血/收割职业专属
            var classCards = ClassPool(tag);

            var result = new List<CardData>();
            while (result.Count < count)
            {
                var u = universal.FindAll(c => !result.Contains(c));
                var cl = classCards.FindAll(c => !result.Contains(c));
                if (u.Count == 0 && cl.Count == 0) break;     // 两个池都抽空了

                // 按 60/40 权重掷一次，决定这一张来自哪个池；池空了就把权重全给另一个池
                float uWeight = u.Count > 0 ? UniversalLootWeight : 0f;
                float cWeight = cl.Count > 0 ? ClassLootWeight : 0f;
                float r = (float)_rng.NextDouble() * (uWeight + cWeight);
                var source = r < uWeight ? u : cl;
                result.Add(source[_rng.Next(source.Count)]);
            }
            return result;
        }

        /// <summary>战士：减法压制（切瘤）。</summary>
        public static List<CardData> WarriorDeck()
        {
            return new List<CardData>
            {
                WarriorClassCard(),  // 专属卡：铁腕重击
                WarriorSweep(),      // 专属卡：横扫（AOE）
                WarriorEnchant(),    // 专属卡：火焰附魔
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 3),
                Make(Operation.Subtract, 1),
                // 折半（÷2）已从起始卡组移除：起始卡组里这张是没经过削弱的旧版，
                // 现在全游戏只保留战利品池里削弱过的 RewardHalve（÷2 但下回合能量 -1）。
            };
        }

        /// <summary>法师：乘除精准（削大数字）。</summary>
        public static List<CardData> MageDeck()
        {
            return new List<CardData>
            {
                MageFire(),     // 专属卡：烈焰
                MageIce(),      // 专属卡：冰霜
                MageStorm(),    // 专属卡：解析风暴
                // 折半（÷2）已从起始卡组移除：起始卡组里这张是没经过削弱的旧版，
                // 现在全游戏只保留战利品池里削弱过的 RewardHalve（÷2 但下回合能量 -1）。
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
            };
        }

        /// <summary>收割者：诱导 + 毒性标记起手，其余职业牌（菌种培育/收获/镰刀横扫）走战利品 40% 职业池获取。</summary>
        public static List<CardData> ReaperDeck()
        {
            return new List<CardData>
            {
                ReaperInduce(),          // 专属卡：诱导
                ReaperToxicCatalyst(),   // 专属卡：毒性催化（标记 2 → 8 伤）
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
            };
        }
    }
}
