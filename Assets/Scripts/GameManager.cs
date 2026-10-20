using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        GameObject _settingsMenu;   // 设置菜单
        TMP_Text _masterLabel;      // 主音量标签
        TMP_Text _musicLabel;       // 音乐音量标签
        TMP_Text _sfxLabel;         // 音效音量标签
        TMP_Text _fsLabel;          // 全屏标签

        // 场景化引用（SceneBuilder 生成场景时用 BindScene 注入；为空则回退到纯运行时生成）
        [SerializeField] GameObject _worldRoot;
        [SerializeField] GameObject _titleRoot;
        [SerializeField] Button _startButton;      // 主按钮（开始游戏 / 继续游戏）
        [SerializeField] Button _newGameButton;    // 开始新游戏（有存档才显示）
        [SerializeField] Button _settingsButton;   // 设置
        [SerializeField] Button _codexButton;      // 卡牌图鉴
        [SerializeField] Button _quitButton;
        [SerializeField] GameObject _classSelectRoot;
        [SerializeField] GameObject _routeRoot;

        public TurnManager Turn => _turn;
        public BoardState Board => _turn.board;
        public PlayerState Player => _player;
        public List<CardData> HandCards => _turn.deck.hand;
        /// <summary>整副牌（抽牌堆 + 弃牌堆 + 手牌的并集），供商店删牌 / 火堆强化选牌用。</summary>
        public List<CardData> DeckCards => FullDeck();
        public RouteMap Route => _route;
        public ClassData PlayerClass => _cls;
        public TurnPhase Phase => _turn.phase;
        public bool IsGameOver => _turn.phase != TurnPhase.PlayerTurn;
        public NodeType CurrentNodeType => _route.nodes[_route.currentIndex].type;
        public string MonsterName => _monsterName;
        public bool IsShapeBoard { get; private set; }   // 当前战斗是否是精英/BOSS 形状棋盘
        public GameObject ClassSelectRoot => _classSelectRoot;
        public GameObject RouteRoot => _routeRoot;
        public RectTransform WorldAxis { get; private set; }   // 坐标轴（路线界面缩放时同步）

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_worldRoot != null) _world = _worldRoot;
            if (_world != null && WorldAxis == null)
                WorldAxis = _world.transform.Find("WorldAxis")?.GetComponent<RectTransform>();

            // 确保 Canvas 走相机模式（CRT 后处理需要），旧场景的 Overlay Canvas 也会在此升级
            UIFactory.EnsureCanvas();

            // 补中文 fallback 链（方舟像素缺「然」等字，不补会显示空白）
            UIFactory.EnsureChineseFallbacks();

            if (_titleRoot != null)
            {
                // 场景化：标题已在场景里，只接线按钮（接线集中到 RefreshTitle，回到标题时会重算）
                RefreshTitle();
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

            // 音效 + 背景音乐
            if (GetComponent<SfxPlayer>() == null) gameObject.AddComponent<SfxPlayer>();
            if (GetComponent<MusicPlayer>() == null) gameObject.AddComponent<MusicPlayer>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                TogglePause();

            // 战斗时按 M 或 Tab 查看地图
            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Tab))
                ToggleMapOverlay();
        }

        /// <summary>战斗时切换地图查看（只读）。</summary>
        void ToggleMapOverlay()
        {
            if (GetComponent<BattleScreen>() == null) return;   // 只在战斗中可用
            var overlay = GetComponent<MapOverlay>();
            if (overlay != null) Destroy(overlay);
            else gameObject.AddComponent<MapOverlay>();
        }

        /// <summary>
        /// 刷新标题界面中与「存档」有关的状态：主按钮的文字与接线、开始新游戏按钮的显隐。
        /// 必须每次回到标题都重算——存档会在游戏过程中被创建（进路线时自动存）或被清除（死亡 / 通关），
        /// 如果只在 Start 里算一次，就会出现「玩完一局回到标题，继续游戏没出现」或「点了继续没反应」。
        /// </summary>
        void RefreshTitle()
        {
            if (_titleRoot == null) return;
            bool hasSave = SaveSystem.HasSave;

            if (_startButton != null)
            {
                var label = _startButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = hasSave ? "继续游戏" : "开始游戏";

                // 只摘掉本类挂的这两个监听，不能 RemoveAllListeners —— 那会连 ButtonClickSound 的点击音一起删掉
                _startButton.onClick.RemoveListener(ContinueGame);
                _startButton.onClick.RemoveListener(ShowClassSelect);
                if (hasSave) _startButton.onClick.AddListener(ContinueGame);
                else _startButton.onClick.AddListener(ShowClassSelect);
                _startButton.gameObject.SetActive(true);     // 主按钮永远可见
            }
            if (_newGameButton != null)
            {
                _newGameButton.onClick.RemoveListener(ShowClassSelect);
                _newGameButton.onClick.AddListener(ShowClassSelect);
                _newGameButton.gameObject.SetActive(hasSave);   // 有存档才显示「开始新游戏」
            }
            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(ShowSettings);
                _settingsButton.onClick.AddListener(ShowSettings);
            }
            if (_codexButton != null)
            {
                _codexButton.onClick.RemoveListener(ShowCodex);
                _codexButton.onClick.AddListener(ShowCodex);
            }
            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveListener(QuitGame);
                _quitButton.onClick.AddListener(QuitGame);
            }
            LayoutTitleButtons(hasSave);   // 无存档时「开始新游戏」隐藏，下方按钮上移补位
        }

        /// <summary>
        /// 按存档状态重排标题按钮：无存档时「开始新游戏」隐藏，其下方的设置/图鉴/退出整体上移 80（NewGame 槽位 70 + 间距 10）补位。
        /// 场景烘焙时按「有存档」的满排位置生成，这里在运行时按实际存档状态收拢空档。
        /// </summary>
        void LayoutTitleButtons(bool hasSave)
        {
            float shift = hasSave ? 0f : 80f;
            RepositionY(_settingsButton, 380 + shift, 450 + shift);
            RepositionY(_codexButton, 300 + shift, 370 + shift);
            RepositionY(_quitButton, 220 + shift, 290 + shift);
        }

        void RepositionY(Button btn, float bottom, float top)
        {
            if (btn == null) return;
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
            rt.offsetMax = new Vector2(rt.offsetMax.x, top);
        }

        /// <summary>打开卡牌图鉴。</summary>
        public void ShowCodex()
        {
            if (_titleFx != null) _titleFx.SetVisible(false);
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<CodexScreen>(); });
        }

        /// <summary>打开关键词图鉴（从卡牌图鉴右上角进入；内容来自 KeywordCatalog）。</summary>
        public void ShowKeywords()
        {
            if (_titleFx != null) _titleFx.SetVisible(false);
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<KeywordCodexScreen>(); });
        }

        /// <summary>场景化绑定：SceneBuilder 生成场景时调用，把预置 UI 引用挂到本组件。</summary>
        public void BindScene(GameObject worldRoot, GameObject titleRoot, Button startButton, Button newGameButton, Button settingsButton, Button codexButton, Button quitButton,
            GameObject classSelectRoot, GameObject routeRoot)
        {
            _worldRoot = worldRoot;
            _titleRoot = titleRoot;
            _startButton = startButton;
            _newGameButton = newGameButton;
            _settingsButton = settingsButton;
            _codexButton = codexButton;
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

        /// <summary>建暂停菜单（继续 / 设置 / 回到标题 / 退出游戏），懒加载。</summary>
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
                new Vector2(-300, -300), new Vector2(300, 300));

            var title = UIFactory.CreateText("PauseTitle", panel.transform, "暂 停", 60, GameConfig.Accent);
            UIFactory.Place(title.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -110), new Vector2(0, -20));

            var resume = UIFactory.CreateButton("Resume", panel.transform, "继续游戏", 48,
                GameConfig.Accent, GameConfig.Background, TogglePause);
            UIFactory.Place(resume,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, 110), new Vector2(180, 190));

            var settings = UIFactory.CreateButton("Settings", panel.transform, "设 置", 40,
                GameConfig.CardBase, GameConfig.SubText, OpenSettingsFromPause);
            UIFactory.Place(settings,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, 10), new Vector2(180, 90));

            var toTitle = UIFactory.CreateButton("ToTitle", panel.transform, "回到标题", 40,
                GameConfig.CardBase, GameConfig.SubText, BackToTitle);
            UIFactory.Place(toTitle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, -90), new Vector2(180, -10));

            var quit = UIFactory.CreateButton("Quit", panel.transform, "退出游戏", 40,
                GameConfig.CardBase, GameConfig.SubText, QuitGame);
            UIFactory.Place(quit,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-180, -190), new Vector2(180, -110));

            _pauseMenu.SetActive(false);
        }

        /// <summary>打开设置面板（音量 / 全屏）。</summary>
        public void ShowSettings()
        {
            if (_settingsMenu != null) return;
            BuildSettingsMenu();
        }

        void BuildSettingsMenu()
        {
            var canvas = UIFactory.EnsureCanvas();
            var root = UIFactory.CreateRect("SettingsMenu", canvas.transform);
            UIFactory.Stretch(root);
            _settingsMenu = root.gameObject;

            var mask = UIFactory.CreatePanel("Mask", root, new Color(0, 0, 0, 0.7f));
            UIFactory.Stretch(mask.rectTransform);

            var panel = UIFactory.CreatePanel("Panel", root, GameConfig.CardBase);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-340, -400), new Vector2(340, 400));

            var title = UIFactory.CreateText("Title", panel.transform, "设 置", 60, GameConfig.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, -20));

            // 三档音量：主 / 音乐 / 音效
            BuildVolumeRow(panel.transform, "Master", "主音量", -120, ref _masterLabel, () => AudioSettings.Master, v => AudioSettings.Master = v);
            BuildVolumeRow(panel.transform, "Music", "音乐音量", -210, ref _musicLabel, () => AudioSettings.Music, v => AudioSettings.Music = v);
            BuildVolumeRow(panel.transform, "Sfx", "音效音量", -300, ref _sfxLabel, () => AudioSettings.Sfx, v => AudioSettings.Sfx = v);

            _fsLabel = UIFactory.CreateText("FsLabel", panel.transform, FsText(), 36, GameConfig.SubText);
            UIFactory.Place(_fsLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 150), new Vector2(200, 200));

            var fsBtn = UIFactory.CreateButton("Fullscreen", panel.transform, "切换全屏", 32, GameConfig.GridLine, GameConfig.SubText, ToggleFullscreen);
            UIFactory.Place(fsBtn, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 90), new Vector2(120, 140));

            var back = UIFactory.CreateButton("Back", panel.transform, "返 回", 40, GameConfig.Accent, GameConfig.Background, HideSettings);
            UIFactory.Place(back, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 20), new Vector2(120, 80));

            RefreshVolumeLabels();
        }

        /// <summary>建一行音量设置：标签 + 「-」「+」按钮。topY 为行顶相对面板顶部的偏移（负值往下）。</summary>
        void BuildVolumeRow(Transform parent, string key, string name, float topY, ref TMP_Text labelRef,
            System.Func<float> get, System.Action<float> set)
        {
            labelRef = UIFactory.CreateText(key + "Label", parent, "", 36, GameConfig.SubText);
            UIFactory.Place(labelRef.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, topY - 70), new Vector2(0, topY));

            var down = UIFactory.CreateButton(key + "Down", parent, "-", 34, GameConfig.GridLine, GameConfig.SubText,
                () => { set(get() - 0.1f); RefreshVolumeLabels(); });
            UIFactory.Place(down, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, topY - 70), new Vector2(140, topY));

            var up = UIFactory.CreateButton(key + "Up", parent, "+", 34, GameConfig.GridLine, GameConfig.SubText,
                () => { set(get() + 0.1f); RefreshVolumeLabels(); });
            UIFactory.Place(up, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, topY - 70), new Vector2(-40, topY));
        }

        void RefreshVolumeLabels()
        {
            if (_masterLabel != null) _masterLabel.text = "主音量 " + VolStep(AudioSettings.Master);
            if (_musicLabel != null) _musicLabel.text = "音乐音量 " + VolStep(AudioSettings.Music);
            if (_sfxLabel != null) _sfxLabel.text = "音效音量 " + VolStep(AudioSettings.Sfx);
        }

        static string VolStep(float v) { return Mathf.RoundToInt(v * 10f).ToString(); }

        string FsText() { return "全屏：" + (Screen.fullScreen ? "开" : "关"); }

        void ToggleFullscreen() { Screen.fullScreen = !Screen.fullScreen; if (_fsLabel != null) _fsLabel.text = FsText(); }

        /// <summary>从暂停菜单打开设置（隐藏暂停面板，返回时恢复）。</summary>
        void OpenSettingsFromPause()
        {
            if (_pauseMenu != null) _pauseMenu.SetActive(false);
            ShowSettings();
        }

        void HideSettings()
        {
            if (_settingsMenu != null) { Destroy(_settingsMenu); _settingsMenu = null; }
            _masterLabel = null;
            _musicLabel = null;
            _sfxLabel = null;
            _fsLabel = null;
            // 从暂停菜单进入的设置，返回时回到暂停菜单（继续暂停）
            if (_pauseMenu != null && Time.timeScale == 0f)
                _pauseMenu.SetActive(true);
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
            WorldAxis = axis.rectTransform;
        }

        /// <summary>显示/隐藏背景坐标轴（选路界面用网格地图，不需要坐标轴背景）。</summary>
        public void SetWorldAxisVisible(bool visible)
        {
            if (WorldAxis != null) WorldAxis.gameObject.SetActive(visible);
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
            MusicPlayer.Instance?.Play(MusicTrack.Title);
            if (_titleFx != null) _titleFx.SetVisible(true);
            EnsureWorld();
            SetWorldAxisVisible(true);
            RefreshTitle();   // 存档状态可能已变（新建 / 清除），回到标题时重算按钮
            Switch(() => {
                ClearScreen();
                if (_titleRoot != null) ShowFrame(_titleRoot);
                else gameObject.AddComponent<TitleScreen>(); // 回退：运行时生成标题
            });
        }

        public void ShowClassSelect()
        {
            MusicPlayer.Instance?.Play(MusicTrack.Map);   // 从标题开始游戏：无缝切到地图音乐
            if (_titleFx != null) _titleFx.SetVisible(false);
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(_classSelectRoot); gameObject.AddComponent<ClassSelectScreen>(); });
        }

        /// <summary>按选定职业开一局：建持久状态 + 路线图，进路线界面。</summary>
        public void StartRun(ClassData cls)
        {
            _cls = cls;
            _player = new PlayerState(cls.initialHealth);
            if (cls.startingRelic.HasValue) _player.relics.Add(cls.startingRelic.Value);   // 职业初始遗物
            _deck = new Deck(cls.startingDeck);
            _route = RouteMap.Generate();

            ShowRoute();
        }

        public void ShowRoute()
        {
            MusicPlayer.Instance?.Play(MusicTrack.Map);
            if (_titleFx != null) _titleFx.SetVisible(false);
            EnsureWorld();
            SetWorldAxisVisible(false);
            SaveGame();   // 自动存档（路线检查点）
            Switch(() => { ClearScreen(); ShowFrame(_routeRoot); gameObject.AddComponent<RouteScreen>(); });
        }

        /// <summary>战斗胜利战利品：三选一拿卡（BOSS 通关走结局，另议）。</summary>
        public void ShowCardReward()
        {
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<CardRewardScreen>(); });
        }

        /// <summary>精英胜利：获得遗物。</summary>
        public void ShowRelicReward()
        {
            EnsureWorld();
            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<RelicRewardScreen>(); });
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
            MusicPlayer.Instance?.Play(MusicTrack.Map);
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
            _route.movesTaken++;   // 移动一次（缩圈依据）

            // 缩圈：第 5 次移动起，安全圈向中心收缩，停在圈外掉血
            if (!_route.IsSafe(nodeIndex))
            {
                _player.health -= RouteMap.ShrinkDamage;
                if (_player.health <= 0)
                {
                    _player.health = 0;
                    SaveSystem.Clear();   // 圈外致死：清档回标题
                    ShowTitle();
                    return;
                }
            }

            if (node.type == NodeType.Camp)
            {
                Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<CampScreen>(); });
                return;
            }
            if (node.type == NodeType.Shop)
            {
                Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<ShopScreen>(); });
                return;
            }
            if (node.type == NodeType.Treasure)
            {
                Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<TreasureScreen>(); });
                return;
            }
            if (node.type == NodeType.Mystery)
            {
                // 问号可能出怪：约 1/7 概率是遭遇战（普通怪），其余走剧情事件
                if (UnityEngine.Random.value < 0.15f)
                {
                    StartBattle(node, NodeType.Battle);
                    return;
                }
                Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<MysteryScreen>(); });
                return;
            }

            StartBattle(node);
        }

        void StartBattle(MapNode node)
        {
            StartBattle(node, node.type);
        }

        void StartBattle(MapNode node, NodeType battleType)
        {
            IsShapeBoard = (battleType == NodeType.Elite || battleType == NodeType.Boss);
            MusicPlayer.Instance?.Play(battleType == NodeType.Boss ? MusicTrack.Boss : MusicTrack.Combat);
            var board = SpawnBoard(battleType, _route.movesTaken);
            _deck.Reset();   // 每场战斗前重置卡组
            // 怪物自然成长按「怪物等级」取档位（普通 / 精英 / BOSS 的具体数值见 Core/GrowthTable.cs）
            _turn = new TurnManager(_cls, board, _player, _deck, GameConfig.EnergyPerTurn, GrowthTable.Of(TierOf(battleType)));

            // 遗物「法力饼干盒」：战斗开始时往手牌里塞一张衍生牌
            if (_player.HasRelic(RelicType.ManaCookie))
                _turn.deck.hand.Add(CardLibrary.ManaCookie());

            Switch(() => { ClearScreen(); ShowFrame(null); gameObject.AddComponent<BattleScreen>(); });
        }

        /// <summary>把地图节点类型翻译成「怪物等级」（只有战斗/精英/BOSS 用到成长档位）。</summary>
        static MonsterTier TierOf(NodeType battleType)
        {
            switch (battleType)
            {
                case NodeType.Elite: return MonsterTier.Elite;
                case NodeType.Boss: return MonsterTier.Boss;
                default: return MonsterTier.Normal;
            }
        }

        BoardState SpawnBoard(NodeType type, int row)
        {
            MonsterShape shape;
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

            int bonus = Mathf.Max(0, row - 1);   // 越靠后越强（row 越大 bonus 越高）
            var board = new BoardState(shape.rows, shape.cols);
            foreach (var c in shape.cells)
            {
                int v = Mathf.Min(8, c.value + bonus);   // 封顶 8，避免开局即临界
                board.numbers.Add(new NumberData(v, c.row, c.col, c.kind, c.attackOrder, c.coreGroup));
            }
            return board;
        }

        /// <summary>回血（不超上限）。</summary>
        public void Heal(int amount) { _player.health = Mathf.Min(_player.health + amount, _player.maxHealth); }

        /// <summary>扣血。</summary>
        public void Damage(int amount) { _player.health -= amount; }

        /// <summary>强化一张随机牌，返回被强化的牌（供 UI 显示）。</summary>
        public CardData UpgradeRandomCard()
        {
            var all = FullDeck();
            if (all.Count == 0) return null;
            var card = all[UnityEngine.Random.Range(0, all.Count)];
            card.Upgrade();
            return card;
        }

        /// <summary>获得一张随机奖励牌（问号事件用）。</summary>
        public CardData AddRandomCard()
        {
            var choices = CardLibrary.RewardChoices(1, _cls.passive, _cls.tag);
            if (choices.Count == 0) return null;
            var card = choices[0];
            _deck.AddCard(card);
            return card;
        }

        /// <summary>花血（够扣返回 true 并扣血）。</summary>
        public bool SpendHealth(int amount)
        {
            if (_player.health <= amount) return false;
            _player.health -= amount;
            return true;
        }

        /// <summary>获得一个随机遗物（不重复），返回遗物（全拿过返回 null）。</summary>
        public Relic AddRandomRelic()
        {
            var relic = RelicCatalog.RandomUnowned(_player.relics);
            if (relic == null) return null;
            _player.relics.Add(relic.type);
            if (relic.type == RelicType.LifeCrystal)
            {
                _player.maxHealth += 5;
                _player.health = _player.maxHealth;
            }
            return relic;
        }

        /// <summary>买牌：花血加入卡组。</summary>
        public bool BuyCard(CardData card, int cost)
        {
            if (!SpendHealth(cost)) return false;
            _deck.AddCard(card);
            return true;
        }

        /// <summary>删一张随机牌（删不掉返回 null）。</summary>
        public CardData RemoveRandomCard()
        {
            var all = FullDeck();
            if (all.Count == 0) return null;
            var card = all[UnityEngine.Random.Range(0, all.Count)];
            _deck.RemoveCard(card);
            return card;
        }

        /// <summary>删一张指定牌（删不掉返回 false）。</summary>
        public bool RemoveCard(CardData card)
        {
            return _deck.RemoveCard(card);
        }

        /// <summary>强化一张指定牌。</summary>
        public void UpgradeCard(CardData card)
        {
            card.Upgrade();
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
                maxHealth = _player.maxHealth,
                relics = _player.relics,
                deck = FullDeck(),
                route = _route,
            };
            SaveSystem.Save(data);
        }

        /// <summary>继续上次运行（有存档则恢复并进路线；存档损坏或过期则清掉并退回选职业，避免按钮点了没反应）。</summary>
        public void ContinueGame()
        {
            var data = SaveSystem.Load();
            if (data == null || data.route == null || data.route.nodes == null || data.route.nodes.Count == 0
                || data.deck == null || data.deck.Count == 0)
            {
                SaveSystem.Clear();     // 读不出来就当没有，别让「继续游戏」永远点不动
                ShowClassSelect();
                return;
            }

            // 旧版本存档的节点索引可能越界，夹回合法范围，避免路线界面崩溃
            if (data.route.currentIndex < 0 || data.route.currentIndex >= data.route.nodes.Count)
                data.route.currentIndex = 0;

            _cls = FindClass(data.classId);
            _player = new PlayerState(data.health);
            _player.maxHealth = data.maxHealth > 0 ? data.maxHealth : data.health;
            if (data.relics != null) _player.relics = data.relics;
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
            var camp = GetComponent<CampScreen>(); if (camp != null) Destroy(camp);
            var mystery = GetComponent<MysteryScreen>(); if (mystery != null) Destroy(mystery);
            var shop = GetComponent<ShopScreen>(); if (shop != null) Destroy(shop);
            var treasure = GetComponent<TreasureScreen>(); if (treasure != null) Destroy(treasure);
            var relic = GetComponent<RelicRewardScreen>(); if (relic != null) Destroy(relic);
            var codex = GetComponent<CodexScreen>(); if (codex != null) Destroy(codex);
            var keywords = GetComponent<KeywordCodexScreen>(); if (keywords != null) Destroy(keywords);
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
