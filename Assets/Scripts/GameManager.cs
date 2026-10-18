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
        TitleMathFX _titleFx;   // 标题数学动效
        string _monsterName;    // 当前怪物名字（战斗屏显示）
        GameObject _pauseMenu;  // 暂停菜单（ESC）

        // 场景化引用（SceneBuilder 生成场景时用 BindScene 注入；为空则回退到纯运行时生成）
        [SerializeField] GameObject _worldRoot;
        [SerializeField] GameObject _titleRoot;
        [SerializeField] Button _startButton;
        [SerializeField] Button _quitButton;
        [SerializeField] Button _continueButton;
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
        public string MonsterName => _monsterName;
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
                if (_continueButton != null)
                {
                    _continueButton.onClick.AddListener(ContinueGame);
                    _continueButton.gameObject.SetActive(SaveSystem.HasSave); // 无存档则隐藏
                }
                if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
            }
            else
            {
                ShowTitle(); // 回退：未跑 SceneBuilder 时纯运行时生成
            }

            // CRT 后处理（辉光/暗角/色差/扫描线），幂等
            if (GetComponent<CRTPostFX>() == null) gameObject.AddComponent<CRTPostFX>();

            // 标题数学动效（漂浮数字+曲线+螺旋），初始就在标题
            _titleFx = gameObject.AddComponent<TitleMathFX>();
            _titleFx.SetVisible(true);

            // 音效
            if (GetComponent<SfxPlayer>() == null) gameObject.AddComponent<SfxPlayer>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                TogglePause();
        }

        /// <summary>场景化绑定：SceneBuilder 生成场景时调用，把预置 UI 引用挂到本组件。</summary>
        public void BindScene(GameObject worldRoot, GameObject titleRoot, Button startButton, Button continueButton, Button quitButton,
            GameObject classSelectRoot, GameObject routeRoot)
        {
            _worldRoot = worldRoot;
            _titleRoot = titleRoot;
            _startButton = startButton;
            _continueButton = continueButton;
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

        /// <summary>切换暂停菜单（ESC）：暂停/继续。</summary>
        void TogglePause()
        {
            if (_pauseMenu == null) BuildPauseMenu();
            bool show = !_pauseMenu.activeSelf;
            _pauseMenu.SetActive(show);
            Time.timeScale = show ? 0f : 1f;   // 暂停时冻结时间（动画也停）
        }

        /// <summary>从暂停菜单回到标题。</summary>
        void BackToTitle()
        {
            if (_pauseMenu != null) _pauseMenu.SetActive(false);
            Time.timeScale = 1f;
            ShowTitle();
        }

        /// <summary>建暂停菜单（继续 / 回到标题 / 退出游戏），懒加载。</summary>
        void BuildPauseMenu()
        {
            var canvas = UIFactory.EnsureCanvas();
            var root = UIFactory.CreateRect("PauseMenu", canvas.transform);
            UIFactory.Stretch(root);
            _pauseMenu = root.gameObject;

            var mask = UIFactory.CreatePanel("Mask", root, new Color(0, 0, 0, 0.75f));
            UIFactory.Stretch(mask.rectTransform);

            var panel = UIFactory.CreatePanel("Panel", root, GameConfig.CardBase);
            UIFactory.Place(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300, -260), new Vector2(300, 260));

            var title = UIFactory.CreateText("PauseTitle", panel.transform, "暂 停", 60, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -100), new Vector2(0, -20));

            var resume = UIFactory.CreateButton("Resume", panel.transform, "继续游戏", 48,
                GameConfig.Accent, GameConfig.Background, TogglePause);
            UIFactory.Place(resume,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, 40), new Vector2(-20, 120));

            var toTitle = UIFactory.CreateButton("ToTitle", panel.transform, "回到标题", 40,
                GameConfig.CardBase, GameConfig.SubText, BackToTitle);
            UIFactory.Place(toTitle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, -40), new Vector2(-20, 40));

            var quit = UIFactory.CreateButton("Quit", panel.transform, "退出游戏", 40,
                GameConfig.CardBase, GameConfig.SubText, QuitGame);
            UIFactory.Place(quit,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, -120), new Vector2(-20, -40));

            _pauseMenu.SetActive(false);
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
            if (_titleFx != null) _titleFx.SetVisible(true);
            EnsureWorld();
            Switch(() => {
                ClearScreen();
                if (_titleRoot != null) ShowFrame(_titleRoot);
                else gameObject.AddComponent<TitleScreen>(); // 回退：运行时生成标题
            });
        }

        public void ShowClassSelect()
        {
            if (_titleFx != null) _titleFx.SetVisible(false);
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
            if (_titleFx != null) _titleFx.SetVisible(false);
            EnsureWorld();
            SaveGame();   // 自动存档（路线检查点）
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
            SaveSystem.Clear();  // 通关，清除存档
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
            SpawnMonster(board, type);   // 所有战斗都用怪物形状（部位 + 核心）
            return board;
        }

        /// <summary>从形状目录铺「怪物形状棋盘」：普通史莱姆 / 精英四选一 / BOSS 双头龙。</summary>
        void SpawnMonster(BoardState board, NodeType type)
        {
            List<MonsterCell> shape;
            string name;
            if (type == NodeType.Battle)
            {
                shape = MonsterShapes.Slime();
                name = "史莱姆";
            }
            else if (type == NodeType.Boss)
            {
                shape = MonsterShapes.BossDragon();
                name = "BOSS·双头龙";
            }
            else
            {
                shape = MonsterShapes.EliteRandom();
                name = "精英·" + MonsterShapes.LastEliteName;
            }
            _monsterName = name;
            foreach (var c in shape)
                board.numbers.Add(new NumberData(c.value, c.row, c.col, c.kind, c.attackOrder, c.coreGroup));
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

        /// <summary>保存当前运行（职业/血量/卡组/路线）。</summary>
        public void SaveGame()
        {
            if (_cls == null || _player == null || _deck == null || _route == null) return;
            var data = new SaveData
            {
                classId = _cls.id,
                health = _player.health,
                deck = FullDeck(),
                route = _route,
            };
            SaveSystem.Save(data);
        }

        /// <summary>继续上次运行（有存档则恢复并进路线）。</summary>
        public void ContinueGame()
        {
            var data = SaveSystem.Load();
            if (data == null) return;
            _cls = FindClass(data.classId);
            _player = new PlayerState(data.health);
            _deck = new Deck(data.deck);
            _route = data.route;
            ShowRoute();
        }

        List<CardData> FullDeck()
        {
            var list = new List<CardData>();
            list.AddRange(_deck.drawPile);
            list.AddRange(_deck.discardPile);
            list.AddRange(_deck.hand);
            return list;
        }

        ClassData FindClass(string id)
        {
            foreach (var c in ClassCatalog.All())
                if (c.id == id) return c;
            return ClassCatalog.Warrior();
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
