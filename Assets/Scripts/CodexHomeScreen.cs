using UnityEngine;
using TMPro;

namespace ShuYu
{
    /// <summary>
    /// 图鉴主页（图鉴枢纽）：版式和标题主页完全一致 —— 世界坐标轴底图 + 左下角「数域」大字 + 左侧竖排按钮，
    /// 只是把主页那排按钮换成「卡牌图鉴 / 关键词图鉴 / 遗物图鉴 / 怪物图鉴 / 返回」。
    /// <para>
    /// 为什么要加这一层：图鉴越来越多（卡牌 / 关键词 / 遗物 / 怪物），如果让它们互相跳转很快就会变成一张网。
    /// 统一成星形结构「标题主页 → 图鉴主页 → 各子页 → 返回图鉴主页」，子页只认图鉴主页，不认标题页，
    /// 以后再加「BOSS 图鉴」之类只需要在这里多摆一个槽位。
    /// </para>
    /// <para>
    /// ⚠️ 坐标与 <see cref="TitleScreen"/>、<c>Assets/Editor/SceneBuilder.BuildTitle</c> 是同一套
    /// （OriginX=150、按钮高 70 + 间距 10、从 540 往下排），改一处就要同步另外两处，否则两个页面的版式会错位。
    /// </para>
    /// </summary>
    public class CodexHomeScreen : MonoBehaviour
    {
        const float OriginX = 150f;   // 左侧留白（与主页一致）
        const float Width = 360f;     // 按钮列宽度（与主页一致）

        RectTransform _root;

        void Start()
        {
            var canvas = UIFactory.EnsureCanvas();
            _root = UIFactory.CreateRect("CodexHomeRoot", canvas.transform);
            UIFactory.Stretch(_root);

            // 标题：和主页同一个位置、字号、颜色（视觉上就是"同一个界面，换了按钮"）
            var title = UIFactory.CreateText("Title", _root, "数域", 144, GameConfig.Accent);
            title.alignment = TextAlignmentOptions.Left;
            UIFactory.PlaceAbs(title.rectTransform, OriginX + 60, 640, OriginX + Width, 820);

            // 槽位 0 是主入口（金底），其余为次级（深底），最后一个固定是返回
            AddSlot(0, "卡牌图鉴", 44, GameConfig.Accent, GameConfig.Background,
                () => GameManager.Instance.ShowCodex());
            AddSlot(1, "关键词图鉴", 40, GameConfig.CardBase, GameConfig.Accent,
                () => GameManager.Instance.ShowKeywords());
            AddSlot(2, "遗物图鉴", 40, GameConfig.CardBase, GameConfig.Accent,
                () => GameManager.Instance.ShowRelicCodex());
            AddSlot(3, "怪物图鉴", 40, GameConfig.CardBase, GameConfig.SubText,
                () => GameManager.Instance.ShowMonsterCodex(), pending: true);
            AddSlot(4, "返 回", 36, GameConfig.CardBase, GameConfig.SubText,
                () => GameManager.Instance.ShowTitle());
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>
        /// 在主页同一列的位置上摆一个按钮。slot 0 = 主页主按钮的位置（540~630，高 90），
        /// 之后每个槽位 80 往下排、按钮本体高 70（和主页的 设置 / 图鉴 / 退出 三个槽位对齐）。
        /// </summary>
        /// <param name="pending">true = 还没做内容，按钮右侧挂一个「占位」小标（点进去是占位屏）。</param>
        void AddSlot(int slot, string label, float fontSize, Color bg, Color text,
            UnityEngine.Events.UnityAction onClick, bool pending = false)
        {
            float top = slot == 0 ? 630f : 530f - (slot - 1) * 80f;
            float bottom = slot == 0 ? 540f : top - 70f;

            var btn = UIFactory.CreateButton("Codex_" + label, _root, label, fontSize, bg, text, onClick);
            UIFactory.PlaceAbs(btn, OriginX + 60, bottom, OriginX + Width, top);

            if (!pending) return;
            var tag = UIFactory.CreateText("Pending", btn, "占位", 22, GameConfig.SubText);
            tag.alignment = TextAlignmentOptions.Right;
            UIFactory.Place(tag.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-16, 0));
        }
    }
}
