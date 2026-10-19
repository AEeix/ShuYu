using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShuYu
{
    /// <summary>对局界面：血条 + 4×4 棋盘 + 手牌 + 结束回合 + 胜负覆盖层。</summary>
    public class BattleScreen : MonoBehaviour
    {
        CardData _selectedCard; // 当前选中的卡（null = 未选）
        RectTransform _root;
        readonly List<CardView> _handCards = new List<CardView>();
        TMP_Text _hintText;
        TMP_Text _hpText;
        RectTransform[,] _cellRects;                     // 格子引用（目标高亮）
        readonly List<GameObject> _highlights = new List<GameObject>(); // 高亮覆盖层
        GameObject _pileViewer;   // 抽/弃牌堆查看弹窗
        bool _skipDealAnim;       // 回合结算逐步播放时，跳过抽牌动画
        SpriteAnimator _monsterAnim;   // 怪物立绘动画（crab/flower；slime 静态为 null）

        void Start() { Build(); }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
            if (_pileViewer != null) Destroy(_pileViewer);
        }

        void Build()
        {
            if (_root != null) Destroy(_root.gameObject);

            var canvas = UIFactory.EnsureCanvas();
            var go = new GameObject("BattleRoot", typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            UIFactory.Stretch(_root);

            var bg = UIFactory.CreatePanel("BG", _root, GameConfig.Background);
            UIFactory.Stretch(bg.rectTransform);

            BuildBlood(_root);
            BuildBoard(_root);
            BuildMonsterName(_root);
            BuildHand(_root);
            BuildClassInfo(_root);
            BuildMonsterView(_root);
            BuildOverlay(_root);
        }

        void BuildBlood(Transform parent)
        {
            var gm = GameManager.Instance;

            // 血量（左下角）：HP：XX
            var hp = UIFactory.CreateText("HP", parent, "HP：" + gm.Player.health, 56, GameConfig.Accent);
            _hpText = hp;
            UIFactory.Place(hp.rectTransform,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(60, 40), new Vector2(400, 110));

            // 提示（顶部）
            _hintText = UIFactory.CreateText("Hint", parent, _selectedCard == null
                    ? "点一张卡，再点棋盘上的数字出牌"
                    : "已选 " + _selectedCard.DisplayText() + "，点数字出牌",
                36, GameConfig.SubText);
            UIFactory.Place(_hintText.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -60), new Vector2(0, -20));

            // 回合数（右上角）：帮玩家预判「每 2 回合入侵」的时机
            var turn = UIFactory.CreateText("Turn", parent, "回合 " + gm.Turn.turnNumber, 40, GameConfig.SubText);
            UIFactory.Place(turn.rectTransform,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-240, -50), new Vector2(-40, -10));
        }

        void BuildBoard(Transform parent)
        {
            var gm = GameManager.Instance;
            var board = gm.Board;
            int rows = board.rows;
            int cols = board.cols;

            _cellRects = new RectTransform[rows, cols];
            _highlights.Clear();

            // 棋盘高度固定，格子大小按行数算；宽度随列数变化（巨蟹 6 列更宽）
            float boardArea = 360f;
            float cellSize = boardArea / rows;
            float halfH = boardArea * 0.5f;
            float boardWidth = cellSize * cols;
            float halfW = boardWidth * 0.5f;
            float upShift = 120f;     // 上移棋盘

            var container = UIFactory.CreateRect("Board", parent);
            UIFactory.Place(container,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-halfW, -halfH + upShift), new Vector2(halfW, halfH + upShift));

            // 精英/BOSS 用「怪物形状」；普通小怪（史莱姆，含问号遭遇战）用「史莱姆形状」（切掉四角）
            bool isMonster = GameManager.Instance.IsShapeBoard;
            bool cutCorners = !isMonster;

            // 棋盘底：史莱姆战填 CardBase（史莱姆身体），怪物战融入背景
            var boardBg = container.gameObject.AddComponent<Image>();
            boardBg.color = isMonster ? GameConfig.Background : GameConfig.CardBase;

            // 格子：透明按钮层，只负责点击 + 显示数字
            var cells = UIFactory.CreateRect("Cells", container);
            UIFactory.Stretch(cells);
            var grid = cells.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = Vector2.zero;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    CreateCell(cells, row, col, cellSize, cutCorners && board.IsCornerCell(row, col));

            // 棋盘形状的轮廓（内部格子线 + 外轮廓边框）统一画在最上层
            DrawShapeOutline(container, rows, cols, cellSize, isMonster);
        }

        void DrawGridLine(Transform container, int i, float cellSize, float lineW, float boardArea)
        {
            float p = i * cellSize;

            var vLine = UIFactory.CreatePanel("VLine_" + i, container, GameConfig.SubText);
            UIFactory.Place(vLine.rectTransform,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(p - lineW / 2f, 0), new Vector2(p + lineW / 2f, boardArea));

            var hLine = UIFactory.CreatePanel("HLine_" + i, container, GameConfig.SubText);
            UIFactory.Place(hLine.rectTransform,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, p - lineW / 2f), new Vector2(boardArea, p + lineW / 2f));
        }

        /// <summary>格子是否属于「棋盘形状」：史莱姆=非切角格；怪物=部位/核心格。</summary>
        bool IsShapeCell(BoardState board, bool isMonster, int row, int col)
        {
            if (row < 0 || row >= board.rows || col < 0 || col >= board.cols) return false; // 出界 → 非形状（该边画外轮廓）
            if (isMonster)
            {
                var d = board.At(row, col);
                return d != null && d.kind != NumberKind.Normal;
            }
            return !board.IsCornerCell(row, col);
        }

        /// <summary>画棋盘形状（史莱姆/怪物）的轮廓：先内部格子线（SubText），再外轮廓边框（白色，盖在最上）。</summary>
        void DrawShapeOutline(Transform container, int rows, int cols, float cellSize, bool isMonster)
        {
            var board = GameManager.Instance.Board;
            const float outerW = 10f;   // 外轮廓白边（加粗）
            const float innerW = 10f;   // 内部格子线（与外轮廓同粗）

            // 第一遍：内部格子线（只查右/下，避免重复画）
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                if (!IsShapeCell(board, isMonster, row, col)) continue;

                float x0 = col * cellSize, x1 = (col + 1) * cellSize;
                float y0 = (rows - 1 - row) * cellSize, y1 = (rows - row) * cellSize;

                if (IsShapeCell(board, isMonster, row, col + 1)) DrawEdge(container, x1, y0, x1, y1, innerW, GameConfig.SubText, false);
                if (IsShapeCell(board, isMonster, row + 1, col)) DrawEdge(container, x0, y0, x1, y0, innerW, GameConfig.SubText, true);
            }

            // 第二遍：外轮廓（白色，画在最上层，盖住内部线越界到边界处的部分）
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                if (!IsShapeCell(board, isMonster, row, col)) continue;

                float x0 = col * cellSize, x1 = (col + 1) * cellSize;
                float y0 = (rows - 1 - row) * cellSize, y1 = (rows - row) * cellSize;

                if (!IsShapeCell(board, isMonster, row - 1, col)) DrawEdge(container, x0, y1, x1, y1, outerW, GameConfig.BoardOutline, true);
                if (!IsShapeCell(board, isMonster, row + 1, col)) DrawEdge(container, x0, y0, x1, y0, outerW, GameConfig.BoardOutline, true);
                if (!IsShapeCell(board, isMonster, row, col - 1)) DrawEdge(container, x0, y0, x0, y1, outerW, GameConfig.BoardOutline, false);
                if (!IsShapeCell(board, isMonster, row, col + 1)) DrawEdge(container, x1, y0, x1, y1, outerW, GameConfig.BoardOutline, false);
            }
        }

        void DrawEdge(Transform container, float x0, float y0, float x1, float y1, float w, Color color, bool horizontal)
        {
            var seg = UIFactory.CreatePanel(horizontal ? "EdgeH" : "EdgeV", container, color);
            seg.raycastTarget = false;
            // 两端各多伸 w/2，让相邻边在转角处重叠，避免留出深色缺口
            if (horizontal)
                UIFactory.Place(seg.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(x0 - w / 2f, y0 - w / 2f), new Vector2(x1 + w / 2f, y0 + w / 2f));
            else
                UIFactory.Place(seg.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(x0 - w / 2f, y0 - w / 2f), new Vector2(x0 + w / 2f, y1 + w / 2f));
        }

        void CreateCell(Transform parent, int row, int col, float cellSize, bool cutCorner)
        {
            var gm = GameManager.Instance;
            var data = gm.Board.At(row, col);

            var go = new GameObject("Cell_" + row + "_" + col, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();

            if (cutCorner)
            {
                // 切角格：盖回背景色（史莱姆形状被切掉的角），不可点、无数字
                img.color = GameConfig.Background;
                img.raycastTarget = false;
                return;
            }

            bool isAttacker = gm.Turn.attacker != null && gm.Turn.attacker.row == row && gm.Turn.attacker.col == col;

            // 格子状态：灼烧（橙红底 + 火焰特效）优先于毒性标记（浅紫底）
            bool hasBurn = data != null && data.burn > 0;
            bool hasToxin = data != null && data.toxins.Count > 0;

            if (data != null && data.kind == NumberKind.Part)
            {
                // 部位：暗块填充（棋盘里的一块暗块）；攻击部位更亮橙红
                img.color = isAttacker ? new Color(1f, 0.35f, 0.23f, 0.9f) : GameConfig.MonsterFill;
            }
            else if (data != null && data.kind == NumberKind.Core)
            {
                // 核心：金色描边 + 脉冲发光；带灼烧 → 橙红脉冲，带毒性 → 紫色脉冲（金描边保留，仍能看出是核心）
                img.color = hasBurn ? GameConfig.BurnCell
                          : hasToxin ? GameConfig.ToxinCell
                          : new Color(1f, 0.7f, 0f, 0.45f);
                UIFactory.AddOutline(img, GameConfig.Accent, new Vector2(3f, -3f));
                var pulse = img.gameObject.AddComponent<Pulse>();
                if (hasBurn)
                    pulse.Setup(img, new Color(0.85f, 0.28f, 0.08f, 0.45f), new Color(1f, 0.55f, 0.15f, 0.8f));
                else if (hasToxin)
                    pulse.Setup(img, new Color(0.62f, 0.5f, 0.88f, 0.45f), new Color(0.78f, 0.68f, 1f, 0.8f));
                else
                    pulse.Setup(img, new Color(1f, 0.7f, 0f, 0.25f), new Color(1f, 0.7f, 0f, 0.6f));
            }
            else
                img.color = new Color(0, 0, 0, 0);                 // 透明底，露出网格线

            if (hasBurn) img.color = GameConfig.BurnCell;
            else if (hasToxin && data.kind != NumberKind.Core) img.color = GameConfig.ToxinCell;

            // 灼烧 + 毒性同时在：底色给橙红（灼烧更危险），叠一层薄紫提示毒性
            if (hasBurn && hasToxin)
            {
                var ov = UIFactory.CreatePanel("ToxinHint", rt, new Color(0.55f, 0.35f, 1f, 0.22f));
                ov.raycastTarget = false;
                UIFactory.Stretch(ov.rectTransform);
            }

            // 冰冻：冰蓝覆盖层（盖在格子底色上、文字之下）
            if (data != null && data.frozen)
            {
                var ov = UIFactory.CreatePanel("Frozen", rt, new Color(0.3f, 0.6f, 1f, 0.4f));
                ov.raycastTarget = false;
                UIFactory.Stretch(ov.rectTransform);
            }

            // 火焰特效：格子底部一排闪烁火苗，层数越多越旺（画在数字之前 → 数字压在火上面）
            if (hasBurn)
            {
                var flame = rt.gameObject.AddComponent<CellFlame>();
                flame.Setup(rt, data.burn);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            int r = row, c = col;
            btn.onClick.AddListener(() => OnCellClicked(r, c));

            // 悬停检测：出牌目标高亮
            _cellRects[row, col] = rt;
            var hover = go.AddComponent<CellHover>();
            int hr = row, hc = col;
            hover.onHover = (h) => OnCellHover(hr, hc, h);

            if (data != null)
            {
                // 核心文字用白（金色底更清晰），其余按危险度着色（SPEC 13.4）
                Color textColor = (data.kind == NumberKind.Core) ? Color.white : GameConfig.NumberColor(data.value);
                var txt = UIFactory.CreateText("Value", rt, data.value.ToString(), cellSize * 0.5f, textColor);
                UIFactory.Stretch(txt.rectTransform);

                // 高危数字颤抖（SPEC 13.4：8 明显 / 9 剧烈）
                if (data.value >= 8)
                    txt.gameObject.AddComponent<NumberShake>().Setup(data.value >= 9 ? 3.5f : 1.5f);
            }

            if (isAttacker)
            {
                var mark = UIFactory.CreateText("Atk", rt, "!", cellSize * 0.3f, Color.white);
                var mrt = mark.rectTransform;
                mrt.anchorMin = new Vector2(1, 1);
                mrt.anchorMax = new Vector2(1, 1);
                mrt.pivot = new Vector2(1, 1);
                mrt.anchoredPosition = new Vector2(-4, -2);
                mrt.sizeDelta = new Vector2(cellSize * 0.3f, cellSize * 0.3f);
            }

            // 状态数值（格子底部一行）：灼烧层数「火N」+ 毒性标记进度「已累计/所需」（多个用 · 分隔）
            var statusText = new System.Text.StringBuilder();
            if (hasBurn) statusText.Append("火").Append(data.burn);
            if (hasToxin)
            {
                foreach (var m in data.toxins)
                {
                    statusText.Append(statusText.Length > 0 ? " · " : "");
                    statusText.Append(m.progress).Append('/').Append(m.goal);
                }
            }
            if (statusText.Length > 0)
            {
                var tp = UIFactory.CreateText("Status", rt, statusText.ToString(), cellSize * 0.18f,
                    new Color(0.16f, 0.06f, 0.08f));
                UIFactory.Place(tp.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(2, 2), new Vector2(-2, 2 + cellSize * 0.26f));
            }

            // 延迟数值（菌种培育：下个回合该格 -4）：左上角标出来，让玩家知道还没结算
            if (data != null && data.delayedValueDelta != 0)
            {
                var dp = UIFactory.CreateText("Pending", rt, data.delayedValueDelta.ToString(), cellSize * 0.2f,
                    new Color(1f, 0.55f, 0.15f));
                var drt = dp.rectTransform;
                drt.anchorMin = new Vector2(0, 1);
                drt.anchorMax = new Vector2(0, 1);
                drt.pivot = new Vector2(0, 1);
                drt.anchoredPosition = new Vector2(3, -2);
                drt.sizeDelta = new Vector2(cellSize * 0.5f, cellSize * 0.24f);
            }

        }

        void BuildHand(Transform parent)
        {
            var gm = GameManager.Instance;

            // 能量（本回合剩余 / 上限）
            var plays = UIFactory.CreateText("Energy", parent,
                "能量 " + gm.Turn.energy + " / " + gm.Turn.energyPerTurn,
                36, GameConfig.Accent);
            UIFactory.Place(plays.rectTransform,
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-500, 40), new Vector2(-40, 90));

            // 抽牌堆 / 弃牌堆：可点击查看里面有哪些牌
            var deck = gm.Turn.deck;
            var drawBtn = UIFactory.CreateButton("DrawPile", parent, "抽牌堆 " + deck.drawPile.Count, 32,
                GameConfig.CardBase, GameConfig.SubText, () => ShowPileViewer(deck.drawPile, "抽牌堆"));
            UIFactory.Place(drawBtn, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 120), new Vector2(230, 170));

            var discBtn = UIFactory.CreateButton("DiscardPile", parent, "弃牌堆 " + deck.discardPile.Count, 32,
                GameConfig.CardBase, GameConfig.SubText, () => ShowPileViewer(deck.discardPile, "弃牌堆"));
            UIFactory.Place(discBtn, new Vector2(0, 0), new Vector2(0, 0), new Vector2(240, 120), new Vector2(410, 170));

            // 手牌（1~5 张，杀戮尖塔式：竖长牌形 + 圆角 + 描边，底部排开）
            var hand = gm.HandCards;
            int n = hand.Count;
            const float cardW = 180f;   // 扩大卡面
            const float cardH = 280f;
            const float gap = 20f;
            float totalW = n * cardW + (n - 1) * gap;
            float startX = -totalW / 2f;

            _handCards.Clear();

            for (int i = 0; i < n; i++)
            {
                var card = hand[i];
                bool selected = _selectedCard != null && ReferenceEquals(_selectedCard, card);

                float centerX = startX + cardW / 2f + i * (cardW + gap);
                var go = new GameObject("Card_" + card.DisplayText(), typeof(RectTransform));
                var cardRt = go.GetComponent<RectTransform>();
                cardRt.SetParent(parent, false);
                UIFactory.Place(cardRt,
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(centerX - cardW / 2f, 30), new Vector2(centerX + cardW / 2f, 30 + cardH));

                var view = go.AddComponent<CardView>();
                view.Setup(card, OnCardClicked, selected);
                view.SetEnchanted(gm.Turn.FireEnchanted);
                if (!_skipDealAnim) view.PlayDeal(i * 0.05f);
                _handCards.Add(view);
            }

            // 结束回合按钮
            var endBtn = UIFactory.CreateButton("EndTurn", parent, "结束回合", 48,
                GameConfig.GridLine, GameConfig.SubText,
                () => OnEndTurnClicked());
            UIFactory.Place(endBtn,
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-260, 110), new Vector2(-40, 210));
        }

        /// <summary>怪物名字（棋盘下方）：史莱姆 / 精英 / BOSS。</summary>
        void BuildMonsterName(Transform parent)
        {
            var name = GameManager.Instance.MonsterName;
            var t = UIFactory.CreateText("MonsterName", parent, name, 44, GameConfig.Accent);
            t.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(t.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300, -150), new Vector2(300, -90));
        }

        void BuildClassInfo(Transform parent)
        {
            var cls = GameManager.Instance.Turn.playerClass;
            var text = UIFactory.CreateText("Class", parent, cls.displayName + " · " + cls.description, 32, GameConfig.SubText);
            text.alignment = TextAlignmentOptions.Left;
            UIFactory.Place(text.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(60, -260), new Vector2(560, -220));

            // 遗物列表
            var gm = GameManager.Instance;
            string relicStr = "遗物：";
            if (gm.Player.relics.Count == 0) relicStr += "无";
            else
                for (int i = 0; i < gm.Player.relics.Count; i++)
                    relicStr += (i > 0 ? " · " : "") + RelicCatalog.Name(gm.Player.relics[i]);
            var relicText = UIFactory.CreateText("Relics", parent, relicStr, 28, GameConfig.CardGold);
            relicText.alignment = TextAlignmentOptions.Left;
            UIFactory.Place(relicText.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(60, -300), new Vector2(560, -262));
        }

        /// <summary>怪物立绘（左侧）：史莱姆静态图；尖叫花/巨蟹逐帧动画；龙/蛇/BOSS 无立绘（纯形状棋盘）。整体上移。</summary>
        void BuildMonsterView(Transform parent)
        {
            _monsterAnim = null;
            string key = MonsterKey();
            if (key == null) return;

            var img = UIFactory.CreatePanel("MonsterView", parent, Color.white);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            // 立绘：左侧，上移（原垂直居中 y=-200~200，现 y=0~400，中心 +200）
            UIFactory.Place(img.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(40, 0), new Vector2(440, 400));

            if (key == "slime")
            {
                var slime = UIFactory.Slime;
                if (slime == null) { Destroy(img.gameObject); return; }
                img.sprite = slime;
                return;
            }

            // crab / flower：三态逐帧动画
            var anim = MonsterSprites.Get(key);
            if (!anim.Valid) { Destroy(img.gameObject); return; }
            var sa = img.gameObject.AddComponent<SpriteAnimator>();
            sa.Setup(img, anim.idle, anim.attack, anim.hit,
                anim.idleInterval, anim.attackInterval, anim.hitInterval);
            _monsterAnim = sa;
        }

        /// <summary>当前怪物的立绘 key：slime / crab / flower；无立绘（龙/蛇/BOSS）返回 null。</summary>
        string MonsterKey()
        {
            var gm = GameManager.Instance;
            if (!gm.IsShapeBoard) return "slime";
            if (gm.CurrentNodeType == NodeType.Elite)
            {
                switch (MonsterShapes.LastEliteName)
                {
                    case "巨蟹": return "crab";
                    case "尖叫花": return "flower";
                }
            }
            return null;
        }

        /// <summary>抽牌堆/弃牌堆查看：全屏变暗遮罩 + 卡牌网格居中铺满全屏（杀戮尖塔式）。</summary>
        void ShowPileViewer(List<CardData> pile, string title)
        {
            if (_pileViewer != null) Destroy(_pileViewer);

            var canvas = UIFactory.EnsureCanvas();
            var root = UIFactory.CreateRect("PileViewer", canvas.transform);
            UIFactory.Stretch(root);
            _pileViewer = root.gameObject;

            // 全屏变暗遮罩（点击空白处关闭）
            var mask = UIFactory.CreatePanel("Mask", root, new Color(0, 0, 0, 0.85f));
            UIFactory.Stretch(mask.rectTransform);
            var maskBtn = mask.gameObject.AddComponent<Button>();
            maskBtn.targetGraphic = mask;
            maskBtn.onClick.AddListener(ClosePileViewer);

            var titleText = UIFactory.CreateText("Title", root, title, 60, GameConfig.Accent);
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -120), new Vector2(0, -40));

            var hint = UIFactory.CreateText("Hint", root, "点击空白处关闭", 30, GameConfig.SubText);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 30), new Vector2(200, 80));

            if (pile.Count == 0)
            {
                var empty = UIFactory.CreateText("Empty", root, "（空）", 60, GameConfig.SubText);
                UIFactory.Place(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200, -50), new Vector2(200, 50));
                return;
            }

            // 铺满全屏的卡牌网格（居中）
            const float cardW = 200f, cardH = 260f, gap = 24f;
            int cols = Mathf.Min(5, pile.Count);
            int rows = Mathf.CeilToInt(pile.Count / (float)cols);
            float totalW = cols * cardW + (cols - 1) * gap;
            float totalH = rows * cardH + (rows - 1) * gap;
            float startX = -totalW / 2f + cardW / 2f;
            float startY = totalH / 2f - cardH / 2f - 40f;   // 稍下移，给标题留空间

            for (int i = 0; i < pile.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                float x = startX + c * (cardW + gap);
                float y = startY - r * (cardH + gap);
                CreatePileCard(pile[i], x, y, cardW, cardH, root);
            }
        }

        /// <summary>在弹窗里铺一张卡（名字 + 大数字 + 左上角费用 + 圆角描边）。</summary>
        void CreatePileCard(CardData card, float x, float y, float cardW, float cardH, RectTransform parent)
        {
            var rt = UIFactory.CreateRect("Card_" + card.DisplayText(), parent);
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x - cardW / 2f, y - cardH / 2f), new Vector2(x + cardW / 2f, y + cardH / 2f));

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.Rounded;
            img.type = Image.Type.Sliced;
            img.color = GameConfig.CardBase;
            UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f));

            // 名字（顶部）
            if (!string.IsNullOrEmpty(card.cardName))
            {
                var name = UIFactory.CreateText("Name", rt, card.cardName, 30, GameConfig.SubText);
                name.alignment = TextAlignmentOptions.Center;
                UIFactory.Place(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(10, -50), new Vector2(-10, -12));
            }

            // 大数字（中下部）
            var label = UIFactory.CreateText("Label", rt, card.DisplayText(), 56, GameConfig.CardColor(card.op));
            UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10, -148), new Vector2(-10, -52));

            // 描述（底部；字号自动缩放，长文本不被截断）
            if (!string.IsNullOrEmpty(card.description))
            {
                var desc = UIFactory.CreateText("Desc", rt, card.description, 22, GameConfig.SubText);
                desc.alignment = TextAlignmentOptions.Center;
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 11;
                desc.fontSizeMax = 22;
                UIFactory.Place(desc.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(10, 8), new Vector2(-10, 96));
            }

            // 费用（左上角）
            var cost = UIFactory.CreateText("Cost", rt, card.cost.ToString(), 36, GameConfig.Accent);
            UIFactory.Place(cost.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -46), new Vector2(60, -12));
        }

        void ClosePileViewer()
        {
            if (_pileViewer != null) { Destroy(_pileViewer); _pileViewer = null; }
        }

        void BuildOverlay(Transform parent)
        {
            var gm = GameManager.Instance;
            if (!gm.IsGameOver) return;

            var overlay = UIFactory.CreatePanel("Overlay", parent, new Color(0, 0, 0, 0.75f));
            UIFactory.Stretch(overlay.rectTransform);

            string msg = gm.Phase == TurnPhase.Won ? "胜 利" : "失 败";
            Color msgColor = gm.Phase == TurnPhase.Won ? GameConfig.Accent : GameConfig.Hex("#FF2222");
            var text = UIFactory.CreateText("Result", parent, msg, 144, msgColor);
            UIFactory.Place(text.rectTransform,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -40), new Vector2(0, 140));

            if (gm.Phase == TurnPhase.Won)
            {
                SfxPlayer.Instance?.Play(SfxType.Win);
                // 普通胜利 → 三选一拿卡；精英胜利 → 获得遗物；BOSS 胜利 → 通关结局屏
                NodeType nodeType = GameManager.Instance.CurrentNodeType;
                bool isBoss = nodeType == NodeType.Boss;
                bool isElite = nodeType == NodeType.Elite;
                var cont = UIFactory.CreateButton("Continue", parent, isBoss ? "查看结局" : "继续前进", 48,
                    GameConfig.Accent, GameConfig.Background,
                    () => {
                        if (isBoss) GameManager.Instance.ShowEnding();
                        else if (isElite) GameManager.Instance.ShowRelicReward();
                        else GameManager.Instance.ShowCardReward();
                    });
                UIFactory.Place(cont,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-230, -200), new Vector2(-20, -100));
            }
            else
            {
                SfxPlayer.Instance?.Play(SfxType.Lose);
                SaveSystem.Clear();  // 死亡清档（杀戮尖塔式：死了就没了）

                var retry = UIFactory.CreateButton("Retry", parent, "再来一局", 48,
                    GameConfig.Accent, GameConfig.Background,
                    () => GameManager.Instance.StartRun(GameManager.Instance.PlayerClass));
                UIFactory.Place(retry,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-230, -200), new Vector2(-20, -100));

                var toTitle = UIFactory.CreateButton("ToTitle", parent, "回到标题", 48,
                    GameConfig.CardBase, GameConfig.SubText,
                    () => GameManager.Instance.ShowTitle());
                UIFactory.Place(toTitle,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(20, -200), new Vector2(230, -100));
            }
        }

        /// <summary>格子悬停：有选中卡时，高亮受影响的格子。</summary>
        void OnCellHover(int row, int col, bool hovered)
        {
            if (_selectedCard == null || GameManager.Instance.IsGameOver) { ClearHighlight(); return; }
            if (!hovered) { ClearHighlight(); return; }
            ClearHighlight();
            foreach (var p in GetTargets(_selectedCard, row, col))
                AddHighlight(p.x, p.y);
        }

        List<Vector2Int> GetTargets(CardData card, int row, int col)
        {
            var result = new List<Vector2Int>();
            if (card.enchant || card.heal > 0) return result;  // 附魔/回血卡不指向格子
            var board = GameManager.Instance.Board;
            switch (card.target)
            {
                case TargetType.Row:
                    for (int c = 0; c < board.cols; c++)
                        if (board.At(row, c) != null) result.Add(new Vector2Int(row, c));
                    break;
                case TargetType.Column:
                    for (int r = 0; r < board.rows; r++)
                        if (board.At(r, col) != null) result.Add(new Vector2Int(r, col));
                    break;
                case TargetType.All:
                    for (int r = 0; r < board.rows; r++)
                        for (int c = 0; c < board.cols; c++)
                            if (board.At(r, c) != null) result.Add(new Vector2Int(r, c));
                    break;
                default: // Single
                    {
                        var d = board.At(row, col);
                        // 「不能对核心使用」的牌（镰刀横扫）不给核心高亮，避免玩家白点
                        if (d != null && !(card.noCore && d.kind == NumberKind.Core))
                            result.Add(new Vector2Int(row, col));
                    }
                    break;
            }
            return result;
        }

        void AddHighlight(int row, int col)
        {
            var cell = _cellRects[row, col];
            if (cell == null) return;
            var img = UIFactory.CreatePanel("Target", cell, new Color(1f, 0.7f, 0f, 0.12f)); // 淡金填充
            img.raycastTarget = false;
            UIFactory.Stretch(img.rectTransform);
            img.rectTransform.SetAsFirstSibling();  // 放到文字之下

            // 金色发光边框（走 Bloom 辉光）
            UIFactory.AddOutline(img, new Color(1f, 0.75f, 0.1f), new Vector2(3f, -3f));

            // 填充呼吸脉冲
            img.gameObject.AddComponent<Pulse>().Setup(img,
                new Color(1f, 0.7f, 0f, 0.08f), new Color(1f, 0.7f, 0f, 0.22f), 5f);

            _highlights.Add(img.gameObject);
        }

        void ClearHighlight()
        {
            foreach (var h in _highlights) if (h != null) Destroy(h);
            _highlights.Clear();
        }

        /// <summary>快照当前棋盘：每格 → 数字值（只记有数字的格）。</summary>
        Dictionary<Vector2Int, int> SnapshotBoard()
        {
            var d = new Dictionary<Vector2Int, int>();
            foreach (var n in GameManager.Instance.Board.numbers)
                d[new Vector2Int(n.row, n.col)] = n.value;
            return d;
        }

        /// <summary>对比快照与当前棋盘，返回数值有变化的格子及其增减量。</summary>
        Dictionary<Vector2Int, int> ComputeDeltas(Dictionary<Vector2Int, int> before)
        {
            var d = new Dictionary<Vector2Int, int>();
            foreach (var n in GameManager.Instance.Board.numbers)
            {
                var key = new Vector2Int(n.row, n.col);
                int old = before.TryGetValue(key, out var v) ? v : 0;
                int delta = n.value - old;
                if (delta != 0) d[key] = delta;
            }
            return d;
        }

        /// <summary>给数值变化的格子加「浮动 +N/-N 文字 + 数字缩放 pop」（方案 A 确定感）。</summary>
        void SpawnValueDeltaFeedback(Dictionary<Vector2Int, int> deltas)
        {
            foreach (var kv in deltas)
            {
                if (_cellRects == null || kv.Key.x < 0 || kv.Key.x >= _cellRects.GetLength(0)
                    || kv.Key.y < 0 || kv.Key.y >= _cellRects.GetLength(1)) continue;
                var cell = _cellRects[kv.Key.x, kv.Key.y];
                if (cell == null) continue;
                StartCoroutine(FloatDelta(cell, kv.Value));
                StartCoroutine(PopNumber(cell));
            }
        }

        /// <summary>浮动增减量文字：从格子中央往上浮起并渐隐（增长金、减少红）。</summary>
        IEnumerator FloatDelta(RectTransform cell, int delta)
        {
            bool up = delta > 0;
            var t = UIFactory.CreateText("Delta", cell, (up ? "+" : "") + delta, 40,
                up ? GameConfig.Accent : GameConfig.Hex("#FF2222"));
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(90f, 44f);
            rt.anchoredPosition = new Vector2(0f, 20f);

            float time = 0f;
            const float dur = 0.7f;
            while (time < dur)
            {
                time += Time.deltaTime;
                float k = time / dur;
                rt.anchoredPosition = new Vector2(0f, 20f + 44f * k);
                Color c = t.color; c.a = 1f - k; t.color = c;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        /// <summary>数字缩放 pop：1 → 1.4 → 1（只缩放数字文字，不动格子底）。</summary>
        IEnumerator PopNumber(RectTransform cell)
        {
            var txt = cell.Find("Value");
            if (txt == null) yield break;
            var rt = (RectTransform)txt;
            float t = 0f;
            const float dur = 0.25f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                rt.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>打击反馈：命中格子粒子爆发 + 闪白 + 屏幕微震。</summary>
        void SpawnHitFeedback(List<Vector2Int> hitCells)
        {
            if (hitCells.Count > 0) SfxPlayer.Instance?.Play(SfxType.Hit);
            foreach (var p in hitCells)
            {
                var cell = _cellRects[p.x, p.y];
                if (cell == null) continue;
                StartCoroutine(Burst(cell));
                StartCoroutine(Flash(cell));
            }
            StartCoroutine(Shake());
        }

        /// <summary>
        /// 玩家被咬的受击反馈（原来完全没有）：怪物攻击音效（与打怪同一套）+ 红色屏闪 + 屏幕震动
        /// + 伤害数字从攻击部位飞向血条 + 血条闪红。
        /// </summary>
        void SpawnPlayerHurtFeedback(int damage, Vector3 origin)
        {
            if (damage <= 0) return;
            SfxPlayer.Instance?.Play(SfxType.Hit);
            StartCoroutine(RedFlash());
            StartCoroutine(Shake());
            if (_hpText == null) return;
            Vector3 from = origin != Vector3.zero ? origin : _hpText.rectTransform.position;
            StartCoroutine(FlyDamage(damage, from));   // 内部会给血条闪红
        }

        /// <summary>受击屏闪：全屏铺一层暗红，快速淡出。</summary>
        IEnumerator RedFlash()
        {
            var flash = UIFactory.CreatePanel("HurtFlash", _root, new Color(0.9f, 0.1f, 0.1f, 0.3f));
            flash.raycastTarget = false;
            UIFactory.Stretch(flash.rectTransform);
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Color c = flash.color; c.a = 0.3f * (1f - t / dur); flash.color = c;
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        /// <summary>粒子爆发：金色小点从格子中心向外飞散并淡出。</summary>
        IEnumerator Burst(RectTransform cell)
        {
            const int n = 10;
            var parts = new List<Image>();
            var angles = new float[n];
            for (int i = 0; i < n; i++)
            {
                var img = UIFactory.CreatePanel("Hit", cell, new Color(1f, 0.8f, 0.2f, 0.9f));
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(5f, 5f);
                rt.anchoredPosition = Vector2.zero;
                parts.Add(img);
                angles[i] = Random.value * Mathf.PI * 2f;
            }

            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                for (int i = 0; i < n; i++)
                {
                    float dist = 30f + 45f * k;
                    parts[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angles[i]) * dist, Mathf.Sin(angles[i]) * dist);
                    Color c = parts[i].color; c.a = 1f - k; parts[i].color = c;
                }
                yield return null;
            }
            foreach (var p in parts) Destroy(p.gameObject);
        }

        /// <summary>闪白：命中格子快速白闪一下。</summary>
        IEnumerator Flash(RectTransform cell)
        {
            var flash = UIFactory.CreatePanel("Flash", cell, new Color(1f, 1f, 1f, 0.5f));
            flash.raycastTarget = false;
            UIFactory.Stretch(flash.rectTransform);
            flash.rectTransform.SetAsFirstSibling();
            float t = 0f;
            const float dur = 0.15f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Color c = flash.color; c.a = 0.5f * (1f - t / dur); flash.color = c;
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        /// <summary>屏幕微震：整个战斗界面轻微抖动一下。</summary>
        IEnumerator Shake()
        {
            float t = 0f;
            const float dur = 0.2f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float amp = 6f * (1f - t / dur);
                _root.anchoredPosition = new Vector2(Random.value * 2f - 1f, Random.value * 2f - 1f) * amp;
                yield return null;
            }
            _root.anchoredPosition = Vector2.zero;
        }

        /// <summary>溢出爆炸反馈：每个溢出格子大爆炸 + 总伤害飞向血条 + 强震。</summary>
        void SpawnOverflowFeedback(IReadOnlyList<OverflowEvent> events)
        {
            if (events == null || events.Count == 0) return;

            int total = 0;
            Vector3 origin = Vector3.zero;
            bool hasOrigin = false;
            foreach (var ev in events)
            {
                total += ev.damage;
                var cell = _cellRects[ev.row, ev.col];
                if (cell == null) continue;
                StartCoroutine(OverflowExplosion(cell, ev.isCore));
                if (!hasOrigin) { origin = cell.position; hasOrigin = true; }
            }

            StartCoroutine(StrongShake());
            if (_hpText != null && total > 0)
                StartCoroutine(FlyDamage(total, hasOrigin ? origin : _hpText.rectTransform.position));
        }

        /// <summary>溢出大爆炸：中心爆闪 + 冲击波扩散 + 一圈大小不一的粒子飞散（核心金色、普通白热）。</summary>
        IEnumerator OverflowExplosion(RectTransform cell, bool isCore)
        {
            Color sparkColor = isCore ? new Color(1f, 0.7f, 0f, 1f) : new Color(1f, 0.85f, 0.4f, 1f);

            // 中心爆闪
            var flash = UIFactory.CreatePanel("Boom", cell, new Color(1f, 1f, 1f, 0.95f));
            flash.raycastTarget = false;
            var frt = flash.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(40f, 40f);
            frt.anchoredPosition = Vector2.zero;

            // 冲击波（更大更淡的扩散圈）
            var shock = UIFactory.CreatePanel("Shock", cell, new Color(1f, 0.8f, 0.3f, 0.55f));
            shock.raycastTarget = false;
            var srt = shock.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(20f, 20f);
            srt.anchoredPosition = Vector2.zero;

            // 粒子（大小不一）
            const int n = 24;
            var parts = new List<Image>();
            var angles = new float[n];
            for (int i = 0; i < n; i++)
            {
                var p = UIFactory.CreatePanel("Spark", cell, sparkColor);
                p.raycastTarget = false;
                var rt = p.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float s = 3f + Random.value * 6f;   // 3~9px
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = Vector2.zero;
                parts.Add(p);
                angles[i] = Random.value * Mathf.PI * 2f;
            }

            float t = 0f;
            const float dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                frt.sizeDelta = new Vector2(40f + 110f * k, 40f + 110f * k);
                Color fc = flash.color; fc.a = 0.95f * (1f - k); flash.color = fc;
                srt.sizeDelta = new Vector2(20f + 150f * k, 20f + 150f * k);
                Color sc = shock.color; sc.a = 0.55f * (1f - k); shock.color = sc;
                for (int i = 0; i < n; i++)
                {
                    float dist = 12f + 85f * k;
                    parts[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angles[i]) * dist, Mathf.Sin(angles[i]) * dist);
                    Color c = parts[i].color; c.a = 1f - k; parts[i].color = c;
                }
                yield return null;
            }
            Destroy(flash.gameObject);
            Destroy(shock.gameObject);
            foreach (var p in parts) Destroy(p.gameObject);
        }

        /// <summary>强震：溢出爆炸时更剧烈的屏幕抖动。</summary>
        IEnumerator StrongShake()
        {
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float amp = 14f * (1f - t / dur);
                _root.anchoredPosition = new Vector2(Random.value * 2f - 1f, Random.value * 2f - 1f) * amp;
                yield return null;
            }
            _root.anchoredPosition = Vector2.zero;
        }

        /// <summary>伤害数字：从爆炸点飞向血条，同时血条闪红。</summary>
        IEnumerator FlyDamage(int total, Vector3 origin)
        {
            if (_hpText != null) StartCoroutine(FlashText(_hpText));

            var dmg = UIFactory.CreateText("Dmg", _root, "-" + total, 72, GameConfig.Hex("#FF2222"));
            var rt = dmg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 100f);
            rt.position = origin;
            dmg.alignment = TextAlignmentOptions.Center;

            Vector3 target = _hpText.rectTransform.position;
            float t = 0f;
            const float dur = 0.6f;
            Vector3 from = rt.position;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                rt.position = Vector3.Lerp(from, target, k);
                rt.localScale = Vector3.one * (1f + 0.3f * (1f - k));
                Color c = dmg.color; c.a = 1f - k;
                dmg.color = c;
                yield return null;
            }
            Destroy(dmg.gameObject);
        }

        /// <summary>血条闪红：受到溢出伤害时血条短暂变红再还原。</summary>
        IEnumerator FlashText(TMP_Text text)
        {
            Color baseColor = text.color;
            text.color = GameConfig.Hex("#FF2222");
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                text.color = Color.Lerp(GameConfig.Hex("#FF2222"), baseColor, t / dur);
                yield return null;
            }
            text.color = baseColor;
        }

        void OnCardClicked(CardData card)
        {
            if (GameManager.Instance.IsGameOver) return;
            _selectedCard = (_selectedCard != null && ReferenceEquals(_selectedCard, card)) ? null : card;

            // 就地更新：只改选中状态 + 提示文字，不重建整个界面（保住悬停动画）
            foreach (var v in _handCards)
            {
                if (v == null) continue;
                v.SetSelected(_selectedCard != null && ReferenceEquals(v.Card, _selectedCard));
            }
            if (_hintText != null)
                _hintText.text = _selectedCard == null
                    ? "点一张卡，再点棋盘上的数字出牌"
                    : "已选 " + _selectedCard.DisplayText() + "，点数字出牌";
        }

        void OnCellClicked(int row, int col)
        {
            var gm = GameManager.Instance;
            if (gm.IsGameOver) return;
            if (_selectedCard == null) return;

            // 找到选中的卡牌视图，先播「飞出」动画，动画结束后再重建
            CardView played = null;
            foreach (var v in _handCards)
            {
                if (v != null && ReferenceEquals(v.Card, _selectedCard)) { played = v; break; }
            }

            // 能量不足：不能出牌，播放特效
            if (_selectedCard.cost > gm.Turn.energy)
            {
                if (played != null) played.PlayCantAfford();
                return;
            }

            // 不能对核心使用的牌（镰刀横扫）：点到核心上直接提示，不消耗卡牌
            var targetData = gm.Board.At(row, col);
            if (_selectedCard.noCore && targetData != null && targetData.kind == NumberKind.Core)
            {
                if (_hintText != null) _hintText.text = (_selectedCard.cardName ?? "这张牌") + " 不能对核心使用";
                return;
            }

            var hitCells = GetTargets(_selectedCard, row, col);  // 命中格子（在 PlayCard 前记录）

            gm.PlayCard(_selectedCard, row, col);
            _selectedCard = null;

            if (played != null) played.PlayCard(0.2f, () => { Build(); SpawnHitFeedback(hitCells); if (hitCells.Count > 0) _monsterAnim?.Play(MonsterAnimState.Hit); });
            else { Build(); SpawnHitFeedback(hitCells); if (hitCells.Count > 0) _monsterAnim?.Play(MonsterAnimState.Hit); }
        }

        void OnEndTurnClicked()
        {
            var gm = GameManager.Instance;
            if (gm.IsGameOver) return;
            _selectedCard = null;
            StartCoroutine(AnimateEndTurn());
        }

        /// <summary>逐步播放回合结算：攻击 → 增殖 → 溢出 → 收尾（灼烧在新回合开始时的 StartTurn 里结算）。</summary>
        IEnumerator AnimateEndTurn()
        {
            var gm = GameManager.Instance;
            var turn = gm.Turn;
            if (turn.phase != TurnPhase.PlayerTurn) yield break;

            _skipDealAnim = true;   // 中间步骤不重抽手牌动画

            // 怪物反击：先记住攻击部位在屏幕上的位置（Build 会销毁旧格子），再结算掉血
            Vector3 attackerPos = Vector3.zero;
            if (turn.attacker != null && _cellRects != null
                && turn.attacker.row >= 0 && turn.attacker.row < _cellRects.GetLength(0)
                && turn.attacker.col >= 0 && turn.attacker.col < _cellRects.GetLength(1)
                && _cellRects[turn.attacker.row, turn.attacker.col] != null)
                attackerPos = _cellRects[turn.attacker.row, turn.attacker.col].position;

            int hpBefore = gm.Player.health;
            turn.ResolveAttack();
            int hurt = hpBefore - gm.Player.health;
            Build();
            if (hurt > 0)
            {
                _monsterAnim?.Play(MonsterAnimState.Attack);   // 怪物咬人：播攻击动画
                SpawnPlayerHurtFeedback(hurt, attackerPos);    // 受击音效 + 屏闪 + 伤害飞字
            }
            yield return new WaitForSeconds(0.2f);

            var beforeGrowth = SnapshotBoard();
            turn.ResolveGrowth();
            Build();
            SpawnValueDeltaFeedback(ComputeDeltas(beforeGrowth));   // 增殖的 +N 浮动 + 数字 pop（确定感）
            yield return new WaitForSeconds(0.4f);

            turn.ResolveOverflow();
            Build();
            SpawnOverflowFeedback(turn.LastOverflow);
            yield return new WaitForSeconds(0.55f);

            _skipDealAnim = false;  // 收尾抽牌恢复动画
            turn.FinishTurn();      // 进入新回合 → StartTurn 里结算灼烧（这一帧能看到数值下降）
            Build();
            SpawnBurnFeedback();
        }

        /// <summary>灼烧结算的反馈：屏幕轻微震动（火焰音效由 TurnManager.ResolveBurn 播放，这里不再重复播）。</summary>
        void SpawnBurnFeedback()
        {
            var turn = GameManager.Instance.Turn;
            if (turn.LastBurnTick <= 0) return;
            StartCoroutine(Shake());
        }
    }
}
