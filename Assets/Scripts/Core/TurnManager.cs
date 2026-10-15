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

        public NumberData attacker;    // 本回合宣言攻击的部位（UI 高亮；无怪/无部位则为 null）
        int _lastAttackOrder = -1;     // 固定攻击顺序游标：上一个宣言部位的 attackOrder
        bool _isMonster;               // 这场是否有核心/部位（怪物战，胜=打爆核心；否则胜=清空）
        bool _invasion;                // 是否启用数字入侵（普通小怪战的生存压力源）
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

        /// <summary>对目标格出牌。成功返回 true（不能对空格 / 非玩家回合 / 能量不足 / 手牌里没这张牌）。</summary>
        public bool TryPlayCard(CardData card, int row, int col)
        {
            if (phase != TurnPhase.PlayerTurn || energy < card.cost) return false;

            var target = board.At(row, col);
            if (target == null) return false; // 不能对空格出牌
            if (!deck.hand.Contains(card)) return false; // 手牌里没有这张牌

            target.value = MathOps.Apply(target.value, card.op, card.magnitude, playerClass.passive);
            if (target.value <= 0)
            {
                board.numbers.Remove(target);   // 压到 0 = 消灭
                if (target.kind == NumberKind.Core)
                    board.numbers.RemoveAll(n => n.kind == NumberKind.Part); // 核心崩 → 整怪部位一起消散
            }

            deck.PlayCard(card);   // 出牌 → 弃牌堆
            energy -= card.cost;   // 扣费
            CheckOutcome();
            return true;
        }

        /// <summary>结束回合：怪物攻击 → 增殖 → 核心爆炸/溢出 → 判胜负，未分胜负则进入下一回合。</summary>
        public void EndTurn()
        {
            if (phase != TurnPhase.PlayerTurn) return;

            // 1. 怪物攻击结算：宣言部位还活着 → 扣「当前值」血（被减 = 减伤，被打掉 = 打断无伤）
            if (attacker != null && board.numbers.Contains(attacker))
                player.health -= attacker.value;

            // 2. 增殖：普通/部位 +1，核心 +2（喂养）
            for (int i = 0; i < board.numbers.Count; i++)
                board.numbers[i].value += (board.numbers[i].kind == NumberKind.Core) ? GameConfig.CoreGrowth : 1;

            // 3. 溢出 + 核心爆炸
            int totalDamage = 0;
            for (int i = board.numbers.Count - 1; i >= 0; i--)
            {
                var n = board.numbers[i];
                if (n.kind == NumberKind.Core)
                {
                    if (n.value > GameConfig.OverflowThreshold) // ≥10 蓄满爆炸
                    {
                        totalDamage += GameConfig.CoreExplosionDamage;
                        n.value = GameConfig.CoreResetValue;    // 爆完回落
                    }
                }
                else if (n.value > GameConfig.OverflowThreshold)
                {
                    totalDamage += n.value - GameConfig.OverflowThreshold;
                    board.numbers.RemoveAt(i);
                }
            }
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
            for (int r = 0; r < board.size; r++)
                for (int c = 0; c < board.size; c++)
                    if (board.At(r, c) == null) empty.Add(r * board.size + c);
            if (empty.Count == 0) return;

            int idx = empty[_rng.Next(empty.Count)];
            int value = _rng.Next(GameConfig.InvasionMinValue, GameConfig.InvasionMaxValue + 1);
            board.numbers.Add(new NumberData(value, idx / board.size, idx % board.size));
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
