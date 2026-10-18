using System.Collections.Generic;

namespace ShuYu
{
    public enum TurnPhase { PlayerTurn, Won, Lost }

    /// <summary>
    /// 回合状态机：抽牌 → 玩家行动 → 怪物攻击结算 → 增殖 → 核心爆炸/溢出 → 入侵 → 判胜负（SPEC 5）。
    /// 纯逻辑层，不碰 UI，方便调试与后续序列化。
    /// </summary>
    public class TurnManager
    {
        public BoardState board;
        public PlayerState player;
        public Deck deck;
        public ClassData playerClass;
        public int energy;          // 当前能量
        public int energyPerTurn;   // 每回合能量上限
        public int turnNumber;
        public TurnPhase phase;
        public bool FireEnchanted => _fireEnchanted;  // 附魔是否激活（下一张攻击带灼烧）
        public IReadOnlyList<OverflowEvent> LastOverflow => _lastOverflow;  // 本回合溢出事件（UI 播爆炸动画用）

        public NumberData attacker;    // 本回合宣言攻击的部位（UI 高亮；无怪/无部位则为 null）
        int _lastAttackOrder = -1;     // 固定攻击顺序游标：上一个宣言部位的 attackOrder
        bool _isMonster;               // 这场是否有核心/部位（怪物战，胜=打爆核心；否则胜=清空）
        bool _invasion;                // 是否启用数字入侵（普通小怪战的生存压力源）
        bool _fireEnchanted;           // 下一次攻击是否附带灼烧（火焰附魔）
        readonly List<OverflowEvent> _lastOverflow = new List<OverflowEvent>();
        static readonly System.Random _rng = new System.Random();

        public TurnManager(ClassData playerClass, BoardState board, PlayerState player, Deck deck, int energyPerTurn)
        {
            this.playerClass = playerClass;
            this.board = board;
            this.player = player;
            this.deck = deck;
            this.energyPerTurn = energyPerTurn;
            this.turnNumber = 1;
            this.phase = TurnPhase.PlayerTurn;
            _isMonster = board.numbers.Exists(n => n.kind != NumberKind.Normal);
            _invasion = !_isMonster;   // 入侵只在普通小怪战生效（怪物战靠部位咬人 + 核心爆炸给压力）
            StartTurn();   // 开局先抽满手牌 + 宣言首个攻击部位
        }

        /// <summary>回合开始：抽满手牌 + 回满能量 + 宣言本回合攻击部位。</summary>
        public void StartTurn()
        {
            deck.DrawToFull();
            SfxPlayer.Instance?.Play(SfxType.Draw);
            energy = energyPerTurn;
            attacker = ResolveAttacker();
        }

        /// <summary>按固定顺序找下一个还活着的攻击部位（部位按 attackOrder 升序轮换，死掉的自动跳过）。</summary>
        NumberData ResolveAttacker()
        {
            var parts = new List<NumberData>();
            foreach (var n in board.numbers)
                if (n.kind == NumberKind.Part) parts.Add(n);
            parts.Sort((a, b) => a.attackOrder.CompareTo(b.attackOrder));
            if (parts.Count == 0) return null;

            foreach (var n in parts)
                if (n.attackOrder > _lastAttackOrder) { _lastAttackOrder = n.attackOrder; return n; }
            var first = parts[0];
            _lastAttackOrder = first.attackOrder;
            return first;
        }

        /// <summary>对目标格出牌。成功返回 true（附魔卡无需目标；单目标要对非空格；行/列/全体要目标上有数字）。</summary>
        public bool TryPlayCard(CardData card, int row, int col)
        {
            if (phase != TurnPhase.PlayerTurn || energy < card.cost) return false;
            if (!deck.hand.Contains(card)) return false; // 手牌里没有这张牌

            // 回血卡：无需目标，直接回血
            if (card.heal > 0)
            {
                player.health += card.heal;
                if (player.health > GameConfig.InitialHealth) player.health = GameConfig.InitialHealth;
                deck.PlayCard(card);
                energy -= card.cost;
                return true;
            }

            // 附魔卡：无需目标，给下一次攻击附魔火焰
            if (card.enchant)
            {
                _fireEnchanted = true;
                deck.PlayCard(card);
                energy -= card.cost;
                return true;
            }

            // 收集目标
            List<NumberData> targets;
            if (card.target != TargetType.Single)
            {
                targets = board.numbers.FindAll(n =>
                    card.target == TargetType.Row ? n.row == row :
                    card.target == TargetType.Column ? n.col == col : true);
                if (targets.Count == 0) return false;
            }
            else
            {
                var t = board.At(row, col);
                if (t == null) return false; // 单目标不能对空格
                targets = new List<NumberData> { t };
            }

            // 结算（火焰附魔/烈焰 → 灼烧；冰霜 → 冰冻）
            bool fire = _fireEnchanted || card.burns;
            bool freeze = card.freezes;
            foreach (var t in targets)
            {
                if (!board.numbers.Contains(t)) continue; // 已被核心崩解移除
                ApplyToNumber(t, card, fire, freeze);
            }
            _fireEnchanted = false;

            deck.PlayCard(card);   // 出牌 → 弃牌堆
            energy -= card.cost;   // 扣费
            CheckOutcome();
            return true;
        }

