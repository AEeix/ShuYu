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

        void Start() { Build(); }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
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
            BuildSlimeView(_root);
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

            // 精英/BOSS 用「怪物形状」；普通小怪（史莱姆）用「史莱姆形状」（切掉四角）
            // 注意：不能按「有没有部位/核心」判断——史莱姆也有核心/瘤块，但它是普通怪，必须保留立绘 + 身体棋盘
            bool isMonster = GameManager.Instance.CurrentNodeType != NodeType.Battle;
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

            if (data != null && data.kind == NumberKind.Part)
            {
                // 部位：暗块填充（棋盘里的一块暗块）；攻击部位更亮橙红
                img.color = isAttacker ? new Color(1f, 0.35f, 0.23f, 0.9f) : GameConfig.MonsterFill;
            }
            else if (data != null && data.kind == NumberKind.Core)
            {
                // 核心：金色填充 + 金色描边 + 脉冲发光（蓄力爆炸中）
                img.color = new Color(1f, 0.7f, 0f, 0.45f);
                UIFactory.AddOutline(img, GameConfig.Accent, new Vector2(3f, -3f));
                img.gameObject.AddComponent<Pulse>().Setup(img,
                    new Color(1f, 0.7f, 0f, 0.25f), new Color(1f, 0.7f, 0f, 0.6f));
            }
            else
                img.color = new Color(0, 0, 0, 0);                 // 透明底，露出网格线

            // 状态变色：灼烧橙红、冰冻冰蓝（半透明覆盖层，盖在格子底色上、文字之下）
            if (data != null && data.burn)
            {
                var ov = UIFactory.CreatePanel("Burn", rt, new Color(1f, 0.35f, 0.1f, 0.4f));
                ov.raycastTarget = false;
                UIFactory.Stretch(ov.rectTransform);
            }
            else if (data != null && data.frozen)
            {
                var ov = UIFactory.CreatePanel("Frozen", rt, new Color(0.3f, 0.6f, 1f, 0.4f));
                ov.raycastTarget = false;
                UIFactory.Stretch(ov.rectTransform);
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
                view.PlayDeal(i * 0.05f);
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
        }

        void BuildSlimeView(Transform parent)
        {
            // 普通小怪战：左侧放史莱姆立绘当「怪物视图」（数字瘤块仍在下方棋盘上）；
            // 精英/BOSS 用形状棋盘自带的怪物轮廓，不放立绘
            var gm = GameManager.Instance;
            bool isMonster = gm.CurrentNodeType != NodeType.Battle;
            if (isMonster) return;

            var slime = UIFactory.Slime;
            if (slime == null) return;

            var img = UIFactory.CreatePanel("SlimeView", parent, Color.white);
            img.sprite = slime;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            UIFactory.Place(img.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(40, -200), new Vector2(440, 200));
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
                // 普通/精英胜利 → 三选一拿卡；BOSS 胜利 → 通关结局屏
                bool isBoss = GameManager.Instance.CurrentNodeType == NodeType.Boss;
                var cont = UIFactory.CreateButton("Continue", parent, isBoss ? "查看结局" : "继续前进", 48,
                    GameConfig.Accent, GameConfig.Background,
                    () => { if (isBoss) GameManager.Instance.ShowEnding(); else GameManager.Instance.ShowCardReward(); });
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
                    if (board.At(row, col) != null) result.Add(new Vector2Int(row, col));
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

        /// <summary>溢出大爆炸：中心爆闪 + 一圈白/金粒子飞散（核心金色、普通白热）。</summary>
        IEnumerator OverflowExplosion(RectTransform cell, bool isCore)
        {
            Color sparkColor = isCore ? new Color(1f, 0.7f, 0f, 1f) : new Color(1f, 0.85f, 0.4f, 1f);

            var flash = UIFactory.CreatePanel("Boom", cell, new Color(1f, 1f, 1f, 0.95f));
            flash.raycastTarget = false;
            var frt = flash.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(50f, 50f);
            frt.anchoredPosition = Vector2.zero;

            const int n = 18;
            var parts = new List<Image>();
            var angles = new float[n];
            for (int i = 0; i < n; i++)
            {
                var p = UIFactory.CreatePanel("Spark", cell, sparkColor);
                p.raycastTarget = false;
                var rt = p.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(7f, 7f);
                rt.anchoredPosition = Vector2.zero;
                parts.Add(p);
                angles[i] = Random.value * Mathf.PI * 2f;
            }

            float t = 0f;
            const float dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                frt.sizeDelta = new Vector2(50f + 100f * k, 50f + 100f * k);
                Color fc = flash.color; fc.a = 0.95f * (1f - k); flash.color = fc;
                for (int i = 0; i < n; i++)
                {
                    float dist = 18f + 70f * k;
                    parts[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angles[i]) * dist, Mathf.Sin(angles[i]) * dist);
                    Color c = parts[i].color; c.a = 1f - k; parts[i].color = c;
                }
                yield return null;
            }
            Destroy(flash.gameObject);
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

            var hitCells = GetTargets(_selectedCard, row, col);  // 命中格子（在 PlayCard 前记录）

            gm.PlayCard(_selectedCard, row, col);
            _selectedCard = null;

            if (played != null) played.PlayCard(0.2f, () => { Build(); SpawnHitFeedback(hitCells); });
            else { Build(); SpawnHitFeedback(hitCells); }
        }

        void OnEndTurnClicked()
        {
            var gm = GameManager.Instance;
            if (gm.IsGameOver) return;
            gm.EndTurn();
            _selectedCard = null;
            Build();
            SpawnOverflowFeedback(gm.Turn.LastOverflow);   // 溢出爆炸 + 伤害飞向血条
        }
    }
}
