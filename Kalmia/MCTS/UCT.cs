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
        Move? lastMove;
        Node root;
        EdgeMarker rootMark;
        int npsCounter = 0;
        int searchStartTime;
        int searchiEndTime;

        public float Nps { get { return (float)this.npsCounter / this.SearchEllapsedTime; } }

        public int Depth { get; private set; } = 0;
        public int NodeCount { get; private set; } = 0;
        public bool IsSearching { get; private set; }
        public int SearchEllapsedTime { get { return (this.IsSearching) ? Environment.TickCount - this.searchStartTime : this.searchiEndTime - this.searchStartTime; } }

        public int ExpansionThreshold
        {
            get { return this.expansionThreshold; }
            set { if (value > 0) this.expansionThreshold = value; else throw new ArgumentOutOfRangeException(); }
        }

        public int VirtualLoss
        {
            get { return this.virtualLoss; }
            set { if (value >= 0) this.virtualLoss = value; else throw new ArgumentOutOfRangeException(); }
        }

        public UCT() : this(Environment.ProcessorCount) { }

        public UCT(int threadNum)
        {
            this.THREAD_NUM = threadNum;
            this.ROLLOUT_BOARD = (from i in Enumerable.Range(0, this.THREAD_NUM) select new Board(Color.Black, InitialBoardState.Cross)).ToArray();
            this.MOVES = (from len in Enumerable.Repeat(Board.MAX_MOVES_NUM, this.THREAD_NUM) select new Move[len]).ToArray();
            this.RAND = (from i in Enumerable.Range(0, this.THREAD_NUM) select new Xorshift()).ToArray();
        }

        public MoveEval GetRootNodeEvaluation()
        {
            if (this.root == null)
                return new MoveEval();

            var value = (this.rootMark == EdgeMarker.Unknown) ? this.root.WinCount / this.root.VisitCount : GetScoreFromEdgeMarker(this.rootMark);
            return new MoveEval(this.lastMove.Value, float.NaN, value, this.root.VisitCount);
        }

        public IEnumerable<MoveEval> GetChildNodeEvaluations()
        {
            if (this.root == null)
                yield break;

            var visitCountSum = this.root.VisitCount;
            foreach (var edge in this.root.Edges)
            {
                var value = (edge.Mark == EdgeMarker.Unknown) ? this.root.WinCount / this.root.VisitCount : GetScoreFromEdgeMarker(edge.Mark);
                yield return new MoveEval(edge.Move, (float)edge.VisitCount / visitCountSum, value, edge.VisitCount);
            }
        }

        public IEnumerable<MoveEval> GetBestPath()
        {
            if (this.root == null)
                yield break;

            var node = this.root;
            while (node != null && node.Edges != null)
            {
                var idx = SelectBestChildNode(node);
                var edge = node.Edges[idx];
                float value;
                if (!edge.IsMarked || edge.IsUnknown)
                    value = edge.WinCount / edge.VisitCount;
                else if (edge.IsWin)
                    value = 1.0f;
                else if (edge.IsLoss)
                    value = 0.0f;
                else
                    value = 0.5f;
                yield return new MoveEval(edge.Move, 0.0f, value, edge.VisitCount);

                if (node.ChildNodes != null && node.ChildNodes[idx] != null)
                    node = node.ChildNodes[idx];
                else
                    break;
            }
        } 

        public void SetRoot(Move move)      // Looks one move ahead, and if a child node which has same move as specified one is found, sets it to root,
                                            // otherwise sets new Node object to root.
        {
            if (this.lastMove.HasValue && this.lastMove == move)
                return;

            this.lastMove = move;
            if (this.root != null && this.root.Edges != null)
                for (var i = 0; i < this.root.Edges.Length; i++)
                {
                    if (move == this.root.Edges[i].Move && this.root.ChildNodes != null && this.root.ChildNodes[i] != null)
                    {
                        this.NodeCount -= this.root.ChildNum - 1;
                        DecrementNodeCount(this.root.ChildNodes[i]);
                        this.rootMark = this.root.Edges[i].Mark;
                        this.root = this.root.ChildNodes[i];
                        return;
                    }
                }
            this.rootMark = EdgeMarker.Unknown;
            this.root = new Node();
            this.NodeCount = 0;
        }

        public void Clear()
        {
            this.lastMove = null;
            this.root = null;
        }

        public async Task<Move> SearchAsync(Board board, int count, int timeLimit, CancellationToken ct)
        {
            return await Task.Run(() => Search(board, count, timeLimit, ct));
        }

        public Move Search(Board board, int count, int timeLimit = int.MaxValue) 
        {
            return Search(board, count, timeLimit, new CancellationToken(false));
        }

        public Move Search(Board board, int count, int timeLimit, CancellationToken ct)
        {
            var currentBoard = (from b in Enumerable.Repeat(board, this.THREAD_NUM) select new Board(Color.Black, InitialBoardState.Cross)).ToArray();
            var dummyEdge = new Edge();

            this.searchStartTime = Environment.TickCount;
            this.IsSearching = true;
            this.npsCounter = 0;
            this.Depth = 0;
            Parallel.For(0, this.THREAD_NUM, (threadID) =>
            {
                var b = currentBoard[threadID];
                for (var i = 0; !stop() && i < count / this.THREAD_NUM; i++)
                {
                    board.CopyTo(b, false);
                    SearchKernel(this.root, ref dummyEdge, b, 0, threadID);
                }
            });

            for (var i = 0; !stop() && i < count % this.THREAD_NUM; i++)
            {
                board.CopyTo(board, false);
                SearchKernel(this.root, ref dummyEdge, currentBoard[0], 0, 0);
            }
            this.IsSearching = false;
            this.searchiEndTime = Environment.TickCount;
            return SelectBestMove();

            bool stop()
            {
                return ct.IsCancellationRequested || Environment.TickCount - this.searchStartTime >= timeLimit;
            }
        }

        void DecrementNodeCount(Node node)
        {
            if (node.ChildNodes == null)
                return;
            foreach (var childNode in node.ChildNodes)
                if (childNode != null)
                {
                    this.NodeCount--;
                    DecrementNodeCount(childNode);
                }
        }

        Move SelectBestMove()       // selects one node which has the largest visit count.
        {
            var edges = this.root.Edges;
            var bestEdge = edges[0];
            var lossCount = 0;
            var drawCount = 0;
            for (var i = 1; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (!edge.IsUnknown)
                    if (edge.IsWin)
                    {
                        this.rootMark = EdgeMarker.Win;
                        return edge.Move;
                    }
                    else if (edge.IsLoss)
                    {
                        lossCount++;
                        continue;
                    }
                    else
                        drawCount++;

                if (edge.VisitCount > bestEdge.VisitCount)
                    bestEdge = edge;
            }

            if(lossCount == this.root.ChildNum)
                this.rootMark = EdgeMarker.Loss;
            else if(lossCount + drawCount == this.root.ChildNum)
                this.rootMark = EdgeMarker.Draw;
            return bestEdge.Move;
        }

        float SearchKernel(Node currentNode, ref Edge edgeToCurrentNode, Board currentBoard, int depth, int threadID)     // goes down to leaf node and back up to root node with updating score
        {
            var moves = this.MOVES[threadID];
            int childNodeIdx;
            var currentNodeTurn = currentBoard.Turn;
            float score;

            if (depth > this.Depth)
                this.Depth++;

            var lockTaken = false;
            try
            {
                Monitor.Enter(currentNode, ref lockTaken);
                if (currentNode.Edges == null)      // not expanded
                {
                    var moveNum = currentBoard.GetNextMoves(moves);
                    currentNode.Expand(moves, moveNum);
                }

                childNodeIdx = SelectChildNode(currentNode, ref edgeToCurrentNode);
                AddVirtualLoss(currentNode, childNodeIdx); 
                var edges = currentNode.Edges;
                var edge = edges[childNodeIdx];
                currentBoard.Update(edge.Move);

                if (!edge.IsMarked) 
                {
                    MarkEdge(ref edges[childNodeIdx], currentBoard, currentNodeTurn);
                    edge = edges[childNodeIdx];
                }
                
                if (edge.IsUnknown)
                {
                    if (edge.VisitCount >= this.expansionThreshold)     // current node is not a leaf node 
                    {
                        if (currentNode.ChildNodes == null)
                            currentNode.InitChildNodes();
                        if (currentNode.ChildNodes[childNodeIdx] == null)
                        {
                            currentNode.CreateChildNode(childNodeIdx);
                            this.NodeCount++;
                            this.npsCounter++;
                        }
                        Monitor.Exit(currentNode);
                        lockTaken = false;
                        score = SearchKernel(currentNode.ChildNodes[childNodeIdx], ref edges[childNodeIdx], currentBoard, ++depth, threadID);
                    }
                    else    // current node is a leaf node
                    {
                        Monitor.Exit(currentNode);
                        lockTaken = false;
                        score = Rollout(currentBoard, currentNodeTurn, out int count, threadID);
                    }
                }
                else
                {
                    Monitor.Exit(currentNode);
                    score = GetScoreFromEdgeMarker(currentNode.Edges[childNodeIdx].Mark);
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
            AtomicOperations.Add(ref node.VisitCount, this.virtualLoss);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VisitCount, this.virtualLoss);
        }

        void MarkEdge(ref Edge edge, Board currentBoard, Color color)
        {
            switch (currentBoard.GetGameResult(color))
            {
                case GameResult.NotOver:
                    edge.SetUnknown();
                    return;

                case GameResult.Win:
                    edge.SetWin();
                    return;

                case GameResult.Loss:
                    edge.SetLoss();
                    return;

                case GameResult.Draw:
                    edge.SetDraw();
                    return;
            }
        }

        int SelectChildNode(Node parentNode, ref Edge edgeToParentNode)        // Selects child node by UCB score. 
        {
            var childNum = parentNode.ChildNum;
            var maxIdx = 0;
            var maxUCB = float.NegativeInfinity;
            var sum = parentNode.VisitCount + parentNode.Edges.Length;
            var twoLogSum = 2.0f * FastMath.Log(sum);

            var lossCount = 0;
            var drawCount = 0;
            var drawEdgeIdx = 0;
            for (var i = 0; i < childNum; i++)
            {
                if (parentNode.Edges[i].IsWin)
                {
                    edgeToParentNode.SetLoss();
                    return i;       // definitely select win
                }
                else if (parentNode.Edges[i].IsLoss)
                {
                    lossCount++;        // do not select loss
                    continue;
                }
                else if (parentNode.Edges[i].IsDraw)
                {
                    drawCount++;
                    drawEdgeIdx = i;
                }

                // To avoid zero division or log(0), calculates UCB score assuming lost the game at least one time. 
                var edge = parentNode.Edges[i];
                var n = edge.VisitCount + 1;
                var ucb = parentNode.Edges[i].WinCount / n + MathF.Sqrt(twoLogSum / n);
                if (ucb > maxUCB)
                {
                    maxUCB = ucb;
                    maxIdx = i;
                }
            }

            if (lossCount == childNum)
                edgeToParentNode.SetWin();
            else if (lossCount + drawCount == childNum)
            {
                edgeToParentNode.SetDraw();
                return drawEdgeIdx;     // when other nodes are lose, it is better to select draw.
            }
            return maxIdx;
        }

        void UpdateResult(Node node, int childNodeIdx, float score)
        {
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VisitCount, 1 - this.virtualLoss);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].WinCount, score);
            AtomicOperations.Add(ref node.VisitCount, 1 - this.virtualLoss);
            AtomicOperations.Add(ref node.WinCount, score);
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

                case GameResult.Loss:
                    return 0.0f;

                default:
                    return 0.5f;
            }
        }

        float GetScoreFromEdgeMarker(EdgeMarker mark)
        {
            switch (mark)
            {
                case EdgeMarker.Win:
                    return 1.0f;

                case EdgeMarker.Loss:
                    return 0.0f;

                case EdgeMarker.Draw:
                    return 0.5f;

                default:        // unreachable.
                    return float.NaN;
            }
        }

        static int SelectBestChildNode(Node node)   // best node means the node which has the largest visit count
        {
            var edges = node.Edges;
            var bestEdgeIdx = 0;
            var lossCount = 0;
            var drawCount = 0;
            for (var i = 1; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (!edge.IsUnknown)
                    if (edge.IsWin)
                        return i;
                    else if (edge.IsLoss)
                    {
                        lossCount++;
                        continue;
                    }
                    else
                        drawCount++;

                if (edge.VisitCount > edges[bestEdgeIdx].VisitCount)
                    bestEdgeIdx = i;
            }
            return bestEdgeIdx;
        }
    }
}
