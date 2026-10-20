using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 地块「关键词」：卡面和图鉴里会用到的状态名。
    /// 加进来的关键词有两处自动生效：① 卡牌文本里出现时自动加粗；② 出现在关键词图鉴里。
    /// </summary>
    public enum Keyword
    {
        /// <summary>毒性标记（可叠加，按各自阈值引爆）。</summary>
        Mark,

        /// <summary>灼烧（按层数在玩家回合开始时持续扣值）。</summary>
        Burn,

        /// <summary>冻结（按层数让该格停止行动）。</summary>
        Freeze,
    }

    /// <summary>关键词条目：名字 + 说明（说明里的关键词也会自动加粗）。</summary>
    public class KeywordDef
    {
        public Keyword type;
        public string name;
        public string description;

        public KeywordDef(Keyword type, string name, string description)
        {
            this.type = type;
            this.name = name;
            this.description = description;
        }
    }

    /// <summary>
    /// 关键词目录（单一数据源）。
    /// <para>
    /// 想加关键词：① 在 <see cref="Keyword"/> 里加一个成员；② 在 <see cref="Entries"/> 里加一条说明。
    /// 卡牌文本只要写上关键词的名字，UI 层调用 <see cref="Highlight"/> 就会自动加粗，不用手写富文本标签。
    /// </para>
    /// </summary>
    public static class KeywordCatalog
    {
        static readonly KeywordDef[] All =
        {
            new KeywordDef(Keyword.Mark, "标记",
                "毒性标记：在该格累计 x 点非自然成长后，该地块会受到 y 点伤害（x 是阈值、y 是伤害，由具体卡牌决定）。同一格可以挂多个标记，各自独立计数。"),

            new KeywordDef(Keyword.Burn, "灼烧",
                "在该格的回合结束时，受到 x 点伤害，可叠加。"),

            new KeywordDef(Keyword.Freeze, "冻结",
                "跳过该格接下来的 x 个攻击回合，并封锁自然成长。"),
        };

        /// <summary>全部关键词（顺序即图鉴里的显示顺序）。</summary>
        public static IReadOnlyList<KeywordDef> Entries => All;

        /// <summary>取关键词名字（如「灼烧」）。</summary>
        public static string Name(Keyword type)
        {
            foreach (var def in All)
                if (def.type == type) return def.name;
            return "?";
        }

        /// <summary>取关键词说明（已把里面的关键词加粗）。</summary>
        public static string Description(Keyword type)
        {
            foreach (var def in All)
                if (def.type == type) return Highlight(def.description);
            return "";
        }

        /// <summary>
        /// 把文本里出现的所有关键词名字包上 TMP 的加粗标签 <c>&lt;b&gt;…&lt;/b&gt;</c>。
        /// 这样卡牌描述只要写「施加 灼烧+2」，显示时「灼烧」自动变粗，不用到处手写标签。
        /// </summary>
        /// <remarks>如果文本里已经含有 <c>&lt;b&gt;</c>（说明调用方自己标过了），就直接原样返回，避免重复包裹。</remarks>
        public static string Highlight(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Contains("<b>")) return text;

            foreach (var def in All)
                text = text.Replace(def.name, "<b>" + def.name + "</b>");
            return text;
        }
    }
}
