using System;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>运算类型。</summary>
    public enum Operation { Add, Subtract, Multiply, Divide }

    /// <summary>目标类型（M0 只用 Single，其余留给 M1）。</summary>
    public enum TargetType { Single, Row, Column, All }

    /// <summary>
    /// 卡牌的「职业成员标记」：一张牌标了哪个职业，就属于哪个职业的专属牌池
    /// （= 战利品 40% 权重的来源，也是图鉴职业段的内容来源）。
    /// `All` = 全职业牌：任何职业都能用，构成战利品 60% 权重的那一份，图鉴里单独列「全职业卡」段。
    /// 一张牌都不标（`None`）说明漏了标记，测试会报错。
    /// 用 Flags 是为了以后能出一张「多职业共用」的牌（现在每张专属牌只标一个职业）。
    /// </summary>
    [Flags]
    public enum ClassTag { None = 0, Warrior = 1, Mage = 2, Reaper = 4, All = 8 }

    /// <summary>数字种类：普通数字 / 攻击部位 / 核心（怪物行为用）。</summary>
    public enum NumberKind { Normal, Part, Core }

    /// <summary>
    /// 毒性标记：挂在格子上的状态，平时完全不起作用。
    /// 只有当这一格再次获得「非自然成长」（卡牌抬升数值）时才累计，累计够 goal 点就对这一格造成 damage 点直接伤害，然后该标记消失。
    /// 同一格可以叠加多个标记，各自独立累计：
    ///   例：先挂 (1 → 2伤)，再挂 (2 → 4伤)
    ///       然后 +1 → 第一个标记达标，2 伤；
    ///       再 +2 → 第二个标记达标，4 伤；
    ///       如果一次性 +3 → 两个标记同时达标，2 + 4 = 6 伤。
    /// </summary>
    [Serializable]
    public class ToxinMark
    {
        public int goal;       // 需要累计的非自然成长点数
        public int damage;     // 达标时造成的直接伤害
        public int progress;   // 已累计（从挂上这个标记的那一刻开始算）

        public ToxinMark(int goal, int damage) { this.goal = goal; this.damage = damage; }
    }

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
        public bool frozen;         // 冰冻：跳过本轮增殖
        public int burn;            // 灼烧层数：每个玩家回合开始时该格数值 -层数；命中叠加（0 = 未灼烧）
        public List<ToxinMark> toxins = new List<ToxinMark>();  // 毒性标记（可叠加多个，各自独立累计）
        public int delayedValueDelta;   // 延迟数值变化：下个回合开始时并入该格数值（正=加，负=减）

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
        public string cardName;   // 卡牌名字（如「重击」「铁腕重击」）
        public bool enchant;      // 附魔卡（如火焰附魔：下一次攻击附带灼烧）
        public int burnLayers;    // 命中时给目标叠加的灼烧层数（>0 为带灼烧的牌；火焰附魔/烈焰）
        public bool freezes;      // 命中附带冰冻（跳过增殖）
        public int heal;          // 治疗量（>0 为回血卡，无需目标）
        public int induce;        // 诱导量（>0 为诱导卡：立即获得 N 点非自然成长，并跳过下回合自然成长）
        public int toxinGoal;     // 毒性催化：需累计的非自然成长点数（>0 为标记卡）
        public int toxinDamage;   // 毒性催化：达标后对目标造成的直接伤害
        public int nextEnergyDelta;   // 延迟能量：并入下一回合的能量（正 = 加费，负 = 扣费，0 = 无；多张叠加）
        public int nextDrawDelta;     // 延迟抽牌：并入下一回合的手牌数（正 = 多抽，负 = 少抽，0 = 无；多张叠加）
        public int delayedValueDelta; // 延迟数值变化：下个回合开始时并入目标格的数值（负 = 减，0 = 无；同格叠加）
        public bool noCore;           // 不能对核心使用（镰刀横扫）
        public string label;          // 卡面大字覆盖（为空则按运算/目标自动生成，如「+2」「标记」）
        public ClassTag owner;        // 职业成员标记：标了某职业 → 进该职业的牌池 & 图鉴职业段（None = 全职业牌）
        public bool upgraded;         // 是否已被强化过（火堆强化 → 名字后加「+」并换色）

        public CardData(Operation op, TargetType target, int magnitude, int cost = 1, string description = null, bool enchant = false, string cardName = null)
        {
            this.op = op;
            this.target = target;
            this.magnitude = magnitude;
            this.cost = cost;
            this.description = description;
            this.enchant = enchant;
            // 名字：显式传入优先；否则从「名字：描述」里取「名字」；都没有则空
            if (cardName != null) this.cardName = cardName;
            else if (description != null)
            {
                int idx = description.IndexOf('：');
                this.cardName = idx > 0 ? description.Substring(0, idx) : description;
            }
            else this.cardName = null;
        }

        /// <summary>卡牌显示名：强化过的牌在名字后加「+」，供 UI 显示强化差分。</summary>
        public string DisplayName()
        {
            return (string.IsNullOrEmpty(cardName) ? "" : cardName) + (upgraded ? "+" : "");
        }

        public string DisplayText()
        {
            if (!string.IsNullOrEmpty(label)) return label;   // 卡面大字显式覆盖（如菌种培育「+2」）
            if (enchant) return "附魔";
            if (induce > 0) return "诱导";
            if (toxinGoal > 0) return "标记";
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

        /// <summary>强化卡牌：回血牌 +2 治疗；诱导牌 +1 诱导量；毒性催化 +3 伤害；有数值的牌数值 +1；0 数值牌（附魔/冰冻）降 1 费。</summary>
        public void Upgrade()
        {
            upgraded = true;
            if (heal > 0) { heal += 2; return; }
            if (induce > 0) { induce += 1; return; }
            if (toxinDamage > 0) { toxinDamage += 3; return; }
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
                n.delayedValueDelta = numbers[i].delayedValueDelta;
                foreach (var m in numbers[i].toxins)
                    n.toxins.Add(new ToxinMark(m.goal, m.damage) { progress = m.progress });
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

    /// <summary>
    /// 运算规则（SPEC 6.3：除法向下取整、下限 0、不设数值上限）。
    /// passive 为职业被动钩子，relics 为遗物钩子。
    /// 注意：本方法只负责算数，不管 GameConfig.MaxValue 的去留——推过上限该由谁承担后果，由调用方（TurnManager）判定。
    /// </summary>
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
            if (result < 0) result = 0;   // 下限 0（到 0 即消灭）；上限由 TurnManager 按「玩家催化 / 自然成长」分别处理
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
