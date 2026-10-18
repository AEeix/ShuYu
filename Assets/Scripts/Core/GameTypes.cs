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
        public int coreGroup;       // 核心归属分组（Part/Core 用，多核心 BOSS 逐核坏死；Normal 恒 0）
        public bool burn;           // 灼烧：每回合 -1（火焰附魔/火焰魔法附加）
        public bool frozen;         // 冰冻：跳过本轮增殖

        public NumberData(int value, int row, int col, NumberKind kind = NumberKind.Normal, int attackOrder = 0, int coreGroup = 0)
        {
            this.value = value;
            this.row = row;
            this.col = col;
            this.kind = kind;
            this.attackOrder = attackOrder;
            this.coreGroup = coreGroup;
        }
    }

    /// <summary>卡牌数据：运算 / 目标 / 数值。</summary>
    [Serializable]
    public class CardData
    {
        public Operation op;
        public TargetType target;
        public int magnitude;
        public int cost;         // 能量费用
        public string description; // 卡牌描述（职业专属卡用，基础卡为空）
        public bool enchant;      // 附魔卡（如火焰附魔：下一次攻击附带灼烧）
        public bool burns;        // 命中附带灼烧（直接火伤）
        public bool freezes;      // 命中附带冰冻（跳过增殖）
        public int heal;          // 治疗量（>0 为回血卡，无需目标）

        public CardData(Operation op, TargetType target, int magnitude, int cost = 1, string description = null, bool enchant = false)
        {
            this.op = op;
            this.target = target;
            this.magnitude = magnitude;
            this.cost = cost;
            this.description = description;
            this.enchant = enchant;
        }

        public string DisplayText()
        {
            if (enchant) return "附魔";
            if (freezes && magnitude == 0) return "冰冻";
            if (heal > 0) return "回血";
            string s;
            switch (op)
            {
                case Operation.Add: s = "+" + magnitude; break;
                case Operation.Subtract: s = "-" + magnitude; break;
                case Operation.Multiply: s = "×" + magnitude; break;
                case Operation.Divide: s = "÷" + magnitude; break;
                default: s = "?" + magnitude; break;
            }
            switch (target)
            {
                case TargetType.Row: s += " 行"; break;
                case TargetType.Column: s += " 列"; break;
                case TargetType.All: s += " 全"; break;
            }
            return s;
        }

        /// <summary>强化卡牌：回血牌 +2 治疗；有数值的牌数值 +1；0 数值牌（附魔/冰冻）降 1 费。</summary>
        public void Upgrade()
        {
            if (heal > 0) { heal += 2; return; }
            if (magnitude > 0) { magnitude += 1; return; }
            if (cost > 1) cost -= 1;
        }
    }

    /// <summary>棋盘状态（可序列化，为 M5 异步幽灵预留）。</summary>
    [Serializable]
    public class BoardState
    {
        public int rows;   // 行数（高）
        public int cols;   // 列数（宽）
        public List<NumberData> numbers = new List<NumberData>();

        public BoardState(int rows, int cols) { this.rows = rows; this.cols = cols; }
        public BoardState(int size) : this(size, size) { }   // 正方形快捷构造

        public NumberData At(int row, int col)
        {
            for (int i = 0; i < numbers.Count; i++)
            {
                var n = numbers[i];
                if (n.row == row && n.col == col) return n;
            }
            return null;
        }

        /// <summary>是否是「切角」格（普通小怪战的史莱姆形状：去掉四个角）。</summary>
        public bool IsCornerCell(int row, int col)
        {
            return (row == 0 || row == rows - 1) && (col == 0 || col == cols - 1);
        }

        /// <summary>深拷贝快照（后续序列化用）。</summary>
        public BoardState Snapshot()
        {
            var copy = new BoardState(rows, cols);
            for (int i = 0; i < numbers.Count; i++)
            {
                var n = new NumberData(numbers[i].value, numbers[i].row, numbers[i].col, numbers[i].kind, numbers[i].attackOrder, numbers[i].coreGroup);
                n.burn = numbers[i].burn;
                n.frozen = numbers[i].frozen;
                copy.numbers.Add(n);
            }
            return copy;
        }
    }

    /// <summary>玩家状态。</summary>
    [Serializable]
    public class PlayerState
    {
        public int health;
        public int maxHealth;
        public List<RelicType> relics = new List<RelicType>();

        public PlayerState(int health)
        {
            this.health = health;
            this.maxHealth = health;
        }

        public bool HasRelic(RelicType t) => relics.Contains(t);

        /// <summary>回血（不超上限）。</summary>
        public void Heal(int amount) { health = System.Math.Min(health + amount, maxHealth); }
    }

    /// <summary>运算规则（SPEC 6.3：除法向下取整、下限 0、上限 9）。passive 为职业被动钩子，relics 为遗物钩子。</summary>
    public static class MathOps
    {
        public static int Apply(int current, Operation op, int magnitude, PassiveType passive = PassiveType.None, List<RelicType> relics = null)
        {
            int result = current;
            switch (op)
            {
                case Operation.Add: result = current + magnitude; break;
                case Operation.Subtract:
                    int sub = magnitude;
                    if (passive == PassiveType.IronGrip || Has(relics, RelicType.IronTalisman)) sub += 1;   // 铁腕/铁腕护符：减法牌数值 +1
                    result = current - sub;
                    break;
                case Operation.Multiply:
                    int mul = magnitude;
                    if (Has(relics, RelicType.MultiplyBoost)) mul += 1;   // 乘方增幅：乘法牌数值 +1
                    result = current * mul;
                    break;
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

        static bool Has(List<RelicType> relics, RelicType t) => relics != null && relics.Contains(t);
    }

    /// <summary>溢出事件：一个数字溢出（≥10 爆炸）时记录，供 UI 播放爆炸动画。</summary>
    public class OverflowEvent
    {
        public int row;
        public int col;
        public int damage;    // 造成的伤害
        public bool isCore;   // 核心爆炸（回落到 CoreResetValue）vs 普通溢出（数字消失）

        public OverflowEvent(int row, int col, int damage, bool isCore)
        {
            this.row = row;
            this.col = col;
            this.damage = damage;
            this.isCore = isCore;
        }
    }
}
