using System;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    public enum EdgeMarker : byte
    {
        NotMarked = 0,
        Win,
        Loss,
        Draw,
        Unknown = 0
    }

    public struct Edge     // This struct has some information about child node. It is based on idea that reducing direct access between parent node and child node. 
    {
        public int VisitCount;
        public float WinCount;
        public Move Move;
        public EdgeMarker Mark;
        public bool IsWin { get { return this.Mark == EdgeMarker.Win; } }
        public bool IsLoss { get { return this.Mark == EdgeMarker.Loss; } }
        public bool IsDraw { get { return this.Mark == EdgeMarker.Draw; } }
        public bool IsUnknown { get { return this.Mark == EdgeMarker.Unknown; } }
        public bool IsMarked { get { return this.Mark != EdgeMarker.NotMarked; } }

        public void SetWin()
        {
            this.Mark = EdgeMarker.Win;
        }

        public void SetLoss()
        {
            this.Mark = EdgeMarker.Loss;
        }

        public void SetDraw()
        {
            this.Mark = EdgeMarker.Draw;
        }

        public void SetUnknown()
        {
            this.Mark = EdgeMarker.Unknown;
        }
    }

    public class Node
    {
        public int VisitCount = 0;
        public float WinCount = 0.0f;
        public Node[] ChildNodes;
        public Edge[] Edges;
        public int ChildNum { get { return this.Edges.Length; } }

        public void Expand(Move[] moves, int moveNum)
        {
            this.Edges = new Edge[moveNum];
            for (var i = 0; i < this.Edges.Length; i++)
                this.Edges[i].Move = moves[i];
        }

        public void CreateChildNode(int idx)
        {
            this.ChildNodes[idx] = new Node();
        }

        public void InitChildNodes()
        {
            this.ChildNodes = new Node[this.Edges.Length];
        }
    }
}
