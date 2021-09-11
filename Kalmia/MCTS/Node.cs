using System;

using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    public enum EdgeLabel : byte
    {
        NotLabeled = 0,
        Win,
        Loss,
        Draw,
        Unknown
    }

    public struct Edge     // This struct has some information about child node. It is based on idea that reducing direct access between parent node and child node. 
    {
        public int VisitCount;
        public int VirtualLossSum;
        public float MoveProbability;
        public float Value;
        public float WinCount;
        public BoardPosition NextPos;
        public EdgeLabel Label;
        public float ActionValue { get { return (this.IsLabeled && !this.IsUnknown) ? ConvertEdgeLabelToActionValue(this.Label) : this.WinCount / this.VisitCount; } }
        public bool IsWin { get { return this.Label == EdgeLabel.Win; } }
        public bool IsLoss { get { return this.Label == EdgeLabel.Loss; } }
        public bool IsDraw { get { return this.Label == EdgeLabel.Draw; } }
        public bool IsUnknown { get { return this.Label == EdgeLabel.Unknown; } }
        public bool IsLabeled { get { return this.Label != EdgeLabel.NotLabeled; } }

        public void SetWin()
        {
            this.Label = EdgeLabel.Win;
        }

        public void SetLoss()
        {
            this.Label = EdgeLabel.Loss;
        }

        public void SetDraw()
        {
            this.Label = EdgeLabel.Draw;
        }

        public void SetUnknown()
        {
            this.Label = EdgeLabel.Unknown;
        }
        
        static float ConvertEdgeLabelToActionValue(EdgeLabel label)
        {
            switch (label)
            {
                case EdgeLabel.Win:
                    return 1.0f;

                case EdgeLabel.Loss:
                    return 0.0f;

                case EdgeLabel.Draw:
                    return 0.5f;
            }
            throw new ArgumentException($"{label} cannnot be converted to action value.");
        }
    }

    public class Node
    {
        public int VisitCount = 0;
        public int VirtualLossSum = 0;
        public float WinCount = 0.0f;
        public Node[] ChildNodes;
        public Edge[] Edges;
        public int ChildNum { get { return this.Edges.Length; } }

        public void Expand(BoardPosition[] positions, int moveCount)
        {
            this.Edges = new Edge[moveCount];
            for (var i = 0; i < this.Edges.Length; i++)
                this.Edges[i].NextPos = positions[i];
        }

        public void Expand(BoardPosition[] positions, float[] moveProb, int moveCount)
        {
            this.Edges = new Edge[moveCount];
            for (var i = 0; i < this.Edges.Length; i++)
            {
                this.Edges[i].NextPos = positions[i];
                this.Edges[i].MoveProbability = moveProb[i];
            }
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
