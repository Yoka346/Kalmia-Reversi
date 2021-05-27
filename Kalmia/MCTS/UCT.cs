using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace Kalmia.MCTS           
{
    public struct MoveEval
    {
        public Move Move { get; }
        public float Rate { get; }
        public float Value { get; }
        public int SimulationCount { get; }

        public MoveEval(Move move, float rate, float value, int simulationCount)
        {
            this.Move = move;
            this.Rate = rate;
            this.Value = value;
            this.SimulationCount = simulationCount;
        }
    }

    public class UCT
    {
        readonly int THREAD_NUM;
        readonly Board[] ROLLOUT_BOARD;
        readonly Move[][] MOVES;
        readonly Xorshift[] RAND;
        int expansionThreshold = 40;
        int virtualLoss = 3;
        Move lastMove;

        public int ExpansionThreshold 
        {
            get { return this.expansionThreshold; }
            set { if (value > 0) this.expansionThreshold = value; else throw new ArgumentOutOfRangeException(); }
        }

        public int VirtualLoss 
        { 
            get { return this.virtualLoss; } 
            set{ if (value >= 0) this.virtualLoss = value; else throw new ArgumentOutOfRangeException(); } 
        }

        Node root;

        public UCT():this(Environment.ProcessorCount) { }

        public UCT(int threadNum)
        {
            this.THREAD_NUM = threadNum;
            this.ROLLOUT_BOARD = (from i in Enumerable.Range(0, this.THREAD_NUM) select new Board(Color.Black, InitialBoardState.Cross)).ToArray();
            this.MOVES = (from len in Enumerable.Repeat(Board.MAX_MOVES_NUM, this.THREAD_NUM) select new Move[len]).ToArray();
            this.RAND = (from i in Enumerable.Range(0, this.THREAD_NUM) select new Xorshift()).ToArray();
        }

        public MoveEval GetRootNodeEvaluation()
        {
            return new MoveEval(this.lastMove, float.NaN, this.root.WinCount / this.root.VisitCount, this.root.VisitCount);
        }

        public IEnumerable<MoveEval> GetChildNodeEvaluations()
        {
            var visitCountSum = this.root.VisitCount;
            foreach(var edge in this.root.Edges)
                yield return new MoveEval(edge.Move, edge.VisitCount / visitCountSum, edge.WinCount / edge.VisitCount, edge.VisitCount);
        }

        public Move Search(Board board, Move lastMove, int count)
        {
            var currentBoard = (from b in Enumerable.Repeat(board, this.THREAD_NUM) select new Board(Color.Black, InitialBoardState.Cross)).ToArray();

            Parallel.For(0, this.THREAD_NUM, (threadID) =>
            {
                var b = currentBoard[threadID];
                for (var i = 0; i < count / this.THREAD_NUM; i++)
                {
                    board.CopyTo(b, false);
                    SearchKernel(this.root, b, threadID);
                }
            });

            for (var i = 0; i < count % this.THREAD_NUM; i++)
            {
                board.CopyTo(board, false);
                SearchKernel(this.root, currentBoard[0], 0);
            }
            return SelectBestMove();
        }

        public void SetRoot(Move move)      // Looks one move ahead, and if a child node which has same move as specified one is found, sets it to root,
                                            // otherwise, sets new Node object to root.
        {
            this.lastMove = move;
            if (this.root != null && this.root.Edges != null)
                for (var i = 0; i < this.root.Edges.Length; i++)
                {
                    if (move == this.root.Edges[i].Move && this.root.ChildNodes != null && this.root.ChildNodes[i] != null)
                    {
                        this.root.CreateChildNode(i);
                        this.root = this.root.ChildNodes[i];
                        return;
                    }
                }
            this.root = new Node();
        }

        Move SelectBestMove()       // selects one node which has the biggest visit count.
        {
            var edges = this.root.Edges;
            var bestEdge = edges[0];
            for(var i = 1; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge.VisitCount > bestEdge.VisitCount)
                    bestEdge = edge;
            }
            return bestEdge.Move;
        }

        float SearchKernel(Node currentNode, Board currentBoard, int threadID)     // go down to leaf node and back up to root node with updating score
        {
            var moves = this.MOVES[threadID];
            int childNodeIdx;
            var currentNodeTurn = currentBoard.Turn;
            float score;

            bool lockTaken = false;
            try
            {
                Monitor.Enter(currentNode, ref lockTaken);
                if (currentNode.Edges == null)      // not expanded
                {
                    var moveNum = currentBoard.GetNextMoves(moves);
                    currentNode.Expand(moves, moveNum);
                }

                childNodeIdx = SelectChildNode(currentNode);
                AddVirtualLoss(currentNode, childNodeIdx);
                var edge = currentNode.Edges[childNodeIdx];
                currentBoard.Update(edge.Move);

                if (edge.VisitCount >= this.expansionThreshold && !edge.IsTerminal)     // current node is not a leaf node or terminal.
                {
                    if (currentNode.ChildNodes == null)
                        currentNode.InitChildNodes();
                    if (currentNode.ChildNodes[childNodeIdx] == null)
                        currentNode.CreateChildNode(childNodeIdx);
                    Monitor.Exit(currentNode);
                    lockTaken = false;
                    score = SearchKernel(currentNode.ChildNodes[childNodeIdx], currentBoard, threadID);
                }
                else    // current node is a leaf node
                {
                    Monitor.Exit(currentNode);
                    lockTaken = false;
                    score = Rollout(currentBoard, currentNodeTurn, out int count, threadID);
                    if (count == 0 && !edge.IsTerminal)
                        currentNode.Edges[childNodeIdx].IsTerminal = true;
                }
            }
            catch
            {
                if (lockTaken)
                    Monitor.Exit(currentNode);
                throw;
            }

            UpdateResult(currentNode, childNodeIdx, score);
            return 1.0f - score;
        }

        void AddVirtualLoss(Node node, int childNodeIdx)
        {
            // I think it does not need to use Interlocked.Add method because node has been already locked before entered this method,
            // however value mismatch occured in node.VisitCount and node.Edges[childNodeIdx].VisitCount,
            // so I use Interlocked.Add method and then value mismatch was disappered. I do not know why.
            Interlocked.Add(ref node.VisitCount, this.virtualLoss);
            Interlocked.Add(ref node.Edges[childNodeIdx].VisitCount, this.virtualLoss);
            //node.VisitCount += this.virtualLoss;
            //node.Edges[childNodeIdx].VisitCount += this.virtualLoss;
        }

        int SelectChildNode(Node parentNode)        // Selects child node by UCB score. 
        {
            var maxIdx = 0;
            var maxUCB = float.NegativeInfinity;
            var sum = parentNode.VisitCount + parentNode.Edges.Length;        
            var twoLogSum = 2.0f * FastMath.Log(sum);
            for (var i = 0; i < parentNode.Edges.Length; i++)
            {
                // To avoid zero division or log(0), calculates UCB score assuming lost the game at least one time. 
                var edge = parentNode.Edges[i];
                var n = edge.VisitCount + 1;
                var ucb = parentNode.Edges[i].WinCount / n + MathF.Sqrt(twoLogSum / n);    
                if(ucb > maxUCB)
                {
                    maxUCB = ucb;
                    maxIdx = i;
                }
            }
            return maxIdx;
        }

        void UpdateResult(Node node, int childNodeIdx, float score)
        {
            Interlocked.Add(ref node.Edges[childNodeIdx].VisitCount, 1 - this.virtualLoss);
            AtomicFloatAdd(ref node.Edges[childNodeIdx].WinCount, score);
            Interlocked.Add(ref node.VisitCount, 1 - this.virtualLoss);
            AtomicFloatAdd(ref node.WinCount, score);
        }

        float Rollout(Board currentBoard, Color color, out int count, int threadID)
        {
            uint moveNum;
            count = 0;
            var board = this.ROLLOUT_BOARD[threadID];
            currentBoard.CopyTo(board, false);
            var rand = this.RAND[threadID];
            while ((moveNum = (uint)board.GetNextMovesNum()) != 0)
            {
                board.Update(board.GetNextMove((int)rand.Next(moveNum)));
                count++;
            }

            switch (board.GetGameResult(color))
            {
                case GameResult.Win:
                    return 1.0f;

                case GameResult.Lose:
                    return 0.0f;

                default:
                    return 0.5f;
            }
        }

        static void AtomicFloatAdd(ref float value, float arg) 
        {
            float expected;
            do
                expected = value;
            while(expected != Interlocked.CompareExchange(ref value, expected + arg, expected));
        }
    }
}
