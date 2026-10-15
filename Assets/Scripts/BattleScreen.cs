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
            BuildHand(_root);
            BuildClassInfo(_root);
            BuildOverlay(_root);
        }

        void BuildBlood(Transform parent)
        {
            var gm = GameManager.Instance;

            var label = UIFactory.CreateText("BloodLabel", parent, "血 量", 36, GameConfig.SubText);
            UIFactory.Place(label.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -50), new Vector2(0, -20));

            var value = UIFactory.CreateText("BloodValue", parent, gm.Player.health.ToString(), 96, GameConfig.Accent);
            UIFactory.Place(value.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -190), new Vector2(0, -60));

            var hint = UIFactory.CreateText("Hint", parent, _selectedCard == null
                    ? "点一张卡，再点棋盘上的数字出牌"
                    : "已选 " + _selectedCard.DisplayText() + "，点数字出牌",
                36, GameConfig.SubText);
            UIFactory.Place(hint.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -230), new Vector2(0, -200));

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
            int size = board.size;

            // 棋盘区域固定，格子大小按尺寸自动计算；无间距，格子连成一块
            float boardArea = 440f;
            float cellSize = boardArea / size;
            float half = boardArea * 0.5f;
            float upShift = 40f;

            var container = UIFactory.CreateRect("Board", parent);
            UIFactory.Place(container,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-half, -half + upShift), new Vector2(half, half + upShift));

            // 棋盘底：一整块深色正方形（先画一个正方形）
            var boardBg = container.gameObject.AddComponent<Image>();
            boardBg.color = GameConfig.CardBase;

            // 网格线：外框 + 内部线，把大正方形切成 size×size 格（老式粗边框）
            const float lineW = 6f;
            for (int i = 0; i <= size; i++)
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

            // 格子：透明按钮层，叠在网格线上方，只负责点击 + 显示数字
            var cells = UIFactory.CreateRect("Cells", container);
            UIFactory.Stretch(cells);
            var grid = cells.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = size;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = Vector2.zero;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int row = 0; row < size; row++)
                for (int col = 0; col < size; col++)
                    CreateCell(cells, row, col, cellSize);
        }

        void CreateCell(Transform parent, int row, int col, float cellSize)
        {
            var gm = GameManager.Instance;
            var data = gm.Board.At(row, col);

            var go = new GameObject("Cell_" + row + "_" + col, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            bool isAttacker = gm.Turn.attacker != null && gm.Turn.attacker.row == row && gm.Turn.attacker.col == col;
            if (isAttacker)
                img.color = new Color(1f, 0.35f, 0.23f, 0.55f);   // 本回合要咬你的部位：橙红半透明
            else if (data != null && data.kind == NumberKind.Core)
                img.color = new Color(1f, 0.7f, 0f, 0.45f);        // 核心：金标记（蓄力爆炸中）
            else
                img.color = new Color(0, 0, 0, 0);                 // 透明底，露出网格线

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            int r = row, c = col;
            btn.onClick.AddListener(() => OnCellClicked(r, c));

            if (data != null)
            {
                var txt = UIFactory.CreateText("Value", rt, data.value.ToString(), cellSize * 0.5f, GameConfig.NumberColor(data.value));
                UIFactory.Stretch(txt.rectTransform);
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
            const float cardW = 140f;
            const float cardH = 210f;
            const float gap = 20f;
            float totalW = n * cardW + (n - 1) * gap;
            float startX = -totalW / 2f;

            for (int i = 0; i < n; i++)
            {
                var card = hand[i];
                bool selected = _selectedCard != null && ReferenceEquals(_selectedCard, card);
                var bg = selected ? GameConfig.Accent : GameConfig.CardBase;
                var fg = selected ? GameConfig.Background : GameConfig.CardColor(card.op);

                float centerX = startX + cardW / 2f + i * (cardW + gap);
                var cardRt = UIFactory.CreateRect("Card_" + card.DisplayText(), parent);
                UIFactory.Place(cardRt,
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(centerX - cardW / 2f, 30), new Vector2(centerX + cardW / 2f, 30 + cardH));

                var img = cardRt.gameObject.AddComponent<Image>();
                img.sprite = UIFactory.Rounded;
                img.type = Image.Type.Sliced;
                img.color = bg;
                UIFactory.AddOutline(img, GameConfig.SubText, new Vector2(2f, -2f)); // 描边

                var btn = cardRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;

                var label = UIFactory.CreateText("Label", cardRt, card.DisplayText(), 64, fg);
                UIFactory.Stretch(label.rectTransform);

                // 费用（卡牌左上角小字）
                var cost = UIFactory.CreateText("Cost", cardRt, card.cost.ToString(), 40, GameConfig.Accent);
                UIFactory.Place(cost.rectTransform,
                    new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(10, -52), new Vector2(60, -10));

                var c = card;
                btn.onClick.AddListener(() => OnCardClicked(c));
            }

            // 结束回合按钮
            var endBtn = UIFactory.CreateButton("EndTurn", parent, "结束回合", 48,
                GameConfig.GridLine, GameConfig.SubText,
                () => OnEndTurnClicked());
            UIFactory.Place(endBtn,
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-260, 110), new Vector2(-40, 210));
        }

        void BuildClassInfo(Transform parent)
        {
            var cls = GameManager.Instance.Turn.playerClass;
            var text = UIFactory.CreateText("Class", parent, cls.displayName + " · " + cls.description, 32, GameConfig.SubText);
            text.alignment = TextAlignmentOptions.Left;
            UIFactory.Place(text.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(60, 150), new Vector2(560, 190));
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

        void OnCardClicked(CardData card)
        {
            if (GameManager.Instance.IsGameOver) return;
            _selectedCard = (_selectedCard != null && ReferenceEquals(_selectedCard, card)) ? null : card;
            Build();
        }

        void OnCellClicked(int row, int col)
        {
            var gm = GameManager.Instance;
            if (gm.IsGameOver) return;
            if (_selectedCard == null) return;

            gm.PlayCard(_selectedCard, row, col);
            _selectedCard = null;
            Build();
        }

        void OnEndTurnClicked()
        {
            var gm = GameManager.Instance;
            if (gm.IsGameOver) return;
            gm.EndTurn();
            _selectedCard = null;
            Build();
        }
    }
}
