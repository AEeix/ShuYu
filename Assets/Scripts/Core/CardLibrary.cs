using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 卡牌工厂 + 各职业起始卡组（SPEC 6.1 基础牌，不含「交换」）。
    /// 卡组组成是占位数值，后续按 SPEC 平衡调整。
    /// </summary>
    public static class CardLibrary
    {
        public static CardData Make(Operation op, int magnitude)
        {
            return new CardData(op, TargetType.Single, magnitude);
        }

        /// <summary>战士：减法压制（切瘤）。</summary>
        public static List<CardData> WarriorDeck()
        {
            return new List<CardData>
            {
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 3),
                Make(Operation.Divide, 2),
                Make(Operation.Multiply, 0),
                Make(Operation.Add, 2),
            };
        }

        /// <summary>法师：乘除精准（削大数字）。</summary>
        public static List<CardData> MageDeck()
        {
            return new List<CardData>
            {
                Make(Operation.Divide, 2),
                Make(Operation.Divide, 2),
                Make(Operation.Divide, 2),
                Make(Operation.Multiply, 0),
                Make(Operation.Multiply, 0),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Add, 2),
            };
        }

        /// <summary>收割者：加法催爆（溢出收割）。</summary>
        public static List<CardData> ReaperDeck()
        {
            return new List<CardData>
            {
                Make(Operation.Add, 2),
                Make(Operation.Add, 2),
                Make(Operation.Add, 2),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 1),
                Make(Operation.Subtract, 2),
                Make(Operation.Subtract, 2),
                Make(Operation.Divide, 2),
                Make(Operation.Divide, 2),
                Make(Operation.Multiply, 0),
            };
        }
    }
}
