using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>职业被动类型（改规则钩子）。</summary>
    public enum PassiveType { None, IronGrip, Analyze, Bloodthirst, Harvest }

    /// <summary>职业数据：名字 + 气质描述 + 被动 + 起始卡组 + 职业标记 + 初始遗物。</summary>
    public class ClassData
    {
        public string id;
        public string displayName;
        public string description;
        public PassiveType passive;
        public List<CardData> startingDeck;
        public ClassTag tag;      // 职业成员标记：卡牌上标了这个 tag 的，就属于本职业专属牌池
        public int initialHealth; // 初始生命值
        public RelicType? startingRelic;   // 开局自带的遗物（null = 没有；`RelicType?` 是可空枚举）

        public ClassData(string id, string displayName, string description, PassiveType passive, List<CardData> startingDeck,
            ClassTag tag = ClassTag.None, int initialHealth = GameConfig.InitialHealth, RelicType? startingRelic = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.passive = passive;
            this.startingDeck = startingDeck;
            this.tag = tag;
            this.initialHealth = initialHealth;
            this.startingRelic = startingRelic;
        }
    }

    /// <summary>三职业目录（占位数值，后续调）。</summary>
    public static class ClassCatalog
    {
        public static ClassData Warrior() => new ClassData(
            "warrior", "战 士", "铁腕 · 减法牌数值 +1", PassiveType.IronGrip, CardLibrary.WarriorDeck(),
            ClassTag.Warrior, 40);

        /// <summary>
        /// 法师：初始遗物「法力饼干盒」→ 每场战斗开始时手牌里多一张衍生牌「法力饼干」。
        /// 选职业界面「法师」下面显示的那行字就是这个 description（用户 2026-10-20 指定文案）。
        /// </summary>
        public static ClassData Mage() => new ClassData(
            "mage", "法 师", "法力饼干：储存法力施放魔法", PassiveType.Analyze, CardLibrary.MageDeck(),
            ClassTag.Mage, 25, RelicType.ManaCookie);

        public static ClassData Reaper() => new ClassData(
            "reaper", "收割者", "收割 · 怪物获得非自然成长时，所有格子 -1", PassiveType.Harvest, CardLibrary.ReaperDeck(),
            ClassTag.Reaper, 30);

        public static ClassData[] All() => new ClassData[] { Warrior(), Mage(), Reaper() };
    }
}
