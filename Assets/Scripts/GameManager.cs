using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 顶层管理器：持有整局的持久状态（职业/血量/卡组/路线），驱动 标题 ↔ 选职 ↔ 路线 ↔ 战斗 切换。
    /// 坐标轴是持久「世界」底图：标题/选职/选路共用，路线直接画在轴上；进战斗被战斗自己的背景盖掉。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // 一局的持久状态（跨战斗）
        ClassData _cls;
        PlayerState _player;
        Deck _deck;
        RouteMap _route;

        TurnManager _turn;   // 当前这场战斗
        GameObject _world;   // 持久世界：背景 + 坐标轴

        public TurnManager Turn => _turn;
        public BoardState Board => _turn.board;
        public PlayerState Player => _player;
        public List<CardData> HandCards => _turn.deck.hand;
        public RouteMap Route => _route;
        public ClassData PlayerClass => _cls;
        public TurnPhase Phase => _turn.phase;
        public bool IsGameOver => _turn.phase != TurnPhase.PlayerTurn;

        // 启动入口：无需任何场景，直接 Play 即可。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            ShowTitle();
        }

        /// <summary>创建持久世界：深色背景 + 坐标轴（一直保留到进战斗）。</summary>
        void EnsureWorld()
        {
            if (_world != null) return;
            var canvas = UIFactory.EnsureCanvas();
            _world = new GameObject("World", typeof(RectTransform));
            var rt = _world.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);

            var bg = UIFactory.CreatePanel("WorldBG", _world.transform, GameConfig.Background);
            UIFactory.Stretch(bg.rectTransform);

            var axisTex = UIFactory.CreateAxisTexture(1920, 1080, new Vector2(150f, 150f), 100f, 100f, 14f, GameConfig.SubText);
            var axis = UIFactory.CreatePanel("WorldAxis", _world.transform, Color.white);
            axis.sprite = Sprite.Create(axisTex, new Rect(0, 0, 1920, 1080), new Vector2(0.5f, 0.5f), 100f);
            axis.type = Image.Type.Simple;
            UIFactory.Stretch(axis.rectTransform);
        }

        public void ShowTitle()
        {
            EnsureWorld();
            ClearScreen();
            gameObject.AddComponent<TitleScreen>();
        }

        public void ShowClassSelect()
        {
            EnsureWorld();
            ClearScreen();
            gameObject.AddComponent<ClassSelectScreen>();
        }

        /// <summary>按选定职业开一局：建持久状态 + 路线图，进路线界面。</summary>
        public void StartRun(ClassData cls)
        {
            _cls = cls;
            _player = new PlayerState(GameConfig.InitialHealth);
            _deck = new Deck(cls.startingDeck);
            _route = RouteMap.Generate();

            ShowRoute();
        }

        public void ShowRoute()
        {
            EnsureWorld();
            ClearScreen();
            gameObject.AddComponent<RouteScreen>();
        }

        /// <summary>进入路线上的一个节点：战斗/精英/BOSS 开战，篝火回血。</summary>
        public void EnterNode(int nodeIndex)
        {
            var node = _route.nodes[nodeIndex];
            node.reached = true;
            _route.currentIndex = nodeIndex;

            if (node.type == NodeType.Camp)
            {
                _player.health = Mathf.Min(_player.health + 5, GameConfig.InitialHealth); // 篝火回 5（不超上限）
                ShowRoute();
                return;
            }

            StartBattle(node);
        }

        void StartBattle(MapNode node)
        {
            var board = SpawnBoard(node.type);
            _deck.Reset();   // 每场战斗前重置卡组
            _turn = new TurnManager(_cls, board, _player, _deck, GameConfig.PlaysPerTurn);

            ClearScreen();
            gameObject.AddComponent<BattleScreen>();
        }

        BoardState SpawnBoard(NodeType type)
        {
            var board = new BoardState(GameConfig.BoardSize);
            int count = type == NodeType.Boss ? 6 : (type == NodeType.Elite ? 5 : 4);
            // 占位：固定位置铺数字（值 2~7），精英/BOSS 更多；怪物形状棋盘留 M4
            int[,] spots = { { 1, 1 }, { 1, 2 }, { 2, 1 }, { 2, 2 }, { 0, 0 }, { 3, 3 } };
            for (int i = 0; i < count; i++)
                board.numbers.Add(new NumberData(2 + i, spots[i, 0], spots[i, 1]));
            return board;
        }

        /// <summary>UI 调用：出牌。</summary>
        public bool PlayCard(CardData card, int row, int col)
        {
            return _turn.TryPlayCard(card, row, col);
        }

        /// <summary>UI 调用：结束回合。</summary>
        public void EndTurn()
        {
            _turn.EndTurn();
        }

        void ClearScreen()
        {
            var t = GetComponent<TitleScreen>(); if (t != null) Destroy(t);
            var c = GetComponent<ClassSelectScreen>(); if (c != null) Destroy(c);
            var r = GetComponent<RouteScreen>(); if (r != null) Destroy(r);
            var b = GetComponent<BattleScreen>(); if (b != null) Destroy(b);
        }
    }
}
