using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 卡牌工厂 + 各职业起始卡组（SPEC 6.1 基础牌，不含「交换」）。
    /// 卡组组成是占位数值，后续按 SPEC 平衡调整。
    /// </summary>
    public static class CardLibrary
    {
        /// <summary>
        /// 造一张「单格运算」的全职业通用牌（起手基础牌 / 战利品牌共用）。
        /// <para>
        /// **卡名必须显式传**：早期这里靠 <c>AutoName()</c> 按「运算 + 数值」猜名字，有三个毛病：
        /// ① 数值一改名字就跟着漂（-3 叫「重击」，改成 -2 就变「重削」）；
        /// ② 会撞名（-3 和 -4 都叫「重击」，卡组里出现两张同名不同牌）；
        /// ③ 「引爆 / 催爆」是已停用的「加法催爆流」遗留叫法，跟现在的规则毫无关系。
        /// 名字归名字、数值归数值，以后调数值就不会偷偷改掉玩家看到的牌名。
        /// </para>
        /// </summary>
        public static CardData Make(Operation op, int magnitude, int cost, string cardName)
        {
            // Make() 造的都是通用牌 → 一律标「全职业」
            return new CardData(op, TargetType.Single, magnitude, cost, null, false, cardName) { owner = ClassTag.All };
        }

        /// <summary>战士专属卡：铁腕重击（2 费，-6，配被动「铁腕」实际 -7）。</summary>
        public static CardData WarriorClassCard()
        {
            return new CardData(Operation.Subtract, TargetType.Single, 6, 2, "削减一个数字", false, "铁腕重击") { owner = ClassTag.Warrior };
        }

        /// <summary>
        /// 战士专属卡：愈战愈勇（1 费，你**下次受到伤害后**：所有格 -1，并恢复 2 点生命）。
        /// 不需要选目标；挂起的效果在玩家真正挨打时结算一次（同回合打多张则叠加）。
        /// </summary>
        public static CardData WarriorFury()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "你下次受到伤害后：所有格 -1，并恢复 2 点生命", false, "愈战愈勇") { owner = ClassTag.Warrior };
            c.noTarget = true;
            c.onHurtAllDelta = -1;
            c.onHurtHeal = 2;
            // 卡面大字写「蓄力」而不是「-1」：这张牌打出当下不改任何数值，
            // 写 -1 会让人以为出了牌就立刻削全场（用户 2026-10-21 反馈）。
            c.label = "蓄力";
            return c;
        }

        /// <summary>战士专属卡：横扫（2 费，对一整行 -3，配被动 -4）。</summary>
        public static CardData WarriorSweep()
        {
            return new CardData(Operation.Subtract, TargetType.Row, 3, 2, "削减一整行数字", false, "横扫") { owner = ClassTag.Warrior };
        }

        /// <summary>战士专属卡：淬火（1 费，下一次数值攻击命中时给格子叠加 2 层灼烧）。</summary>
        /// <remarks>灼烧层数是固定值：不吃铁腕、铁腕护符等任何增幅（卡面写 2 就是 2）。</remarks>
        public static CardData WarriorQuench()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "下一次数值攻击附带 灼烧+2", true, "淬火") { owner = ClassTag.Warrior };
            c.burnLayers = 2;   // 淬火的灼烧层数 = 2
            return c;
        }

        /// <summary>
        /// 战士专属卡：全副武装（3 费，使敌人下回合的攻击无效化；你的下一回合多获得 1 点能量）。
        /// 不需要选目标（像附魔/回血卡一样直接生效）。
        /// </summary>
        public static CardData WarriorArmor()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 3,
                "获得 2 层 格挡；你的下一回合多获得 1 点能量", false, "全副武装") { owner = ClassTag.Warrior };
            c.grantBlock = 2;        // 自我buff：2 层格挡（能连挡两次攻击）
            c.nextEnergyDelta = 1;
            c.label = "格挡";
            return c;
        }

        /// <summary>法师专属卡：冻结（2 费，给目标叠 2 层冻结：接下来的两个玩家回合内它不成长、不攻击）。</summary>
        public static CardData MageFreeze()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 2,
                "给予 冻结+2", false, "冻结") { owner = ClassTag.Mage };
            c.freezeLayers = 2;
            // 卡面大字写关键词名「冻结」而不是「+2」：+2 容易被读成「对该格 +2 数值」（用户 2026-10-21 反馈）。
            // 层数写在描述里（「给予 冻结+2」），卡名与之相同 → 渲染层会省掉重复的小字卡名。
            c.label = "冻结";
            return c;
        }

        /// <summary>法师专属卡：极寒（1 费，-2；若目标格已被冻结，改为 -4）。</summary>
        public static CardData MageChill()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 2, 1,
                "若目标格已被 冻结，则改为 -4", false, "极寒") { owner = ClassTag.Mage };
            c.condition = CardCondition.TargetFrozen;
            c.bonusMagnitude = 4;
            return c;
        }

        /// <summary>法师专属卡：火球术（2 费，-2 并给目标叠 2 层灼烧）。</summary>
        public static CardData MageFireball()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 2, 2,
                "对一个数字 -2，并给予 灼烧+2", false, "火球术") { owner = ClassTag.Mage };
            c.burnLayers = 2;
            return c;
        }

        /// <summary>法师专属卡：奥术彗星（2 费，-2；若目标周围九宫格内有相同数字，改为 -6）。</summary>
        public static CardData MageComet()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 2, 2,
                "若该格周围九宫格内有相同数字，则改为 -6", false, "奥术彗星") { owner = ClassTag.Mage };
            c.condition = CardCondition.SameValueAround;
            c.bonusMagnitude = 6;
            return c;
        }

        /// <summary>法师专属卡：法力护盾（2 费，获得 1 层 格挡：抵挡下一次攻击；无需选目标）。</summary>
        public static CardData MageShield()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 2,
                "获得 1 层 格挡", false, "法力护盾") { owner = ClassTag.Mage };
            c.grantBlock = 1;   // 自我buff：格挡层数（多张叠起来能连挡好几次）
            c.label = "格挡";
            return c;
        }

        // ---- 衍生牌（只由卡牌/遗物产生，不进卡组、不进奖励、用完即消失）----

        /// <summary>衍生牌：法力饼干（0 费，立刻获得 2 点能量；你的下一回合开始时失去 1 点能量）。</summary>
        public static CardData ManaCookie()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 0,
                "获得 2 点能量；你的下一回合开始时失去 1 点能量", false, "法力饼干") { owner = ClassTag.All };
            c.isToken = true;
            c.noTarget = true;   // 纯资源牌，不需要选格子
            c.gainEnergy = 2;
            c.nextEnergyDelta = -1;
            // 卡面大字写「+2 cost」而不是「+2」：光写 +2 会被当成「给某个数字 +2」（用户 2026-10-21 反馈）。
            c.label = "+2 cost";
            return c;
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

        /// <summary>收割者专属卡：诱导（1 费，目标格 +4 非自然成长，并给该格 1 层冻结）。</summary>
        public static CardData ReaperInduce()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "+4\n给予 冻结+1", false, "诱导") { owner = ClassTag.Reaper };
            c.induce = 4;
            c.label = "+4";   // 蓝色大字优先显示数值 +4，名字「诱导」走右上角小字
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

        /// <summary>收割者专属卡：菌种培育（1 费，+2 非自然成长 + 给该格 1 层冻结 + 下个回合该格 +3）。</summary>
        public static CardData ReaperSeed()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 1,
                "+2\n给予 冻结+1\n下个回合该格 +3", false, "菌种培育") { owner = ClassTag.Reaper };
            c.induce = 2;                 // 立即 +2 非自然成长，并把该格下回合的自然成长冻掉
            c.delayedValueDelta = 3;      // 下个回合开始时该格 +3（正 = 加，走同一套延迟数值管线）
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

        /// <summary>收割者专属卡：镰刀横扫（1 费，对单格先 +1 再 -5，不能对核心使用）。</summary>
        public static CardData ReaperScytheSweep()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 5, 1,
                "先 +1，再 -5；不能对核心使用", false, "镰刀横扫") { owner = ClassTag.Reaper };
            c.noCore = true;
            c.preGrowth = 1;   // 主运算（-5）之前先给该格 +1 非自然成长
            // 卡面大字写完整的「+1 -5」：只写 -5 会盖掉那 +1 的非自然成长（会推进毒性标记），
            // 玩家看到「-5」打出后却发现格子只掉了 4，会当成 bug（用户 2026-10-21 反馈）。
            c.label = "+1 -5";
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

        /// <summary>战利品牌：倍增（0 费，单格 ×2；出牌后立刻获得 1 点能量）。</summary>
        public static CardData RewardDouble()
        {
            var c = new CardData(Operation.Multiply, TargetType.Single, 2, 0, "出牌后立刻获得 1 点能量", false, "倍增") { owner = ClassTag.All };
            c.gainEnergy = 1;
            return c;
        }

        /// <summary>战利品牌：倍化（0 费，单格 ×3；对除该格外的所有格 -2；出牌后立刻获得 1 点能量）。</summary>
        public static CardData RewardTriple()
        {
            var c = new CardData(Operation.Multiply, TargetType.Single, 3, 0,
                "对除该格外的所有格 -2；出牌后立刻获得 1 点能量", false, "倍化") { owner = ClassTag.All };
            c.gainEnergy = 1;
            c.otherCellsDelta = -2;   // 主目标之外的所有格 -2
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
                Make(Operation.Subtract, 1, 1, "削减"),   // -1
                Make(Operation.Subtract, 2, 1, "重削"),   // -2
            };
        }

        /// <summary>战利品牌：休假（3 费，恢复 15 点生命；你的下一回合开始时失去 2 点能量）。</summary>
        public static CardData RewardVacation()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 3,
                "恢复 15 点生命；你的下一回合开始时失去 2 点能量", false, "休假") { owner = ClassTag.All };
            c.heal = 15;
            c.nextEnergyDelta = -2;
            c.label = "回血";   // 卡面大字（不然会显示成 -0）
            return c;
        }

        /// <summary>
        /// 战利品池全量（只剩真正的战利品牌；不含职业专属牌，也不含 -1/-2 起手基础牌）。
        /// 图鉴与 RewardChoices 共用这一份定义，避免两处漂移。
        /// </summary>
        public static List<CardData> RewardPool()
        {
            return new List<CardData>
            {
                Make(Operation.Subtract, 3, 1, "重击"),                 // -3
                RewardHalve(),                                          // 折半 ÷2，延迟费用
                Make(Operation.Divide, 3, 3, "分割"),                   // ÷3（3 费；除法的解析被动照旧）
                RewardDouble(),                                         // 倍增 ×2（0 费，返 1 能量）
                RewardTriple(),                                         // 倍化 ×3（0 费，其余格 -2，返 1 能量）
                new CardData(Operation.Subtract, TargetType.Row, 1, 1, null, false, "削减一整行") { owner = ClassTag.All },
                new CardData(Operation.Subtract, TargetType.Column, 1, 1, null, false, "削减一整列") { owner = ClassTag.All },
                RewardAllOut(),                                         // 全力进攻：全体 -1，下回合少抽 1 张
                RewardVacation(),                                       // 休假：回 15 血，下回合 -2 能量
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
        /// 所有「衍生牌」的唯一清单：只由卡牌/遗物产生，不进卡组、不进奖励，用在图鉴的「衍生牌」段。
        /// 新增衍生牌 = 写工厂方法（里面设 `isToken = true`）+ 在这里加一行。
        /// </summary>
        public static List<CardData> AllTokenCards()
        {
            return new List<CardData>
            {
                ManaCookie(),   // 法力饼干（法师初始遗物产生）
            };
        }

        // ---- 事件限定牌（只能从问号事件拿到，不进任何奖励池）----

        /// <summary>
        /// 事件限定牌：薪火（0 费，给一个格子叠 1 层 灼烧）。
        /// <para>
        /// **一局游戏内会成长**：一场战斗里用满 3 次 → 本局永久 +1 层（每场战斗最多触发一次）。
        /// 成长量存在卡牌实例的 <see cref="CardData.permGrowth"/> 上，随卡走、随存档保存，
        /// 实际生效层数看 <see cref="CardData.EffectiveBurn"/>。
        /// </para>
        /// <para>
        /// 只能从「净化之火」事件的 B 选项拿到，跟奖励池**完全隔离**（`isEventOnly = true`）。
        /// </para>
        /// </summary>
        public static CardData Kindling()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 0,
                "给予 灼烧+1", false, "薪火") { owner = ClassTag.All };
            c.isEventOnly = true;
            c.burnLayers = 1;
            c.growth = CardGrowth.Kindling;
            return c;
        }

        /// <summary>事件限定牌的图鉴清单（图鉴「事件牌」段）。加新事件牌：写工厂（标 `isEventOnly`）+ 在这里加一行。</summary>
        public static List<CardData> AllEventCards()
        {
            return new List<CardData>
            {
                Kindling(),   // 薪火（净化之火事件）
            };
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
                WarriorQuench(),      // 淬火（原「火焰附魔」）
                WarriorArmor(),       // 全副武装
                WarriorFury(),        // 愈战愈勇
                // 法师
                MageFreeze(),         // 冻结
                MageChill(),          // 极寒
                MageFireball(),       // 火球术
                MageComet(),          // 奥术彗星
                MageShield(),         // 法力护盾
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
                universal.Add(Make(Operation.Add, 2, 1, "催爆"));   // 加法牌是嗜血/收割职业专属（名字沿用旧叫法）
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

        /// <summary>
        /// 战士：减法压制（切瘤）—— 起手 2 张职业牌（铁腕重击 / 横扫）+ 5 张基础牌 = 7 张，
        /// 基础牌构成与法师、收割者完全一致：3×削减(-1) + 2×重削(-2)。
        /// </summary>
        public static List<CardData> WarriorDeck()
        {
            return new List<CardData>
            {
                WarriorClassCard(),  // 专属卡：铁腕重击
                WarriorSweep(),      // 专属卡：横扫（AOE）
                // 基础牌：3×削减(-1)、2×重削(-2)（用户 2026-10-21：和其他两职业保持一致，且去掉起手的重击）
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 2, 1, "重削"),
                Make(Operation.Subtract, 2, 1, "重削"),
                // 淬火【不在】起手卡组里：它仍是战士职业牌，只走战利品 40% 职业池获取。
                // 重击(-3) 也已从起手移除（2026-10-21 用户要求）：三职业起手基础牌统一成 3×削减 + 2×重削。
                // 折半（÷2）从起手移除更早：全游戏只保留战利品池里削弱过的 RewardHalve（÷2 但下回合能量 -1）。
            };
        }

        /// <summary>法师：冻结控场 + 灼烧/条件牌输出（起手 3×削减、2×重削、冻结、火球术）。</summary>
        public static List<CardData> MageDeck()
        {
            return new List<CardData>
            {
                MageFreeze(),   // 专属卡：冻结（冻结+2）
                MageFireball(), // 专属卡：火球术（-2 并灼烧+2）
                // 基础牌：3×削减(-1)、2×重削(-2)
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 2, 1, "重削"),
                Make(Operation.Subtract, 2, 1, "重削"),
            };
        }

        /// <summary>收割者：诱导 + 毒性标记起手，其余职业牌（菌种培育/收获/镰刀横扫）走战利品 40% 职业池获取。</summary>
        public static List<CardData> ReaperDeck()
        {
            return new List<CardData>
            {
                ReaperInduce(),          // 专属卡：诱导
                ReaperToxicCatalyst(),   // 专属卡：毒性催化（标记 2 → 8 伤）
                // 基础牌：3×削减(-1)、2×重削(-2)
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 1, 1, "削减"),
                Make(Operation.Subtract, 2, 1, "重削"),
                Make(Operation.Subtract, 2, 1, "重削"),
            };
        }
    }
}
