using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>职业被动类型（改规则钩子）。</summary>
    public enum PassiveType { None, IronGrip, Analyze, Bloodthirst }

    /// <summary>职业数据：名字 + 气质描述 + 被动 + 起始卡组。</summary>
    public class ClassData
    {
        public string id;
        public string displayName;
        public string description;
        public PassiveType passive;
        public List<CardData> startingDeck;

        public ClassData(string id, string displayName, string description, PassiveType passive, List<CardData> startingDeck)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.passive = passive;
            this.startingDeck = startingDeck;
        }
    }

    /// <summary>三职业目录（占位数值，后续调）。</summary>
    public static class ClassCatalog
    {
        public static ClassData Warrior() => new ClassData(
            "warrior", "战 士", "铁腕 · 减法牌数值 +1", PassiveType.IronGrip, CardLibrary.WarriorDeck());

        public static ClassData Mage() => new ClassData(
            "mage", "法 师", "解析 · 除法切干净不留残余", PassiveType.Analyze, CardLibrary.MageDeck());

        public static ClassData Reaper() => new ClassData(
            "reaper", "收割者", "嗜血 · 溢出造成的伤害回血一半", PassiveType.Bloodthirst, CardLibrary.ReaperDeck());

        public static ClassData[] All() => new ClassData[] { Warrior(), Mage(), Reaper() };
    }
}
