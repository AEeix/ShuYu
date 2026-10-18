using System.Collections.Generic;

namespace ShuYu
{
    /// <summary>
    /// 卡组：抽牌堆 / 弃牌堆 / 手牌。纯逻辑、可序列化（为 M5 异步幽灵预留）。
    /// </summary>
    public class Deck
    {
        public const int HandSize = 5;   // 手牌上限（SPEC 4）

        public List<CardData> drawPile = new List<CardData>();
        public List<CardData> discardPile = new List<CardData>();
        public List<CardData> hand = new List<CardData>();

        static readonly System.Random _rng = new System.Random();

        public Deck(IEnumerable<CardData> cards)
        {
            drawPile.AddRange(cards);
            Shuffle(drawPile);
        }

        /// <summary>抽一张进手牌。抽牌堆空则把弃牌堆洗回；仍无牌返回 null。</summary>
        public CardData Draw()
        {
            if (drawPile.Count == 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                Shuffle(drawPile);
            }
            if (drawPile.Count == 0) return null;

            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
            return card;
        }

        /// <summary>抽满手牌至指定上限（默认 HandSize，遗物可 +1）。</summary>
        public void DrawToFull(int handSize = HandSize)
        {
            while (hand.Count < handSize)
                if (Draw() == null) break;
        }

        /// <summary>出牌：从手牌移除并进弃牌堆。成功返回 true。</summary>
        public bool PlayCard(CardData card)
        {
            if (!hand.Remove(card)) return false;
            discardPile.Add(card);
            return true;
        }

        /// <summary>获得一张新卡（战利品）：直接进抽牌堆，下一场战斗重置时洗入。</summary>
        public void AddCard(CardData card)
        {
            drawPile.Add(card);
        }

        /// <summary>重置卡组：手牌 + 弃牌堆全部收回抽牌堆并洗牌（每场战斗前调用）。</summary>
        public void Reset()
        {
            drawPile.AddRange(hand);
            drawPile.AddRange(discardPile);
            hand.Clear();
            discardPile.Clear();
            Shuffle(drawPile);
        }

        /// <summary>从卡组删一张牌（手牌/抽牌堆/弃牌堆里都找）。</summary>
        public bool RemoveCard(CardData card)
        {
            if (hand.Remove(card)) return true;
            if (drawPile.Remove(card)) return true;
            return discardPile.Remove(card);
        }

        static void Shuffle(List<CardData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
