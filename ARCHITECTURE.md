# 《数域》代码结构说明（给团队）

> 更新 2026-10-13 · Unity 2022.3.62f3c1 · 场景化 UI（编辑器生成 Main.unity，Play 前即可见，团队可直接改）

## 怎么跑起来

1. 首次（或静态 UI 结构变了）：点菜单 **ShuYu/Build Main Scene**，生成 `Assets/Scenes/Main.unity` 并写入 Build Settings。
2. 直接运行 Main 场景：标题界面 Play 前就可见；点「开始游戏」→ 选职 → 路线 → 战斗照常。
3. 静态部分（背景 / 坐标轴 / 标题 / 选职卡 / 路线标题按钮）都在场景里，团队成员可直接在 Hierarchy / Inspector 改；动态内容（手牌 / 棋盘数字 / 路线节点 / 能量 / 血量）仍是运行时填充。
4. 选职 / 路线 的静态框架已场景化（运行时只接线 + 填动态）；战斗屏仍由 `GameManager` 运行时生成。

## 文件作用（`Assets/Scripts/`）

| 文件 | 作用 |
|---|---|
| `GameManager.cs` | 顶层入口 / 单例（场景组件）。持有对局 `TurnManager`，驱动 标题 ↔ 选职 ↔ 路线 ↔ 战斗 切换；`BindScene` 绑定场景静态 UI，无场景时回退运行时生成。 |
| `TitleScreen.cs` | 标题界面（坐标系 + 菜单）。 |
| `ClassSelectScreen.cs` | 职业选择界面（三职业卡片）。 |
| `BattleScreen.cs` | 对局界面（棋盘 + 手牌 + 血条 + 胜负覆盖层）。 |
| `CardRewardScreen.cs` | 战斗胜利三选一拿卡界面。 |
| `EndingScreen.cs` | BOSS 通关结局屏（通 关 + 剩余生命 + 再来一局/回标题）。 |
| `CRTPostFX.cs` | CRT 后处理：Bloom 辉光 + 暗角 + 色差 + 扫描线（挂 GameManager，Inspector 可调参数）。 |
| `Editor/SceneBuilder.cs` | 编辑器菜单 `ShuYu/Build Main Scene`：生成 Main.unity + 持久化字体/圆角/坐标轴资产。 |
| `UIFactory.cs` | 程序化 UI 工厂：建 Canvas / 字体 / 圆角 Sprite / 按钮 / 文本 / 定位 / 描边 / 扫描线。零手绘。 |
| `Core/GameTypes.cs` | 数据模型 + 运算规则：`Operation`/`TargetType` 枚举、`NumberData`（数字=瘤块）、`CardData`（卡牌）、`BoardState`（棋盘）、`PlayerState`（玩家）、`MathOps`（运算结算）。 |
| `Core/GameConfig.cs` | 常量 + 色板（数值参数 + Hex 颜色）。 |
| `Core/TurnManager.cs` | 回合状态机：抽牌 → 行动 → 增殖 → 溢出 → 判胜负。纯逻辑，不碰 UI。 |
| `Core/Deck.cs` | 卡组：抽牌堆 / 弃牌堆 / 手牌（上限 5）。 |
| `Core/CardLibrary.cs` | 卡牌工厂 + 各职业起始卡组。 |
| `Core/ClassDefs.cs` | `PassiveType` 被动枚举 + `ClassData` 职业数据 + `ClassCatalog` 三职业目录。 |

## 素材库在哪

- `Assets/Resources/Fonts/` —— 全部字体，运行时 `Resources.Load<Font>` 加载：
  - `VT323-Regular.ttf`（像素数字/拉丁，主字体）
  - `ark-pixel-12px-monospaced-zh_hans.ttf`（中文像素，fallback）
  - `JetBrainsMono-*.ttf`（等宽）、`SourceHanSansSC-*.otf`（思源黑体）、`SimHei.ttf`（兜底）
- **没有图片素材**。颜色全部在 `GameConfig` 色板里定义；圆角矩形 Sprite 由 `UIFactory` 程序化生成。

## 卡牌包设计

- `Core/CardLibrary.cs`：`Make(Operation op, int magnitude)` 造一张卡；`WarriorDeck()/MageDeck()/ReaperDeck()` 返回各职业起始卡组。
- 卡牌数据类 = `Core/GameTypes.cs` 的 `CardData`（运算 `op` / 目标 `target` / 数值 `magnitude`）。
- 目标类型 `TargetType`（单格/行/列/全体）已定义，目前只用了「单格」。
- 已有 `cost` 费用字段（能量系统，3 能量/回合）；职业专属卡待讨论。

## 角色状态类在哪

- **玩家血量**：`Core/GameTypes.cs` 的 `PlayerState`（目前只有 `health`）。运行时用 `GameManager.Instance.Player` 访问。
- **职业数据**：`Core/ClassDefs.cs` 的 `ClassData`（名字 / 被动类型 / 起始卡组）。运行时用 `GameManager.Instance.Turn.playerClass` 访问。
- **回合/棋盘/卡组状态**：`Core/TurnManager.cs`（`board`、`player`、`deck`、`energy`、`phase`）。

## 数据流

```
GameManager（入口 / 切屏 / 单例）
   └─ TurnManager（回合逻辑，纯逻辑）
        ├─ BoardState   棋盘：数字列表
        ├─ PlayerState  血条
        ├─ Deck         抽牌堆 / 弃牌堆 / 手牌
        └─ ClassData    职业 + 被动
UI 层（TitleScreen / ClassSelectScreen / BattleScreen）
   └─ 只读 GameManager 暴露的状态，用 UIFactory 铺界面；操作回调回 GameManager
```

## 约定（团队统一）

- 纯逻辑层（`Core/TurnManager`、`GameTypes`、`Deck` 等）不碰 UI；UI 层（`*Screen`、`UIFactory`）不写游戏规则。
- 中文注释 + `///` XML 文档。
- 颜色统一走 `GameConfig` 色板，禁止散落的魔法色值。
- 视效（CRT / 描边 / 动效）按统一风格接入，别各写各的。
