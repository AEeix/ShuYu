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
        public const int PlaysPerTurn = 3;          // 每回合出牌数
        public const int OverflowThreshold = 9;     // 数字 > 9 触发溢出（即 >= 10）
        public const int InitialEnemyValue = 6;     // M0 小怪初始值（可调到 8/9 更快演示溢出）

        // ---- 色板（SPEC 13.2）----
        public static readonly Color Background = Hex("#0B0E14");
        public static readonly Color GridLine = Hex("#1E2430");
        public static readonly Color Accent = Hex("#FFB300");      // 金 / 血条 / 强调
        public static readonly Color CardBlue = Hex("#4DA6FF");    // 减法卡
        public static readonly Color CardOrange = Hex("#FF8C42");  // 加法卡
        public static readonly Color CardGold = Hex("#FFD54F");    // 乘除卡
        public static readonly Color CardBase = Hex("#161B26");    // 卡牌底
        public static readonly Color SubText = Hex("#8892A6");     // 次要文字

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
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
