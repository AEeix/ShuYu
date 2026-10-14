using System;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>运算类型。</summary>
    public enum Operation { Add, Subtract, Multiply, Divide }

    /// <summary>目标类型（M0 只用 Single，其余留给 M1）。</summary>
    public enum TargetType { Single, Row, Column, All }

    /// <summary>数字种类：普通数字 / 攻击部位 / 核心（怪物行为用）。</summary>
    public enum NumberKind { Normal, Part, Core }

    /// <summary>数字实体（棋盘上的敌人）。</summary>
    [Serializable]
    public class NumberData
    {
        public int value;
        public int row;
        public int col;
        public NumberKind kind;     // 普通 / 攻击部位 / 核心
        public int attackOrder;     // 部位固定攻击顺序（Part 用，Normal/Core 忽略）

        public NumberData(int value, int row, int col, NumberKind kind = NumberKind.Normal, int attackOrder = 0)
        {
            this.value = value;
            this.row = row;
            this.col = col;
            this.kind = kind;
            this.attackOrder = attackOrder;
        }
    }

    /// <summary>卡牌数据：运算 / 目标 / 数值。</summary>
    [Serializable]
    public class CardData
    {
        public Operation op;
        public TargetType target;
        public int magnitude;
        public int cost;   // 能量费用

        public CardData(Operation op, TargetType target, int magnitude, int cost = 1)
        {
            this.op = op;
            this.target = target;
            this.magnitude = magnitude;
            this.cost = cost;
        }

        public string DisplayText()
        {
            switch (op)
            {
                case Operation.Add: return "+" + magnitude;
                case Operation.Subtract: return "-" + magnitude;
                case Operation.Multiply: return "×" + magnitude;
                case Operation.Divide: return "÷" + magnitude;
                default: return "?" + magnitude;
            }
        }
    }

    /// <summary>棋盘状态（可序列化，为 M5 异步幽灵预留）。</summary>
    [Serializable]
    public class BoardState
    {
        public int size;
        public List<NumberData> numbers = new List<NumberData>();

        public BoardState(int size) { this.size = size; }

        public NumberData At(int row, int col)
        {
            for (int i = 0; i < numbers.Count; i++)
            {
                var n = numbers[i];
                if (n.row == row && n.col == col) return n;
            }
            return null;
        }

        /// <summary>深拷贝快照（后续序列化用）。</summary>
        public BoardState Snapshot()
        {
            var copy = new BoardState(size);
            for (int i = 0; i < numbers.Count; i++)
                copy.numbers.Add(new NumberData(numbers[i].value, numbers[i].row, numbers[i].col, numbers[i].kind, numbers[i].attackOrder));
            return copy;
        }
    }

    /// <summary>玩家状态。</summary>
    [Serializable]
    public class PlayerState
    {
        public int health;
        public PlayerState(int health) { this.health = health; }
    }

    /// <summary>运算规则（SPEC 6.3：除法向下取整、下限 0、上限 9）。passive 为职业被动钩子。</summary>
    public static class MathOps
    {
        public static int Apply(int current, Operation op, int magnitude, PassiveType passive = PassiveType.None)
        {
            int result = current;
            switch (op)
            {
                case Operation.Add: result = current + magnitude; break;
                case Operation.Subtract:
                    int sub = magnitude;
                    if (passive == PassiveType.IronGrip) sub += 1;   // 铁腕：减法牌数值 +1
                    result = current - sub;
                    break;
                case Operation.Multiply: result = current * magnitude; break;
                case Operation.Divide:
                    if (magnitude == 0) { result = current; break; }
                    result = current / magnitude;                    // 向下取整
                    if (passive == PassiveType.Analyze && current % magnitude != 0) result -= 1; // 解析：切干净余数
                    break;
            }
            if (result < 0) result = 0;   // 下限 0（消灭）
            if (result > 9) result = 9;   // 上限 9（玩家操作不会直接造出 >=10）
            return result;
        }
    }
}
