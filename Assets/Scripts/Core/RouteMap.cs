using System.Collections.Generic;

namespace ShuYu
{
    public enum NodeType { Start, Battle, Elite, Camp, Treasure, Mystery, Boss }

    /// <summary>路线图节点。</summary>
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
    public class RouteMap
    {
        public const int Rows = 4;          // 中间层数（不含起点与 BOSS）
        public const int ColsPerRow = 3;    // 每层节点数
        public const int CampRow = 2;       // 这一整层全是火堆，保证每条路必过至少一个火堆

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
                int count = (r == 0 || r == levels - 1) ? 1 : ColsPerRow;
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

        static NodeType PickType(int r, int levels)
        {
            if (r == 0) return NodeType.Start;
            if (r == levels - 1) return NodeType.Boss;
            if (r == CampRow) return NodeType.Camp;   // 整层火堆（保证每条路必过）

            double rnd = _rng.NextDouble();
            if (rnd < 0.55) return NodeType.Battle;    // 55% 战斗
            if (rnd < 0.70) return NodeType.Elite;     // 15% 精英
            if (rnd < 0.85) return NodeType.Treasure;  // 15% 宝箱
            return NodeType.Mystery;                   // 15% 问号
        }

        static void Connect(RouteMap map, int[] levelStart, int levels)
        {
            for (int r = 0; r < levels - 1; r++)
            {
                int curStart = levelStart[r];
                int curCount = (r == 0) ? 1 : ColsPerRow;
                int nextStart = levelStart[r + 1];
                int nextCount = (r + 1 == levels - 1) ? 1 : ColsPerRow;

                // 起点连到下一层全部（从原点可选所有路）
                if (r == 0)
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
