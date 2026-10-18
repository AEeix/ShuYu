using UnityEngine;

namespace ShuYu
{
    /// <summary>
    /// 全局常量与色板。数值参数见 CODEX_START 3.1（写死，后续按 SPEC 调）。
    /// </summary>
    public static class GameConfig
    {
        // ---- 数值参数 ----
        public const int BoardSize = 4;             // 4×4 棋盘
        public const int InitialHealth = 20;        // 血条初始值
        public const int EnergyPerTurn = 3;         // 每回合能量（出牌扣费结算）
        public const int OverflowThreshold = 9;     // 数字 > 9 触发溢出（即 >= 10）
        public const int InitialEnemyValue = 6;     // M0 小怪初始值（可调到 8/9 更快演示溢出）
        public const int CoreGrowth = 3;            // 核心每回合喂养量（占位待调）
        public const int CoreExplosionDamage = 5;   // 核心爆炸固定伤害（占位待调）
        public const int CoreResetValue = 3;        // 核心爆炸后回落值（占位待调）
        public const int InvasionInterval = 2;      // 每 N 回合触发一次数字入侵
        public const int InvasionCount = 2;         // 每次入侵新增的数字个数
        public const int InvasionMinValue = 1;      // 入侵数字值下限（SPEC 4：1~3）
        public const int InvasionMaxValue = 3;      // 入侵数字值上限

        // ---- 色板（SPEC 13.2）----
        public static readonly Color Background = Hex("#0D1117");
        public static readonly Color GridLine = Hex("#1E2430");
        public static readonly Color Accent = Hex("#FFB300");      // 金 / 血条 / 强调
        public static readonly Color CardBlue = Hex("#4DA6FF");    // 减法卡
        public static readonly Color CardOrange = Hex("#FF8C42");  // 加法卡
        public static readonly Color CardGold = Hex("#FFD54F");    // 乘除卡
        public static readonly Color CardBase = Hex("#161B26");    // 卡牌底
        public static readonly Color SubText = Hex("#8892A6");     // 次要文字
        public static readonly Color MonsterFill = Hex("#1E2836"); // 怪物部位暗块填充（略亮于背景，衬托发光轮廓）
        public static readonly Color BoardOutline = Hex("#FFFFFF"); // 棋盘形状（史莱姆/怪物）外轮廓边框

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        /// <summary>卡牌颜色按运算区分（SPEC 13.5：减法蓝 / 加法橙 / 乘除金）。</summary>
        public static Color CardColor(Operation op)
        {
            switch (op)
            {
                case Operation.Add: return CardOrange;
                case Operation.Subtract: return CardBlue;
                default: return CardGold; // Multiply / Divide
            }
        }

        /// <summary>数值 → 颜色（SPEC 13.4 视觉映射）。</summary>
        public static Color NumberColor(int value)
        {
            if (value <= 4) return Hex("#7FB4FF");   // 1~4 冷色
            if (value <= 7) return Hex("#FF8C42");   // 5~7 橙
            if (value == 8) return Hex("#FF5A3B");   // 8 橙红
            if (value == 9) return Hex("#FF2222");   // 9 刺眼红
            return Hex("#FFFFFF");                   // >=10 白热
        }
    }
}