        /// <summary>对单个数字结算一次卡牌运算（压到 0 = 消灭；核心崩 → 崩同组部位；fire → 灼烧；freeze → 冰冻）。</summary>
        void ApplyToNumber(NumberData target, CardData card, bool fire, bool freeze)
        {
            target.value = MathOps.Apply(target.value, card.op, card.magnitude, playerClass.passive);
            if (target.value <= 0)
            {
                board.numbers.Remove(target);
                if (target.kind == NumberKind.Core)
                    board.numbers.RemoveAll(n => n.kind == NumberKind.Part && n.coreGroup == target.coreGroup); // 核心崩 → 只崩同组部位（多核心逐核坏死）
            }
            else
            {
                if (fire) target.burn = true;       // 灼烧
                if (freeze) target.frozen = true;   // 冰冻
            }
        }

        /// <summary>结束回合：怪物攻击 → 增殖 → 核心爆炸/溢出 → 判胜负，未分胜负则进入下一回合。</summary>
        public void EndTurn()
        {
            if (phase != TurnPhase.PlayerTurn) return;

            _lastOverflow.Clear();

            // 1. 怪物攻击结算：宣言部位还活着 → 扣「当前值」血（被减 = 减伤，被打掉 = 打断无伤）
            if (attacker != null && board.numbers.Contains(attacker))
                player.health -= attacker.value;

            // 1.5 灼烧：每回合 -1，烧到 0 消灭（核心崩 → 崩同组部位）
            var burned = board.numbers.FindAll(n => n.burn);
            if (burned.Count > 0) SfxPlayer.Instance?.Play(SfxType.Burn);
            foreach (var n in burned)
            {
                if (!board.numbers.Contains(n)) continue; // 已被核心崩解移除
                n.value -= 1;
                if (n.value <= 0)
                {
                    board.numbers.Remove(n);
                    if (n.kind == NumberKind.Core)
                        board.numbers.RemoveAll(x => x.kind == NumberKind.Part && x.coreGroup == n.coreGroup);
                }
            }

            // 2. 增殖：普通/部位 +1，核心 +2（喂养）；冰冻的数字跳过增殖
            for (int i = 0; i < board.numbers.Count; i++)
            {
                var n = board.numbers[i];
                if (n.frozen) { n.frozen = false; continue; }  // 冰冻：跳过本轮增殖
                n.value += (n.kind == NumberKind.Core) ? GameConfig.CoreGrowth : 1;
            }

            // 3. 溢出 + 核心爆炸
            int totalDamage = 0;
            bool overflowed = false;
            for (int i = board.numbers.Count - 1; i >= 0; i--)
            {
                var n = board.numbers[i];
                if (n.kind == NumberKind.Core)
                {
                    if (n.value > GameConfig.OverflowThreshold) // ≥10 蓄满爆炸
                    {
                        totalDamage += GameConfig.CoreExplosionDamage;
                        _lastOverflow.Add(new OverflowEvent(n.row, n.col, GameConfig.CoreExplosionDamage, true));
                        n.value = GameConfig.CoreResetValue;    // 爆完回落
                        overflowed = true;
                    }
                }
                else if (n.value > GameConfig.OverflowThreshold)
                {
                    int dmg = n.value - GameConfig.OverflowThreshold;
                    totalDamage += dmg;
                    _lastOverflow.Add(new OverflowEvent(n.row, n.col, dmg, false));
                    board.numbers.RemoveAt(i);
                    overflowed = true;
                }
            }
            if (overflowed) SfxPlayer.Instance?.Play(SfxType.Overflow);
            player.health -= totalDamage;

            // 嗜血：溢出造成的伤害回血一半（向下取整）
            if (playerClass.passive == PassiveType.Bloodthirst && totalDamage > 0)
                player.health += totalDamage / 2;

            // 4. 判胜负；未分出胜负则进入下一回合
            CheckOutcome();
            if (phase == TurnPhase.PlayerTurn)
            {
                turnNumber++;
                TryInvasion();   // 入侵：每 N 回合往随机空格投新数字（SPEC 4）
                StartTurn();
            }
        }

        /// <summary>入侵：普通小怪战每 N 回合投一批新数字（SPEC 4：每 2 回合 +2 个，值 1~3，投随机空格）。</summary>
        void TryInvasion()
        {
            if (!_invasion) return;
            if (turnNumber % GameConfig.InvasionInterval != 0) return;
            for (int i = 0; i < GameConfig.InvasionCount; i++)
                SpawnInvader();
        }

        /// <summary>往一个随机空格投一个 1~3 的新数字；棋盘满了则跳过（满盘本身已是压力）。</summary>
        void SpawnInvader()
        {
            var empty = new List<int>();
            for (int r = 0; r < board.rows; r++)
                for (int c = 0; c < board.cols; c++)
                    if (board.At(r, c) == null && !board.IsCornerCell(r, c))   // 史莱姆形状：切角格不投新数字
                        empty.Add(r * board.cols + c);
            if (empty.Count == 0) return;

            int idx = empty[_rng.Next(empty.Count)];
            int value = _rng.Next(GameConfig.InvasionMinValue, GameConfig.InvasionMaxValue + 1);
            board.numbers.Add(new NumberData(value, idx / board.cols, idx % board.cols));
        }

        void CheckOutcome()
        {
            if (player.health <= 0) { phase = TurnPhase.Lost; return; }
            if (_isMonster)
            {
                // 怪物战：核心归零即胜（核心被打爆时已把部位一起清掉）
                if (!board.numbers.Exists(n => n.kind == NumberKind.Core)) phase = TurnPhase.Won;
            }
            else if (board.numbers.Count == 0)
            {
                phase = TurnPhase.Won; // 小怪战：清空棋盘即胜
            }
        }
    }
}
