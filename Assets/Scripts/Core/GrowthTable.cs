namespace ShuYu
{
    /// <summary>
    /// 怪物等级：决定这只怪的自然成长速度。
    /// </summary>
    /// <remarks>
    /// 和地图节点类型（<c>NodeType</c>，在 RouteMap 里）故意分开：
    /// 成长规则属于战斗逻辑，不该依赖「地图/UI 那一层」的枚举，这样离线测试也能直接用。
    /// 地图节点 → 怪物等级 的翻译放在 GameManager（那里本来就在处理节点）。
    /// </remarks>
    public enum MonsterTier
    {
        /// <summary>普通战斗（如史莱姆）。</summary>
        Normal,

        /// <summary>精英战。</summary>
        Elite,

        /// <summary>BOSS 战。</summary>
        Boss,
    }

    /// <summary>
    /// 怪物「每回合自然成长量」的一组数值（部位/普通格一个、核心一个）。
    /// </summary>
    /// <remarks>
    /// 用 <c>struct</c> 而不是 <c>class</c>：这里只是两个 int 的组合，
    /// 值类型复制代价极低，也不会出现「两处引用同一个对象被误改」的问题。
    /// </remarks>
    public struct GrowthProfile
    {
        /// <summary>部位 / 普通格 每回合的自然成长量。</summary>
        public int Part;

        /// <summary>核心 每回合的自然成长量。</summary>
        public int Core;

        public GrowthProfile(int part, int core)
        {
            Part = part;
            Core = core;
        }

        /// <summary>便于日志/调试时直接打印，例如 <c>Part=1 Core=2</c>。</summary>
        public override string ToString()
        {
            return "Part=" + Part + " Core=" + Core;
        }
    }

    /// <summary>
    /// 怪物自然成长表：按「怪物等级」决定它每回合长多少。
    /// <para>
    /// ✅ **想调平衡只改这一张表**，不用去翻战斗代码
    /// （<see cref="TurnManager.ResolveGrowth"/> 只负责把这里的数字加上去）。
    /// </para>
    /// <para>
    /// 当前数值（用户 2026-10-20 给定）：
    /// 普通怪 部位+1 / 核心+2；精英 部位+1 / 核心+3；BOSS 部位+2 / 核心+3。
    /// </para>
    /// </summary>
    public static class GrowthTable
    {
        /// <summary>普通战斗（史莱姆）：部位 +1 / 核心 +2。</summary>
        public static readonly GrowthProfile Normal = new GrowthProfile(1, 2);

        /// <summary>精英战：部位 +1 / 核心 +3。</summary>
        public static readonly GrowthProfile Elite = new GrowthProfile(1, 3);

        /// <summary>BOSS 战：部位 +2 / 核心 +3。</summary>
        public static readonly GrowthProfile Boss = new GrowthProfile(2, 3);

        /// <summary>按怪物等级取成长档位。</summary>
        public static GrowthProfile Of(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Elite: return Elite;
                case MonsterTier.Boss: return Boss;
                default: return Normal;
            }
        }
    }
}
