using System.Collections.Generic;

namespace ShuYu
{
    public enum NodeType { Battle, Elite, Camp, Boss }

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

    /// <summary>路线图：节点列表 + 当前位置 + 生成器（杀戮尖塔式分叉路线）。</summary>
    public class RouteMap
    {
        public const int Rows = 4;          // 中间层数（不含起点与 BOSS）
        public const int ColsPerRow = 3;    // 每层节点数

        public List<MapNode> nodes = new List<MapNode>();
        public int currentIndex;

        /// <summary>生成一张路线：起点 → 若干层分叉 → BOSS。</summary>
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
                    float col = (count == 1) ? (r == 0 ? 0.0f : 0.5f) : (float)c / (count - 1);
                    NodeType type = (r == levels - 1) ? NodeType.Boss : PickType(r, c);
                    map.nodes.Add(new MapNode(type, r, col));
                }
            }
            map.nodes[0].reached = true;
            map.currentIndex = 0;

            // 连边：每层每个节点 → 下一层 col 最近的 1~2 个节点（分叉）
            for (int r = 0; r < levels - 1; r++)
            {
                int curCount = (r == 0) ? 1 : ColsPerRow;
                int nextCount = (r + 1 == levels - 1) ? 1 : ColsPerRow;
                int nextStart = levelStart[r + 1];
                for (int c = 0; c < curCount; c++)
                {
                    var node = map.nodes[levelStart[r] + c];
                    if (r == 0)
                    {
                        // 起点连到下一层全部（从原点出发可选所有路）
                        for (int k = nextStart; k < nextStart + nextCount; k++)
                            node.next.Add(k);
                        continue;
                    }
                    int best = nextStart;
                    float bestD = 9f;
                    for (int k = 0; k < nextCount; k++)
                    {
                        float d = Abs(node.col - map.nodes[nextStart + k].col);
                        if (d < bestD) { bestD = d; best = nextStart + k; }
                    }
                    // 连到 closest 及其左右相邻（不重复、不越界），保证分叉且不留死节点
                    for (int k = best - 1; k <= best + 1; k++)
                    {
                        if (k >= nextStart && k < nextStart + nextCount && !node.next.Contains(k))
                            node.next.Add(k);
                    }
                }
            }
            return map;
        }

        static NodeType PickType(int row, int c)
        {
            // 占位（确定性，方便测）：第2层中间出精英，第1/3层中间出篝火，其余战斗
            if (row == 2 && c == 1) return NodeType.Elite;
            if ((row == 1 || row == 3) && c == 1) return NodeType.Camp;
            return NodeType.Battle;
        }

        static float Abs(float v) { return v < 0 ? -v : v; }
    }
}
