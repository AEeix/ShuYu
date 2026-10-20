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
        public const int MaxValue = 99;             // 数字上限（保护 UI/数值）：玩家催化推过此值 → 该格消灭；自然成长推过此值 → 只调回 99 不消灭
        // ---- 溢出机制已取消（怪物数字无限成长），以下三个常量停用，保留待查 ----
        // public const int OverflowThreshold = 9;     // 原：数字 > 9 触发溢出
        // public const int CoreExplosionDamage = 5;   // 原：核心爆炸固定伤害
        // public const int CoreResetValue = 3;        // 原：核心爆炸后回落值
        public const int InitialEnemyValue = 6;     // M0 小怪初始值
        public const int CoreGrowth = 4;            // 核心每回合喂养量（中后期/BOSS 之外的默认值）
        public const int CoreGrowthEarly = 3;       // 前两场战斗：核心喂养量（压难度，给玩家熟悉机制）
        public const int CoreGrowthBoss = 5;        // BOSS 战：核心喂养量（加压）
        public const int PartGrowth = 2;            // 普通/部位每回合增殖量（占位待调）
        public const int EnchantBurnLayers = 2;     // 火焰附魔命中时叠加的灼烧层数
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
        public static readonly Color UpgradedName = Hex("#3DE0A0"); // 强化卡名（亮绿，差分色）
        public static readonly Color CardBase = Hex("#161B26");    // 卡牌底
        public static readonly Color SubText = Hex("#8892A6");     // 次要文字
        public static readonly Color MonsterFill = Hex("#1E2836"); // 怪物部位暗块填充（略亮于背景，衬托发光轮廓）
        public static readonly Color BoardOutline = Hex("#FFFFFF"); // 棋盘形状（史莱姆/怪物）外轮廓边框
        public static readonly Color ToxinCell = new Color(0.72f, 0.62f, 0.95f, 0.55f); // 带毒性标记的格子底色（浅紫）
        public static readonly Color BurnCell = new Color(0.95f, 0.32f, 0.1f, 0.6f);    // 带灼烧的格子底色（橙红）
        public static readonly Color BurnFlame = Hex("#FF7A1A");                         // 火焰特效主色
        public static readonly Color BurnFlameHot = Hex("#FFD84D");                      // 火焰特效高光

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
