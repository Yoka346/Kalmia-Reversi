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

    public struct Edge_old     // This struct has some information about child node. It is based on idea that reducing direct access between parent node and child node. 
    {
        public int VisitCount;
        public int VirtualLossSum;
        public float MoveProbability;
        public float ValueSum;
        public BoardPosition Pos;
        public EdgeLabel Label;
        public float Value { get { return (this.IsLabeled && !this.IsUnknown) ? GetValueFromEdgeLabel(this.Label) : this.ValueSum / this.VisitCount; } }
        public bool IsWin { get { return this.Label == EdgeLabel.Win; } }
        public bool IsLoss { get { return this.Label == EdgeLabel.Loss; } }
        public bool IsDraw { get { return this.Label == EdgeLabel.Draw; } }
        public bool IsUnknown { get { return this.Label == EdgeLabel.Unknown; } }
        public bool IsLabeled { get { return this.Label != EdgeLabel.NotLabeled; } }
        public bool IsProved { get { return this.IsLabeled && !this.IsUnknown; } }

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
        
        static float GetValueFromEdgeLabel(EdgeLabel label)
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
            throw new ArgumentException($"{label} must be Win, Loss or Draw");
        }
    }

    public class Node_old
    {
        public int VisitCount = 0;
        public int VirtualLossSum = 0;
        public float ValueSum = 0.0f;
        public float Value { get { return this.ValueSum / this.VisitCount; } }
        public Node_old[] ChildNodes;
        public Edge_old[] Edges;
        public int ChildNum { get { return this.Edges.Length; } }

        public void Expand(BoardPosition[] positions, int posNum)
        {
            this.Edges = new Edge_old[posNum];
            for (var i = 0; i < this.Edges.Length; i++)
                this.Edges[i].Pos = positions[i];
        }

        public void Expand(BoardPosition[] positions, float[] moveProb, int posNum)
        {
            this.Edges = new Edge_old[posNum];
            for (var i = 0; i < this.Edges.Length; i++)
            {
                this.Edges[i].Pos = positions[i];
                this.Edges[i].MoveProbability = moveProb[i];
            }
        }

        public Node_old CreateChildNode(int idx)
        {
            return this.ChildNodes[idx] = new Node_old();
        }

        public void InitChildNodes()
        {
            this.ChildNodes = new Node_old[this.Edges.Length];
        }
    }
}
