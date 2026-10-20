using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 遗物类型（改规则钩子，SPEC 15）。
    /// <para>⚠️ 这个枚举会被存档序列化成整数，**新成员只能往后加**，不要插在中间或删改顺序。</para>
    /// </summary>
    public enum RelicType
    {
        EnergyCore, Regen, GreedyHand, OverflowShield, BloodRite, IronTalisman, MultiplyBoost, LifeCrystal,
        ManaCookie,   // 法师初始遗物：战斗开始时手牌里多一张衍生牌「法力饼干」
    }

    /// <summary>遗物：类型 + 名字 + 描述（+ 是否只作为初始遗物）。</summary>
    public class Relic
    {
        public RelicType type;
        public string name;
        public string description;
        public bool startingOnly;   // true = 只能作为职业初始遗物，不会出现在随机遗物奖励里

        public Relic(RelicType type, string name, string description, bool startingOnly = false)
        {
            this.type = type;
            this.name = name;
            this.description = description;
            this.startingOnly = startingOnly;
        }
    }

    /// <summary>遗物目录：所有遗物 + 随机抽取 + 名字/描述查询。</summary>
    public static class RelicCatalog
    {
        static readonly System.Random _rng = new System.Random();

        static readonly Relic[] All =
        {
            new Relic(RelicType.EnergyCore, "能量核心", "每回合能量 +1"),
            new Relic(RelicType.Regen, "再生之心", "每回合开始回 1 血"),
            new Relic(RelicType.GreedyHand, "贪婪之手", "手牌上限 +1"),
            new Relic(RelicType.OverflowShield, "溢出护盾", "溢出伤害减半"),
            new Relic(RelicType.BloodRite, "嗜血仪式", "溢出伤害回血一半"),
            new Relic(RelicType.IronTalisman, "铁腕护符", "减法牌数值 +1"),
            new Relic(RelicType.MultiplyBoost, "乘方增幅", "乘法牌数值 +1"),
            new Relic(RelicType.LifeCrystal, "生命水晶", "获得时 +5 血量上限并回满"),
            new Relic(RelicType.ManaCookie, "法力饼干盒", "战斗开始时，手牌里多一张衍生牌「法力饼干」", true),
        };

        /// <summary>随机抽一个可掉落的遗物（排除「只作为初始遗物」的那些）。</summary>
        public static Relic Random() => PickUnowned(null);

        /// <summary>从还没拿过的、可掉落的遗物里随机抽一个（全拿过返回 null）。</summary>
        public static Relic RandomUnowned(List<RelicType> owned) => PickUnowned(owned);

        static Relic PickUnowned(List<RelicType> owned)
        {
            var candidates = new List<Relic>();
            foreach (var r in All)
            {
                if (r.startingOnly) continue;                 // 初始遗物不进随机池
                if (owned != null && owned.Contains(r.type)) continue;
                candidates.Add(r);
            }
            if (candidates.Count == 0) return null;
            return candidates[_rng.Next(candidates.Count)];
        }

        public static string Name(RelicType type) { var r = Find(type); return r != null ? r.name : "?"; }
        public static string Desc(RelicType type) { var r = Find(type); return r != null ? r.description : ""; }

        static Relic Find(RelicType type)
        {
            foreach (var r in All) if (r.type == type) return r;
            return null;
        }
    }
}
