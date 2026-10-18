using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>怪物形状棋盘里的单个部位/核心（纯数据，供 SpawnBoard 铺棋盘，SPEC 10）。</summary>
    public struct MonsterCell
    {
        public int row, col;        // 棋盘坐标
        public int value;           // 初始数字值
        public NumberKind kind;     // Part / Core
        public int attackOrder;     // Part 用：固定攻击顺序（全局唯一）
        public int coreGroup;       // 归属核心分组（Part 指向核心编号；Core 自带编号）

        public MonsterCell(int row, int col, int value, NumberKind kind, int attackOrder = 0, int coreGroup = 0)
        {
            this.row = row;
            this.col = col;
            this.value = value;
            this.kind = kind;
            this.attackOrder = attackOrder;
            this.coreGroup = coreGroup;
        }
    }

    /// <summary>一个怪物形状：棋盘尺寸 + 部位/核心列表（供 SpawnBoard 铺棋盘，支持矩形棋盘）。</summary>
    public struct MonsterShape
    {
        public int rows, cols;
        public List<MonsterCell> cells;

        public MonsterShape(int rows, int cols, List<MonsterCell> cells)
        {
            this.rows = rows;
            this.cols = cols;
            this.cells = cells;
        }
    }

    /// <summary>怪物形状目录：精英 3 种单核心轮廓 + BOSS 1 种双核心轮廓（数值占位，待调）。</summary>
    public static class MonsterShapes
    {
        static readonly System.Random _rng = new System.Random();

        /// <summary>精英「龙形」：头 + 双爪 + 胸口核心。</summary>
        public static MonsterShape Dragon()
        {
            return new MonsterShape(4, 4, new List<MonsterCell>
            {
                new MonsterCell(0, 1, 4, NumberKind.Part, 0, 1), // 头
                new MonsterCell(1, 1, 3, NumberKind.Part, 1, 1), // 身（连头与核心，让轮廓成块）
                new MonsterCell(1, 0, 3, NumberKind.Part, 2, 1), // 左爪
                new MonsterCell(1, 2, 3, NumberKind.Part, 3, 1), // 右爪
                new MonsterCell(2, 1, 6, NumberKind.Core, 0, 1), // 核心
            });
        }

        /// <summary>精英「蛇形」：纵向 头/身/核/尾。</summary>
        public static MonsterShape Serpent()
        {
            return new MonsterShape(4, 4, new List<MonsterCell>
            {
                new MonsterCell(0, 1, 4, NumberKind.Part, 0, 1), // 头
                new MonsterCell(1, 1, 4, NumberKind.Part, 1, 1), // 身
                new MonsterCell(2, 1, 6, NumberKind.Core, 0, 1), // 核心
                new MonsterCell(3, 1, 3, NumberKind.Part, 2, 1), // 尾
            });
        }

        /// <summary>精英「蟹形」：大矩形身体（壳）+ 左下/右下各一个 2×2 大钳，棋盘加宽到 6 列让双钳隔开。</summary>
        public static MonsterShape Crab()
        {
            return new MonsterShape(4, 6, new List<MonsterCell>
            {
                // 身体：2×4（壳，居中）
                new MonsterCell(0, 1, 3, NumberKind.Part, 8, 1),
                new MonsterCell(0, 2, 3, NumberKind.Part, 9, 1),
                new MonsterCell(0, 3, 3, NumberKind.Part, 10, 1),
                new MonsterCell(0, 4, 3, NumberKind.Part, 11, 1),
                new MonsterCell(1, 1, 3, NumberKind.Part, 12, 1),
                new MonsterCell(1, 2, 6, NumberKind.Core, 0, 1),   // 核心（身体中央）
                new MonsterCell(1, 3, 3, NumberKind.Part, 13, 1),
                new MonsterCell(1, 4, 3, NumberKind.Part, 14, 1),
                // 左下钳：2×2
                new MonsterCell(2, 0, 4, NumberKind.Part, 0, 1),
                new MonsterCell(2, 1, 4, NumberKind.Part, 1, 1),
                new MonsterCell(3, 0, 4, NumberKind.Part, 2, 1),
                new MonsterCell(3, 1, 4, NumberKind.Part, 3, 1),
                // 右下钳：2×2
                new MonsterCell(2, 4, 4, NumberKind.Part, 4, 1),
                new MonsterCell(2, 5, 4, NumberKind.Part, 5, 1),
                new MonsterCell(3, 4, 4, NumberKind.Part, 6, 1),
                new MonsterCell(3, 5, 4, NumberKind.Part, 7, 1),
            });
        }

        /// <summary>BOSS「双头龙」：双核心各喂养一侧部位，打爆一核 → 该侧部位一起坏死（SPEC 10.5）。</summary>
        public static MonsterShape BossDragon()
        {
            return new MonsterShape(4, 4, new List<MonsterCell>
            {
                new MonsterCell(0, 1, 5, NumberKind.Part, 0, 1), // 头（核A 侧）
                new MonsterCell(0, 2, 5, NumberKind.Part, 3, 2), // 尾（核B 侧）
                new MonsterCell(1, 0, 4, NumberKind.Part, 1, 1), // 左爪（核A 侧）
                new MonsterCell(1, 1, 7, NumberKind.Core, 0, 1), // 核A
                new MonsterCell(1, 2, 7, NumberKind.Core, 0, 2), // 核B
                new MonsterCell(1, 3, 4, NumberKind.Part, 4, 2), // 右爪（核B 侧）
                new MonsterCell(2, 0, 4, NumberKind.Part, 2, 1), // 左腿（核A 侧）
                new MonsterCell(2, 3, 4, NumberKind.Part, 5, 2), // 右腿（核B 侧）
            });
        }

        /// <summary>普通怪「史莱姆」：瘤块 + 中央核心（心脏）。</summary>
        public static MonsterShape Slime()
        {
            return new MonsterShape(4, 4, new List<MonsterCell>
            {
                new MonsterCell(1, 1, 2, NumberKind.Part, 0, 1), // 瘤块
                new MonsterCell(1, 2, 3, NumberKind.Part, 1, 1), // 瘤块
                new MonsterCell(2, 1, 3, NumberKind.Part, 2, 1), // 瘤块
                new MonsterCell(2, 2, 5, NumberKind.Core, 0, 1), // 核心（心脏）
            });
        }

        /// <summary>精英「尖叫花」：四瓣 + 中心嘴（核心）+ 茎。</summary>
        public static MonsterShape ScreamingFlower()
        {
            return new MonsterShape(4, 4, new List<MonsterCell>
            {
                new MonsterCell(0, 1, 4, NumberKind.Part, 0, 1), // 上瓣
                new MonsterCell(1, 0, 3, NumberKind.Part, 1, 1), // 左瓣
                new MonsterCell(1, 1, 6, NumberKind.Core, 0, 1), // 核心（嘴）
                new MonsterCell(1, 2, 3, NumberKind.Part, 2, 1), // 右瓣
                new MonsterCell(2, 1, 3, NumberKind.Part, 3, 1), // 下瓣
                new MonsterCell(3, 1, 3, NumberKind.Part, 4, 1), // 茎
            });
        }

        /// <summary>最近一次精英形状的名字（供 UI 显示）。</summary>
        public static string LastEliteName { get; private set; }

        /// <summary>精英形状四选一随机。</summary>
        public static MonsterShape EliteRandom()
        {
            switch (_rng.Next(4))
            {
                case 0: LastEliteName = "龙"; return Dragon();
                case 1: LastEliteName = "蛇"; return Serpent();
                case 2: LastEliteName = "巨蟹"; return Crab();
                default: LastEliteName = "尖叫花"; return ScreamingFlower();
            }
        }
    }
}
