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
        bool _fireEnchanted;           // 下一次攻击是否附带灼烧（淬火）
        int _enchantBurnLayers;        // 附魔挂上去的灼烧层数（淬火 = 2），命中时叠加到目标格
        bool _negateNextAttack;        // 全副武装 / 法力护盾：敌人下一次攻击无效化（结算时消耗）
        int _onHurtAllDelta;           // 愈战愈勇挂起：玩家下次受伤后，所有格 -N（多张叠加）
        int _onHurtHeal;               // 愈战愈勇挂起：玩家下次受伤后，恢复 N 点生命（多张叠加）
        int _energyDelta;              // 上回合累积的「延迟能量」：下一回合并入能量（正=加费，负=扣费；多张叠加）
        int _drawDelta;                // 上回合累积的「延迟抽牌」：下一回合并入手牌数（正=多抽，负=少抽；多张叠加）
        readonly List<OverflowEvent> _lastOverflow = new List<OverflowEvent>();
        static readonly System.Random _rng = new System.Random();

        /// <summary>本场战斗怪物每回合的自然成长量（部位/核心各一个值，由 GameManager 按怪物等级传入）。</summary>
        public GrowthProfile growth;

        public TurnManager(ClassData playerClass, BoardState board, PlayerState player, Deck deck, int energyPerTurn,
            GrowthProfile? growth = null)
        {
            this.playerClass = playerClass;
            this.board = board;
            this.player = player;
            this.deck = deck;
            this.energyPerTurn = energyPerTurn;
            this.growth = growth ?? GrowthTable.Normal;   // 不传就用普通怪档位（?? = 左边为 null 时取右边）
            this.turnNumber = 1;
            this.phase = TurnPhase.PlayerTurn;
            _isMonster = board.numbers.Exists(n => n.kind != NumberKind.Normal);
            _invasion = !_isMonster;   // 入侵只在普通小怪战生效（怪物战靠部位咬人 + 核心爆炸给压力）
            StartTurn();   // 开局先抽满手牌 + 宣言首个攻击部位
        }

        /// <summary>
        /// 这个格子每回合会自然成长多少（核心用 <c>growth.Core</c>，其余用 <c>growth.Part</c>）。
        /// 战斗界面给每格标「+N」时调它，保证「显示的数字」和「实际加的数值」是同一个来源。
        /// </summary>
        public int GrowthOf(NumberData n)
        {
            if (n == null) return growth.Part;
            return n.kind == NumberKind.Core ? growth.Core : growth.Part;
        }

        /// <summary>回合开始：抽满手牌（并入上回合的延迟抽牌）+ 回满能量（并入延迟能量）+ 灼烧结算 + 宣言攻击部位。</summary>
        public void StartTurn()
        {
            int handSize = Deck.HandSize + (player.HasRelic(RelicType.GreedyHand) ? 1 : 0) + _drawDelta;
            if (handSize < 0) handSize = 0;   // 延迟抽牌最多扣到 0 张，不会变负
            deck.DrawToFull(handSize);
            _drawDelta = 0;                   // 结算后清空
            SfxPlayer.Instance?.Play(SfxType.Draw);
            energy = energyPerTurn + (player.HasRelic(RelicType.EnergyCore) ? 1 : 0) + _energyDelta;
            if (energy < 0) energy = 0;   // 延迟能量最多扣到 0，不会变负
            _energyDelta = 0;             // 结算后清空
            if (player.HasRelic(RelicType.Regen)) player.Heal(1);   // 再生之心：每回合回 1 血
            ApplyDelayedValueChanges();   // 上回合挂的「下个回合该格 -4」等延迟数值，在新回合开始时结算
            ResolveBurn();                // 灼烧：每个玩家回合开始时，带灼烧的格子数值 -（层数）
            TickFreeze();                 // 冻结：每个玩家回合开始时层数 -1（1 层 = 撑到下一回合开始）
            attacker = ResolveAttacker();
            // 延迟数值/灼烧可能正好打死核心（= 连坐崩掉同组全部部位），也可能打掉盘上最后一个数字。
            // 这里必须重新判一次胜负：否则会出现「所有数字都消失了、但 phase 还是玩家回合」——
            // 棋盘空了没东西可点，也没有胜利结算，战斗直接卡死（2026-10-18 用户报的 bug）。
            CheckOutcome();
        }

        /// <summary>结算延迟数值变化（菌种培育：下个回合该格 -4）。正=加，负=减；压到 0 则消灭。</summary>
        void ApplyDelayedValueChanges()
        {
            var snapshot = new List<NumberData>(board.numbers);
            foreach (var n in snapshot)
            {
                if (n.delayedValueDelta == 0) continue;
                int delta = n.delayedValueDelta;
                n.delayedValueDelta = 0;
                if (!board.numbers.Contains(n)) continue;   // 已被核心崩解移除
                int next = n.value + delta;
                if (next <= 0) DestroyNumber(n);
                else n.value = next > GameConfig.MaxValue ? GameConfig.MaxValue : next;
            }
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

            // 回血卡：无需目标，直接回血（同样可以带「延迟代价」，如休假：回 15 血但下回合 -2 能量）
            if (card.heal > 0)
            {
                player.health += card.heal;
                if (player.health > player.maxHealth) player.health = player.maxHealth;
                deck.PlayCard(card);
                energy -= card.cost;
                if (card.gainEnergy != 0) energy += card.gainEnergy;
                if (card.nextEnergyDelta != 0) _energyDelta += card.nextEnergyDelta;
                if (card.nextDrawDelta != 0) _drawDelta += card.nextDrawDelta;
                return true;
            }

            // 附魔卡：无需目标，给下一次数值攻击挂上灼烧层数（淬火 = 灼烧+2）
            if (card.enchant)
            {
                _fireEnchanted = true;
                _enchantBurnLayers = card.burnLayers > 0 ? card.burnLayers : GameConfig.EnchantBurnLayers;
                deck.PlayCard(card);
                energy -= card.cost;
                return true;
            }

            // 全副武装 / 法力护盾：无需目标 → 让敌人下回合的攻击无效化
            if (card.negateAttack)
            {
                _negateNextAttack = true;
                if (card.nextEnergyDelta != 0) _energyDelta += card.nextEnergyDelta;
                deck.PlayCard(card);
                energy -= card.cost;
                if (card.gainEnergy != 0) energy += card.gainEnergy;
                return true;
            }

            // 无目标卡（纯资源/效果牌，如衍生牌「法力饼干」、战士「愈战愈勇」）：无需选格子
            if (card.noTarget)
            {
                if (card.onHurtAllDelta != 0) _onHurtAllDelta += card.onHurtAllDelta;   // 挂起「受伤后全场 -N」
                if (card.onHurtHeal != 0) _onHurtHeal += card.onHurtHeal;               // 挂起「受伤后回 N 血」
                deck.PlayCard(card);
                energy -= card.cost;
                if (card.gainEnergy != 0) energy += card.gainEnergy;
                if (card.nextEnergyDelta != 0) _energyDelta += card.nextEnergyDelta;
                if (card.nextDrawDelta != 0) _drawDelta += card.nextDrawDelta;
                return true;
            }

            // 诱导卡：标记一个格子 → 立即获得非自然成长，并跳过下回合自然成长（需目标）
            if (card.induce > 0)
            {
                var t = board.At(row, col);
                if (t == null) return false;
                if (card.noCore && t.kind == NumberKind.Core) return false;
                if (t.freeze < 1) t.freeze = 1;   // 锁住该格下一个回合（冻结 1 层：不成长、不攻击）
                // 延迟数值变化（菌种培育：下个回合该格 -4）——同格多张叠加
                if (card.delayedValueDelta != 0) t.delayedValueDelta += card.delayedValueDelta;
                GainNonNatural(t, card.induce);   // 立即获得非自然成长（触发毒性标记 / 收割被动）
                deck.PlayCard(card);
                energy -= card.cost;
                CheckOutcome();
                return true;
            }

            // 毒性标记卡（毒性催化 / 收获）：给目标格挂一个标记状态，可叠加（需目标）
            if (card.toxinGoal > 0)
            {
                var t = board.At(row, col);
                if (t == null) return false;
                if (card.noCore && t.kind == NumberKind.Core) return false;
                t.toxins.Add(new ToxinMark(card.toxinGoal, card.toxinDamage));   // 标记平时不生效，等该格再获得非自然成长才累计
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
                if (card.noCore && t.kind == NumberKind.Core) return false;   // 镰刀横扫：不能对核心使用
                targets = new List<NumberData> { t };
            }

            // 结算（淬火/火球术 → 叠加灼烧层数；冻结 → 叠加冻结层数）。
            // 注意这里对每个命中格都算一次，所以「全体 -1」这类 AOE 会把灼烧铺到全场。
            // ⚠️ 灼烧/冻结层数是「卡面写多少就是多少」：**不吃任何被动/遗物加成**
            //    （战士铁腕、铁腕护符的 +1 只加 MathOps 里的减法数值，不会动这些层数）。
            int burnLayers = (_fireEnchanted ? _enchantBurnLayers : 0) + card.burnLayers;
            var mainTargets = new List<NumberData>(targets);   // 倍化要「除主目标外」的格子，先留一份
            foreach (var t in targets)
            {
                if (!board.numbers.Contains(t)) continue; // 已被核心崩解移除
                ApplyToNumber(t, card, burnLayers, card.freezeLayers);
            }
            _fireEnchanted = false;
            _enchantBurnLayers = 0;

            // 倍化：主目标之外的所有格额外 -2（不叠灼烧、不吃被动，卡面写多少就是多少）
            if (card.otherCellsDelta != 0)
            {
                var others = board.numbers.FindAll(n => !mainTargets.Contains(n));
                foreach (var n in others)
                {
                    if (!board.numbers.Contains(n)) continue;
                    int v = n.value + card.otherCellsDelta;
                    if (v <= 0) DestroyNumber(n);
                    else n.value = v > GameConfig.MaxValue ? GameConfig.MaxValue : v;
                }
            }

            deck.PlayCard(card);   // 出牌 → 弃牌堆
            energy -= card.cost;   // 扣费
            if (card.gainEnergy != 0) energy += card.gainEnergy;                    // 出牌返能量（倍增/倍化）
            if (card.nextEnergyDelta != 0) _energyDelta += card.nextEnergyDelta;   // 延迟能量：并入下一回合（多张叠加）
            if (card.nextDrawDelta != 0) _drawDelta += card.nextDrawDelta;         // 延迟抽牌：并入下一回合（多张叠加）
            CheckOutcome();
            return true;
        }

        /// <summary>对单个数字结算一次卡牌运算（压到 0 = 消灭；玩家催化推过上限 = 消灭；核心崩 → 崩同组部位；叠灼烧/冻结；preGrowth → 先 +N 非自然成长）。</summary>
        void ApplyToNumber(NumberData target, CardData card, int burnLayers, int freezeLayers)
        {
            // 镰刀横扫：主运算之前先给该格 +1（走非自然成长管线，会推进毒性标记/收割被动）
            if (card.preGrowth > 0)
            {
                GainNonNatural(target, card.preGrowth);
                if (!board.numbers.Contains(target)) return;   // 该格已被消灭（推过上限 / 毒性引爆）
            }

            // 条件强化：满足条件就用 bonusMagnitude 顶替原本的数值（极寒：目标已冻结 2→4；奥术彗星：九宫格内有同值 2→6）
            int magnitude = MeetsCondition(card, target) ? card.bonusMagnitude : card.magnitude;

            int before = target.value;
            int next = MathOps.Apply(before, card.op, magnitude, playerClass.passive, player.relics);

            // 玩家出牌把数字推过上限 → 该格直接消灭（上限只保护 UI，不会砍死本就很高的大数值怪）
            if (next > GameConfig.MaxValue) { DestroyNumber(target); return; }

            if (burnLayers > 0) target.burn += burnLayers;        // 灼烧叠加（淬火 +2 / 火球术 +2，AOE 时每格都加）
            if (freezeLayers > 0) target.freeze += freezeLayers;  // 冻结叠加（冻结 +2）

            // 玩家把数字抬高（加法 / 乘法）在语义上也是「非自然成长」：
            // 走 GainNonNatural 管线，才能推进毒性催化、才能触发收割被动。
            if (next > before)
            {
                GainNonNatural(target, next - before);
                return;
            }

            target.value = next;
            if (target.value <= 0) DestroyNumber(target);
        }

        /// <summary>
        /// 这张牌的「强化条件」是否满足。满足时用 <see cref="CardData.bonusMagnitude"/> 代替原数值。
        /// 目前两种：极寒（目标已被冻结）、奥术彗星（九宫格内有相同数字）。
        /// </summary>
        bool MeetsCondition(CardData card, NumberData target)
        {
            switch (card.condition)
            {
                case CardCondition.TargetFrozen:
                    return target.freeze > 0;

                case CardCondition.SameValueAround:
                    return HasSameValueAround(target);

                default:
                    return false;   // CardCondition.None：没有强化，用卡面原数值
            }
        }

        /// <summary>目标格周围九宫格（3×3，不含自己）里，是否存在与它数值相同的格子。</summary>
        bool HasSameValueAround(NumberData target)
        {
            foreach (var n in board.numbers)
            {
                if (n == target) continue;
                if (System.Math.Abs(n.row - target.row) > 1) continue;
                if (System.Math.Abs(n.col - target.col) > 1) continue;
                if (n.value == target.value) return true;
            }
            return false;
        }

        /// <summary>消灭一个格子；若是核心，连坐崩掉同组所有部位（多核心逐核坏死）。</summary>
        void DestroyNumber(NumberData n)
        {
            board.numbers.Remove(n);
            if (n.kind == NumberKind.Core)
                board.numbers.RemoveAll(x => x.kind == NumberKind.Part && x.coreGroup == n.coreGroup);
        }

        /// <summary>非自然成长（卡牌施加的成长）：加值 → 推进毒性标记 → 触发收割被动。玩家催化推过上限则该格消灭。</summary>
        void GainNonNatural(NumberData n, int amount)
        {
            if (amount <= 0) return;
            n.value += amount;

            if (n.value > GameConfig.MaxValue)
            {
                // 玩家催化把数字推过上限 → 该格直接消灭
                DestroyNumber(n);
            }
            else if (n.toxins.Count > 0)
            {
                ResolveToxinMarks(n, amount);
            }

            // 收割被动：怪物获得非自然成长 → 所有格子 -1
            if (playerClass.passive == PassiveType.Harvest) HarvestAllCells();
        }

        /// <summary>
        /// 结算格子上的毒性标记：每个标记各自累计本次的非自然成长量，累计够 goal 就对该格造成 damage 点直接伤害并移除该标记。
        /// 同一格多个标记互不影响（一次 +3 可能同时引爆 (1→2伤) 和 (2→4伤)，合计 6 伤）。
        /// </summary>
        void ResolveToxinMarks(NumberData n, int growth)
        {
            int totalDamage = 0;
            for (int i = n.toxins.Count - 1; i >= 0; i--)   // 倒序遍历，方便就地移除
            {
                var mark = n.toxins[i];
                mark.progress += growth;
                if (mark.progress >= mark.goal)
                {
                    totalDamage += mark.damage;
                    n.toxins.RemoveAt(i);
                }
            }
            if (totalDamage > 0) DealDirectDamage(n, totalDamage);
        }

        /// <summary>直接减值（不走运算钳制）；压到 0 则消灭，核心崩解连坐同组部位。</summary>
        void DealDirectDamage(NumberData n, int amount)
        {
            n.value -= amount;
            if (n.value <= 0) DestroyNumber(n);
        }

        /// <summary>收割：所有格子 -1（压到 0 则消灭）。</summary>
        void HarvestAllCells()
        {
            var snapshot = new List<NumberData>(board.numbers);
            foreach (var n in snapshot)
            {
                if (!board.numbers.Contains(n)) continue;   // 已被核心崩解移除
                DealDirectDamage(n, 1);
            }
        }

        /// <summary>结束回合（一次性结算，兼容旧调用）：攻击 → 增殖 → 溢出 → 判胜负 →（进入新回合时结算灼烧）。</summary>
        public void EndTurn()
        {
            if (phase != TurnPhase.PlayerTurn) return;
            ResolveAttack();
            ResolveGrowth();
            ResolveOverflow();
            FinishTurn();   // FinishTurn 会调 StartTurn，灼烧在 StartTurn 里结算
        }

        /// <summary>怪物攻击结算：宣言部位还活着 → 扣「当前值」血（被减 = 减伤，被打掉 = 打断无伤）。</summary>
        /// <remarks>
        /// 两种情况这一次攻击直接跳过：①「全副武装 / 法力护盾」的免伤护盾生效；② 攻击部位本身被「冻结」了。
        /// </remarks>
        public void ResolveAttack()
        {
            bool negated = _negateNextAttack;
            _negateNextAttack = false;      // 无论是否真的挨打，护盾都只吃一次
            LastAttackSkippedByFreeze = false;

            if (negated)
            {
                LastAttackNegated = true;
                return;
            }
            LastAttackNegated = false;

            if (attacker == null || !board.numbers.Contains(attacker)) return;

            // 被冻结的攻击部位：这一轮不动手（冻结关键词：跳过该格接下来的 N 个回合，不会攻击）
            if (attacker.freeze > 0)
            {
                LastAttackSkippedByFreeze = true;
                return;
            }

            int hpBefore = player.health;
            player.health -= attacker.value;
            if (player.health < hpBefore) TriggerOnHurt();   // 真的掉血了 → 结算挂起的「受伤后」效果
        }

        /// <summary>
        /// 结算「受到伤害后」的挂起效果（愈战愈勇）：所有格 -N，并恢复 N 点生命。
        /// 只在玩家**真的掉血**时触发一次；触发后清空挂起值。
        /// </summary>
        void TriggerOnHurt()
        {
            if (_onHurtAllDelta == 0 && _onHurtHeal <= 0) return;

            int allDelta = _onHurtAllDelta;
            int heal = _onHurtHeal;
            _onHurtAllDelta = 0;
            _onHurtHeal = 0;

            if (allDelta != 0)
            {
                var snapshot = new List<NumberData>(board.numbers);
                foreach (var n in snapshot)
                {
                    if (!board.numbers.Contains(n)) continue;   // 已被核心崩解移除
                    int v = n.value + allDelta;
                    if (v <= 0) DestroyNumber(n);
                    else n.value = v > GameConfig.MaxValue ? GameConfig.MaxValue : v;
                }
            }
            if (heal > 0) player.Heal(heal);
        }

        /// <summary>是否已挂起「受伤后」效果（UI 可以显示反击状态）。</summary>
        public bool OnHurtArmed => _onHurtAllDelta != 0 || _onHurtHeal > 0;

        /// <summary>上一次攻击是否被护盾挡下（UI 反馈用）。</summary>
        public bool LastAttackNegated { get; private set; }

        /// <summary>上一次攻击是否因为「攻击部位被冻结」而跳过（UI 反馈用）。</summary>
        public bool LastAttackSkippedByFreeze { get; private set; }

        /// <summary>下一回合的攻击是否已被护盾免疫（UI 可以据此显示格挡状态）。</summary>
        public bool NextAttackNegated => _negateNextAttack;

        /// <summary>
        /// 灼烧结算：**每个玩家回合开始时**，带灼烧的格子数值 -(灼烧层数)；烧到 0 消灭（核心崩 → 崩同组部位）。
        /// 灼烧层数可叠加（淬火 +2 / 烈焰 +1 / AOE 时每格都加），结算后层数不清空 —— 每回合持续生效。
        /// </summary>
        public void ResolveBurn()
        {
            var burned = board.numbers.FindAll(n => n.burn > 0);
            if (burned.Count == 0) return;
            SfxPlayer.Instance?.Play(SfxType.Burn);

            int totalLayers = 0;
            foreach (var n in burned) totalLayers += n.burn;
            foreach (var n in burned)
            {
                if (!board.numbers.Contains(n)) continue;   // 已被核心崩解移除
                DealDirectDamage(n, n.burn);                 // 直接扣层数，不走运算钳制
            }
            _lastBurnTick = totalLayers;   // 供 UI 显示「灼烧结算了多少点」
        }

        /// <summary>上一次灼烧结算的总伤害（UI 反馈用）。</summary>
        public int LastBurnTick => _lastBurnTick;
        int _lastBurnTick;

        /// <summary>每个玩家回合开始时给所有冻结层数 -1（到 0 就解除冻结）。</summary>
        void TickFreeze()
        {
            foreach (var n in board.numbers)
                if (n.freeze > 0) n.freeze--;
        }

        /// <summary>
        /// 增殖：按本场战斗的成长档位（`growth`）给每格加值——核心用 Core、其他用 Part。
        /// 被「冻结」的格子跳过这一轮；自然成长推过上限只调回上限，不会消灭（见 §上限规则）。
        /// </summary>
        public void ResolveGrowth()
        {
            for (int i = 0; i < board.numbers.Count; i++)
            {
                var n = board.numbers[i];
                if (n.freeze > 0) continue;   // 冻结期间不成长（层数在 StartTurn 里扣）
                n.value += GrowthOf(n);
                if (n.value > GameConfig.MaxValue) n.value = GameConfig.MaxValue;   // 自然成长超过上限 → 不消灭，调回上限
            }
        }

        /// <summary>溢出结算：当前版本已取消「承载上限与溢出」——怪物数字无限成长，不再爆掉也不再扣血。保留本方法供战斗回合流程调用，恒为空。</summary>
        public void ResolveOverflow()
        {
            _lastOverflow.Clear();

            // ---- 旧「溢出 + 核心爆炸」机制已停用，保留待查 ----
            // 原判定：n.value > n.Cap（承载上限）时——
            //   普通数字：造成 (值 - 上限) 点伤害后消失；
            //   核心：造成 CoreExplosionDamage 点伤害后回落到 CoreResetValue。
            // 停用后 LastOverflow 恒为空，战斗界面的溢出爆炸反馈不会触发。
        }

        /// <summary>判胜负；未分胜负则进入下一回合（回合数 +1、入侵、抽牌）。</summary>
        public void FinishTurn()
        {
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
