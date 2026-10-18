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
            return new CardData(op, TargetType.Single, magnitude, cost);
        }

        /// <summary>战士专属卡：铁腕重击（2 费，-5，配被动「铁腕」实际 -6）。</summary>
        public static CardData WarriorClassCard()
        {
            return new CardData(Operation.Subtract, TargetType.Single, 5, 2, "铁腕重击：削减一个数字");
        }

        /// <summary>战士专属卡：横扫（2 费，对一整行 -2，配被动 -3）。</summary>
        public static CardData WarriorSweep()
        {
            return new CardData(Operation.Subtract, TargetType.Row, 2, 2, "横扫：削减一整行数字");
        }

        /// <summary>战士专属卡：火焰附魔（2 费，下一次攻击附带灼烧）。</summary>
        public static CardData WarriorEnchant()
        {
            return new CardData(Operation.Subtract, TargetType.Single, 0, 2, "火焰附魔：下一次攻击附带灼烧", true);
        }

        /// <summary>法师专属卡：烈焰（2 费，对一个数字 -2 并灼烧）。</summary>
        public static CardData MageFire()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 2, 2, "烈焰：对一个数字 -2 并灼烧");
            c.burns = true;
            return c;
        }

        /// <summary>法师专属卡：冰霜（2 费，冰冻一个数字，跳过本轮增殖）。</summary>
        public static CardData MageIce()
        {
            var c = new CardData(Operation.Subtract, TargetType.Single, 0, 2, "冰霜：冰冻一个数字");
            c.freezes = true;
            return c;
        }

        /// <summary>法师专属卡：解析风暴（2 费，对一列所有数字 ÷2，配被动「解析」切干净）。</summary>
        public static CardData MageStorm()
        {
            return new CardData(Operation.Divide, TargetType.Column, 2, 2, "解析风暴：对一整列 ÷2");
        }

        /// <summary>收割者专属卡：催爆（2 费，+5 单个数字，推向溢出）。</summary>
        public static CardData ReaperDetonate()
        {
            return new CardData(Operation.Add, TargetType.Single, 5, 2, "催爆：给一个数字 +5，推向溢出");
        }

        /// <summary>收割者专属卡：收割（2 费，对一整行 +2，推向溢出）。</summary>
        public static CardData ReaperReap()
        {
            return new CardData(Operation.Add, TargetType.Row, 2, 2, "收割：给一整行 +2，推向溢出");
        }

        /// <summary>收割者专属卡：嗜血（2 费，回复 3 点生命）。</summary>
        public static CardData ReaperBlood()
        {
            var c = new CardData(Operation.Add, TargetType.Single, 0, 2, "嗜血：回复 3 点生命");
            c.heal = 3;
            return c;
        }

        static readonly System.Random _rng = new System.Random();

        /// <summary>战斗胜利战利品：从通用牌池随机抽 count 张（不重复）。加法牌是收割者专属（溢出收割），非嗜血职业不给。</summary>
        public static List<CardData> RewardChoices(int count, PassiveType passive = PassiveType.None)
        {
            var pool = new List<CardData>
            {
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 3),
                Make(Operation.Divide, 2),
                Make(Operation.Divide, 3),
                Make(Operation.Multiply, 2),
                Make(Operation.Multiply, 3),
                new CardData(Operation.Subtract, TargetType.Row, 1, 2, "削减一整行"),
                new CardData(Operation.Subtract, TargetType.Column, 1, 2, "削减一整列"),
                new CardData(Operation.Subtract, TargetType.All, 1, 3, "削减所有数字"),
            };
            if (passive == PassiveType.Bloodthirst)
                pool.Add(Make(Operation.Add, 2));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            var result = new List<CardData>();
            for (int i = 0; i < count && i < pool.Count; i++)
                result.Add(pool[i]);
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
                Make(Operation.Divide, 2),
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
                Make(Operation.Divide, 2),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
            };
        }

        /// <summary>收割者：加法催爆（溢出收割）。</summary>
        public static List<CardData> ReaperDeck()
        {
            return new List<CardData>
            {
                ReaperDetonate(),  // 专属卡：催爆
                ReaperReap(),      // 专属卡：收割
                ReaperBlood(),     // 专属卡：嗜血
                Make(Operation.Add, 2),
                Make(Operation.Add, 2),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Divide, 2),
                Make(Operation.Add, 2),
            };
        }
    }
}
