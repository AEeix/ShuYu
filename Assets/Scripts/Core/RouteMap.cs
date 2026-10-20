using System;
using System.Collections.Generic;

namespace ShuYu
{
    public enum NodeType { Start, Battle, Elite, Camp, Treasure, Mystery, Shop, Boss }

    /// <summary>网格地图节点：位于网格交汇处，8 方向邻接可移动。</summary>
    [Serializable]
    public class MapNode
    {
        public NodeType type;
        public int col;        // 网格列
        public int row;        // 网格行
        public bool reached;   // 是否已访问/清空

        public MapNode(NodeType type, int col, int row)
        {
            this.type = type;
            this.col = col;
            this.row = row;
        }
    }

    /// <summary>
    /// 网格路线图（黑夜君临式）：整屏网格铺满，节点在交汇处，随机边缘起点，中心 BOSS。
    /// 每次移动一格（8 方向），第 5 次移动起安全区（正方形）向中心收缩，区外掉血。
    /// </summary>
    [Serializable]
    public class RouteMap
    {
        public const int GridCols = 8;           // 网格列数
        public const int GridRows = 6;           // 网格行数
        public const float InitialRadius = 4f;   // 初始安全区半边长（正方形，覆盖全图）
        public const float ShrinkPerMove = 1f;   // 每步收缩量
        public const int ShrinkStartMoves = 4;   // 第 5 次移动（movesTaken=5）起开始缩圈
        public const int ShrinkDamage = 5;       // 圈外每回合掉血

        static readonly System.Random _rng = new System.Random();

        public List<MapNode> nodes = new List<MapNode>();
        public int currentIndex;   // 当前位置
        public int movesTaken;     // 已移动次数（缩圈依据）
        public int bossIndex;      // BOSS 节点索引（中心）

        /// <summary>生成一张网格地图：铺满节点、随机边缘起点、中心 BOSS。</summary>
        public static RouteMap Generate()
        {
            var map = new RouteMap();
            for (int r = 0; r < GridRows; r++)
                for (int c = 0; c < GridCols; c++)
                    map.nodes.Add(new MapNode(map.PickType(c, r), c, r));

            // BOSS 在中心
            map.bossIndex = map.Index(GridCols / 2, GridRows / 2);
            map.nodes[map.bossIndex].type = NodeType.Boss;

            // 随机边缘起点（不落在 BOSS 上）
            map.currentIndex = map.RandomEdgeIndex();
            map.nodes[map.currentIndex].type = NodeType.Start;
            map.nodes[map.currentIndex].reached = true;
            return map;
        }

        /// <summary>网格坐标 → 节点索引。</summary>
        public int Index(int col, int row) { return row * GridCols + col; }

        /// <summary>中段随机类型：战斗 35%、火堆 12%、商店 8%、宝箱 12%、问号 23%、精英 10%。</summary>
        NodeType PickType(int col, int row)
        {
            double rnd = _rng.NextDouble();
            if (rnd < 0.35) return NodeType.Battle;
            if (rnd < 0.47) return NodeType.Camp;
            if (rnd < 0.55) return NodeType.Shop;
            if (rnd < 0.67) return NodeType.Treasure;
            if (rnd < 0.90) return NodeType.Mystery;
            return NodeType.Elite;
        }

        /// <summary>随机挑一个边缘节点（四边）。</summary>
        int RandomEdgeIndex()
        {
            var edges = new List<int>();
            for (int r = 0; r < GridRows; r++)
                for (int c = 0; c < GridCols; c++)
                    if (c == 0 || c == GridCols - 1 || r == 0 || r == GridRows - 1)
                        edges.Add(Index(c, r));
            return edges[_rng.Next(edges.Count)];
        }

        /// <summary>8 方向邻居（上下左右 + 对角线），返回节点索引。</summary>
        public List<int> Neighbors(int index)
        {
            var node = nodes[index];
            var result = new List<int>();
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = node.row + dr, nc = node.col + dc;
                    if (nr >= 0 && nr < GridRows && nc >= 0 && nc < GridCols)
                        result.Add(Index(nc, nr));
                }
            return result;
        }

        /// <summary>当前安全区半边长（正方形）：第 5 次移动起每步收缩 1。</summary>
        public float ShrinkRadius()
        {
            int step = Math.Max(0, movesTaken - ShrinkStartMoves);
            return Math.Max(0f, InitialRadius - step * ShrinkPerMove);
        }

        /// <summary>节点是否在安全区（正方形）内：到 BOSS 的行、列距离各自 ≤ 半边长。</summary>
        public bool IsSafe(int index)
        {
            var b = nodes[bossIndex];
            var n = nodes[index];
            float dc = Math.Abs(n.col - b.col), dr = Math.Abs(n.row - b.row);
            return Math.Max(dc, dr) <= ShrinkRadius();
        }
    }
}
