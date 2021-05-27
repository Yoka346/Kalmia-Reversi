using System;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    public struct EdgeToChildNode     // This struct has some information about child node. It is based on idea that reducing direct access between parent node and child node. 
    {
        public int VisitCount;
        public float WinCount;
        public Move Move;
        public bool IsTerminal;
    }

    public class Node
    {
        public int VisitCount = 0;
        public float WinCount = 0.0f;
        public Node[] ChildNodes;
        public EdgeToChildNode[] Edges;
        public bool IsLeaf = false;

        public Node() { }

        public void Expand(Move[] moves, int moveNum)
        {
            this.Edges = new EdgeToChildNode[moveNum];
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
