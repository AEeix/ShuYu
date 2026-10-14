using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 顶层管理器：持有整局的持久状态（职业/血量/卡组/路线），驱动 标题 ↔ 选职 ↔ 路线 ↔ 战斗 切换。
    /// 坐标轴是持久「世界」底图：标题/选职/选路共用，路线直接画在轴上；进战斗被战斗自己的背景盖掉。
    /// 切屏走淡入淡出过渡。
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
        Image _fade;         // 全屏黑色过渡遮罩
        bool _transitioning; // 过渡中标志（防止连点重叠）

        // 场景化引用（SceneBuilder 生成场景时用 BindScene 注入；为空则回退到纯运行时生成）
        [SerializeField] GameObject _worldRoot;
        [SerializeField] GameObject _titleRoot;
        [SerializeField] Button _startButton;
        [SerializeField] Button _quitButton;
        [SerializeField] GameObject _classSelectRoot;
        [SerializeField] GameObject _routeRoot;

        public TurnManager Turn => _turn;
        public BoardState Board => _turn.board;
        public PlayerState Player => _player;
        public List<CardData> HandCards => _turn.deck.hand;
        public RouteMap Route => _route;
        public ClassData PlayerClass => _cls;
        public TurnPhase Phase => _turn.phase;
        public bool IsGameOver => _turn.phase != TurnPhase.PlayerTurn;
        public NodeType CurrentNodeType => _route.nodes[_route.currentIndex].type;
        public GameObject ClassSelectRoot => _classSelectRoot;
        public GameObject RouteRoot => _routeRoot;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_worldRoot != null) _world = _worldRoot;

            // 确保 Canvas 走相机模式（CRT 后处理需要），旧场景的 Overlay Canvas 也会在此升级
            UIFactory.EnsureCanvas();

            if (_titleRoot != null)
            {
                // 场景化：标题已在场景里，只接线按钮，不再运行时生成
                if (_startButton != null) _startButton.onClick.AddListener(ShowClassSelect);
                if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
            }
            else
            {
                ShowTitle(); // 回退：未跑 SceneBuilder 时纯运行时生成
            }

            // CRT 后处理（辉光/暗角/色差/扫描线），幂等
            if (GetComponent<CRTPostFX>() == null) gameObject.AddComponent<CRTPostFX>();
        }

        /// <summary>场景化绑定：SceneBuilder 生成场景时调用，把预置 UI 引用挂到本组件。</summary>
        public void BindScene(GameObject worldRoot, GameObject titleRoot, Button startButton, Button quitButton,
            GameObject classSelectRoot, GameObject routeRoot)
        {
            _worldRoot = worldRoot;
            _titleRoot = titleRoot;
            _startButton = startButton;
            _quitButton = quitButton;
            _classSelectRoot = classSelectRoot;
            _routeRoot = routeRoot;
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

        /// <summary>创建全屏黑色过渡遮罩（惰性，首次切屏时建）。</summary>
        void EnsureFade()
        {
            if (_fade != null) return;
            var canvas = UIFactory.EnsureCanvas();
            var rt = UIFactory.CreateRect("TransitionFade", canvas.transform);
            UIFactory.Stretch(rt);
            _fade = rt.gameObject.AddComponent<Image>();
            _fade.color = new Color(0, 0, 0, 0);
            _fade.raycastTarget = false;
        }

        /// <summary>带淡入淡出过渡的切屏。build 里执行「销毁旧屏 + 建新屏」。</summary>
        void Switch(Action build)
        {
            if (_transitioning) return;   // 过渡中，忽略连点
            StartCoroutine(Transition(build));
        }

        IEnumerator Transition(Action build)
        {
            _transitioning = true;
            EnsureFade();
            _fade.transform.SetAsLastSibling();

            const float dur = 0.2f;
            // 淡入（画面压黑）
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                _fade.color = new Color(0, 0, 0, Smooth(t / dur));
                yield return null;
            }
            _fade.color = new Color(0, 0, 0, 1);

            // 换屏
            build();

            // 淡出（画面回来）
            _fade.transform.SetAsLastSibling();
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                _fade.color = new Color(0, 0, 0, 1 - Smooth(t / dur));
                yield return null;
            }
            _fade.color = new Color(0, 0, 0, 0);
            _transitioning = false;
        }

        /// <summary>smoothstep 缓动（让淡入淡出更顺）。</summary>
        static float Smooth(float x) { return x * x * (3 - 2 * x); }

        public void ShowTitle()
        {
            EnsureWorld();
            Switch(() => {
                ClearScreen();
                if (_titleRoot != null) ShowFrame(_titleRoot);
                else gameObject.AddComponent<TitleScreen>(); // 回退：运行时生成标题
            });
        }

        public void ShowClassSelect()
        {
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(_classSelectRoot); gameObject.AddComponent<ClassSelectScreen>(); });
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
            Switch(() => { ClearScreen(); ShowFrame(_routeRoot); gameObject.AddComponent<RouteScreen>(); });
        }

        /// <summary>战斗胜利战利品：三选一拿卡（BOSS 通关走结局，另议）。</summary>
        public void ShowCardReward()
        {
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<CardRewardScreen>(); });
        }

        /// <summary>选中战利品卡：加入卡组，回路线。</summary>
        public void TakeRewardCard(CardData card)
        {
            _deck.AddCard(card);
            ShowRoute();
        }

        /// <summary>BOSS 通关：进结局屏。</summary>
        public void ShowEnding()
        {
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<EndingScreen>(); });
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
            if (node.type == NodeType.Treasure)
            {
                _player.health = Mathf.Min(_player.health + 3, GameConfig.InitialHealth); // 宝箱回 3（占位：后续换给卡/遗物）
                ShowRoute();
                return;
            }
            if (node.type == NodeType.Mystery)
            {
                _player.health = Mathf.Min(_player.health + 2, GameConfig.InitialHealth); // 问号回 2（占位：后续换随机事件）
                ShowRoute();
                return;
            }

            StartBattle(node);
        }

        void StartBattle(MapNode node)
        {
            var board = SpawnBoard(node.type);
            _deck.Reset();   // 每场战斗前重置卡组
            _turn = new TurnManager(_cls, board, _player, _deck, GameConfig.EnergyPerTurn);

            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<BattleScreen>(); });
        }

        BoardState SpawnBoard(NodeType type)
        {
            var board = new BoardState(GameConfig.BoardSize);
            if (type == NodeType.Battle)
                SpawnMobs(board);            // 普通小怪：抽象格子，无核心，胜=清空
            else
                SpawnMonster(board, type);   // 精英/BOSS：部位 + 核心，胜=打爆核心
            return board;
        }

        /// <summary>普通小怪：固定位置铺普通数字（无核心，胜=清空）。</summary>
        void SpawnMobs(BoardState board)
        {
            int[,] spots = { { 1, 1 }, { 1, 2 }, { 2, 1 }, { 2, 2 } };
            for (int i = 0; i < 4; i++)
                board.numbers.Add(new NumberData(2 + i, spots[i, 0], spots[i, 1]));
        }

        /// <summary>精英/BOSS：占位「龙形」部位 + 胸口核心。部位按 attackOrder 固定顺序咬人，核心蓄力爆炸、打爆即胜。</summary>
        void SpawnMonster(BoardState board, NodeType type)
        {
            bool boss = type == NodeType.Boss;
            board.numbers.Add(new NumberData(boss ? 5 : 4, 0, 1, NumberKind.Part, 0)); // 头
            board.numbers.Add(new NumberData(boss ? 4 : 3, 1, 0, NumberKind.Part, 1)); // 左爪
            board.numbers.Add(new NumberData(boss ? 4 : 3, 1, 2, NumberKind.Part, 2)); // 右爪
            if (boss)
                board.numbers.Add(new NumberData(5, 2, 2, NumberKind.Part, 3)); // 尾（BOSS 多一部位）
            board.numbers.Add(new NumberData(boss ? 7 : 6, 2, 1, NumberKind.Core)); // 核心（胸口）
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
            var w = GetComponent<CardRewardScreen>(); if (w != null) Destroy(w);
            var e = GetComponent<EndingScreen>(); if (e != null) Destroy(e);
        }

        /// <summary>只显示指定场景框架（其余隐藏）。frame 为 null 则全部隐藏。</summary>
        void ShowFrame(GameObject frame)
        {
            if (_titleRoot != null) _titleRoot.SetActive(frame == _titleRoot);
            if (_classSelectRoot != null) _classSelectRoot.SetActive(frame == _classSelectRoot);
            if (_routeRoot != null) _routeRoot.SetActive(frame == _routeRoot);
        }
    }
}
