namespace ShuYu
{
    public enum TurnPhase { PlayerTurn, Won, Lost }

    /// <summary>
    /// 回合状态机：抽牌 → 玩家行动 → 增殖 → 溢出 → 判胜负（SPEC 5，入侵留待后续）。
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

        public TurnManager(ClassData playerClass, BoardState board, PlayerState player, Deck deck, int energyPerTurn)
        {
            this.playerClass = playerClass;
            this.board = board;
            this.player = player;
            this.deck = deck;
            this.energyPerTurn = energyPerTurn;
            this.turnNumber = 1;
            this.phase = TurnPhase.PlayerTurn;
            StartTurn();   // 开局先抽满手牌
        }

        /// <summary>回合开始：抽满手牌 + 回满能量。</summary>
        public void StartTurn()
        {
            deck.DrawToFull();
            energy = energyPerTurn;
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
                board.numbers.Remove(target); // 压到 0 = 消灭

            deck.PlayCard(card);   // 出牌 → 弃牌堆
            energy -= card.cost;   // 扣费
            CheckOutcome();
            return true;
        }

        /// <summary>结束回合：增殖 → 溢出 → 判胜负，未分胜负则进入下一回合。</summary>
        public void EndTurn()
        {
            if (phase != TurnPhase.PlayerTurn) return;

            // 1. 增殖：场上每个数字 +1
            for (int i = 0; i < board.numbers.Count; i++)
                board.numbers[i].value += 1;

            // 2. 溢出：> 9 的数字扣（数字 - 9）血并从场上消失
            int totalDamage = 0;
            for (int i = board.numbers.Count - 1; i >= 0; i--)
            {
                var n = board.numbers[i];
                if (n.value > GameConfig.OverflowThreshold)
                {
                    totalDamage += n.value - GameConfig.OverflowThreshold;
                    board.numbers.RemoveAt(i);
                }
            }
            player.health -= totalDamage;

            // 嗜血：溢出造成的伤害回血一半（向下取整）
            if (playerClass.passive == PassiveType.Bloodthirst && totalDamage > 0)
                player.health += totalDamage / 2;

            // 3. 判胜负；未分出胜负则进入下一回合
            CheckOutcome();
            if (phase == TurnPhase.PlayerTurn)
            {
                turnNumber++;
                StartTurn();
            }
        }

        void CheckOutcome()
        {
            if (player.health <= 0) phase = TurnPhase.Lost;
            else if (board.numbers.Count == 0) phase = TurnPhase.Won;
        }
    }
}
