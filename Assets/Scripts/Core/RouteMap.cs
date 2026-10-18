using System;
using System.Collections.Generic;

namespace ShuYu
{
    public enum NodeType { Start, Battle, Elite, Camp, Treasure, Mystery, Shop, Boss }

    /// <summary>路线图节点。</summary>
    [Serializable]
    public class MapNode
    {
        public NodeType type;
        public int row;                          // 层：0=起点，越大越靠上
        public float col;                        // 列：0~1 归一化横向位置
        public List<int> next = new List<int>(); // 可前往的下一层节点索引
        public bool reached;                     // 是否已到达

        public MapNode(NodeType type, int row, float col)
        {
            this.type = type;
            this.row = row;
            this.col = col;
        }
    }

    /// <summary>路线图：节点列表 + 当前位置 + 随机生成器（杀戮尖塔式分叉路线）。</summary>
    [Serializable]
    public class RouteMap
    {
        public const int Rows = 5;          // 中间层数（不含起点与 BOSS）
        public const int ColsPerRow = 3;    // 每层节点数
        public const int CampRow = 3;       // 火堆层：只放 1 个火堆节点，上一层所有节点汇聚到它，过完再重新分岔

        static readonly System.Random _rng = new System.Random();

        public List<MapNode> nodes = new List<MapNode>();
        public int currentIndex;

        /// <summary>生成一张随机分叉路线：起点 → 若干层 → BOSS（类型/位置/连边都随机）。</summary>
        public static RouteMap Generate()
        {
            var map = new RouteMap();
            int levels = Rows + 2;   // 0=起点，1..Rows=中间，Rows+1=BOSS

            var levelStart = new int[levels];
            for (int r = 0; r < levels; r++)
            {
                levelStart[r] = map.nodes.Count;
                int count = CountOf(r, levels);
                for (int c = 0; c < count; c++)
                {
                    float col;
                    if (count == 1)
                        col = (r == 0) ? 0f : 0.5f;
                    else
                    {
                        col = (float)c / (count - 1) + (float)(_rng.NextDouble() * 0.12 - 0.06); // 轻微横向抖动，保持列对齐、不显乱
                        if (col < 0f) col = 0f;
                        if (col > 1f) col = 1f;
                    }
                    NodeType type = PickType(r, levels);
                    map.nodes.Add(new MapNode(type, r, col));
                }
            }
            map.nodes[0].reached = true;
            map.currentIndex = 0;

            Connect(map, levelStart, levels);
            return map;
        }

        /// <summary>
        /// 第 r 层的节点数。起点与 BOSS 各 1 个；火堆层也只放 1 个（全路汇聚的唯一火堆）；其余层 ColsPerRow 个。
        /// 生成与连边都走这一个函数，避免两处各写一套「哪层是几个」的判断而漂移。
        /// </summary>
        static int CountOf(int r, int levels)
        {
            if (r == 0 || r == levels - 1) return 1;
            if (r == CampRow) return 1;     // 唯一火堆
            return ColsPerRow;
        }

        static NodeType PickType(int r, int levels)
        {
            if (r == 0) return NodeType.Start;
            if (r == levels - 1) return NodeType.Boss;
            if (r == CampRow) return NodeType.Camp;   // 整层火堆（保证每条路必过）
            if (r == 1) return NodeType.Battle;       // 开局第一层必是战斗

            bool allowElite = r > CampRow;            // 精英不能太靠前：火堆之后才出现
            double rnd = _rng.NextDouble();
            if (allowElite && rnd < 0.15) return NodeType.Elite;
            if (rnd < 0.62) return NodeType.Battle;
            if (rnd < 0.77) return NodeType.Shop;      // 商店
            if (rnd < 0.87) return NodeType.Treasure;
            return NodeType.Mystery;
        }

        static void Connect(RouteMap map, int[] levelStart, int levels)
        {
            for (int r = 0; r < levels - 1; r++)
            {
                int curStart = levelStart[r];
                int curCount = CountOf(r, levels);
                int nextStart = levelStart[r + 1];
                int nextCount = CountOf(r + 1, levels);

                // 单节点层（起点 / 唯一火堆）：直接连下一层全部，把岔路选择权完整交给玩家。
                // 反过来当 nextCount == 1 时（上一层汇入唯一火堆），下面的「连最近的」会让三个节点各连它一条，自然收拢。
                if (curCount == 1)
                {
                    for (int k = nextStart; k < nextStart + nextCount; k++)
                        map.nodes[curStart].next.Add(k);
                    continue;
                }

                // 结构化分叉：连「最近的」下一层节点（保证无死路），
                // 若「第二近的」够近（<0.55）再连它，最多 2 条，不乱
                for (int c = 0; c < curCount; c++)
                {
                    var node = map.nodes[curStart + c];
                    int best = nextStart, second = -1;
                    float bestD = 9f, secondD = 9f;
                    for (int k = 0; k < nextCount; k++)
                    {
                        float d = Abs(node.col - map.nodes[nextStart + k].col);
                        if (d < bestD) { secondD = bestD; second = best; bestD = d; best = nextStart + k; }
                        else if (d < secondD) { secondD = d; second = nextStart + k; }
                    }
                    node.next.Add(best);
                    if (second >= 0 && secondD < 0.55f) node.next.Add(second);
                }

                // 兜底：保证每个下一层节点至少一条入边（防抖动导致孤立）
                for (int k = 0; k < nextCount; k++)
                {
                    bool hasIn = false;
                    for (int c = 0; c < curCount; c++)
                        if (map.nodes[curStart + c].next.Contains(nextStart + k)) { hasIn = true; break; }
                    if (!hasIn)
                        map.nodes[curStart + _rng.Next(curCount)].next.Add(nextStart + k);
                }
            }
        }

        static float Abs(float v) { return v < 0 ? -v : v; }
    }
}
