using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>遗物类型（改规则钩子，SPEC 15）。</summary>
    public enum RelicType { EnergyCore, Regen, GreedyHand, OverflowShield, BloodRite, IronTalisman, MultiplyBoost, LifeCrystal }

    /// <summary>遗物：类型 + 名字 + 描述。</summary>
    public class Relic
    {
        public RelicType type;
        public string name;
        public string description;

        public Relic(RelicType type, string name, string description)
        {
            this.type = type;
            this.name = name;
            this.description = description;
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
        };

        /// <summary>随机抽一个遗物。</summary>
        public static Relic Random() => All[_rng.Next(All.Length)];

        /// <summary>从还没拿过的遗物里随机抽一个（全拿过返回 null）。</summary>
        public static Relic RandomUnowned(List<RelicType> owned)
        {
            var candidates = new List<Relic>();
            foreach (var r in All) if (!owned.Contains(r.type)) candidates.Add(r);
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
