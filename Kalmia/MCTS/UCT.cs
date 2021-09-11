//#define SINGLE_THREAD

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    public struct PositionEval
    {
        public BoardPosition Position { get; }
        public float MoveProbability { get; }
        public float Value { get; }
        public float ActionValue { get; }
        public int SimulationCount { get; }

        public PositionEval(Edge edge, float moveProb)
        {
            this.Position = edge.NextPos;
            this.MoveProbability = moveProb;
            this.Value = edge.Value;
            this.ActionValue = edge.ActionValue;
            this.SimulationCount = edge.VisitCount; 
        }

        public PositionEval(BoardPosition pos, float moveProb, float value, float actionValue, int simCount)
        {
            this.Position = pos;
            this.MoveProbability = moveProb;
            this.Value = value;
            this.ActionValue = actionValue;
            this.SimulationCount = simCount;
        }

        public static IEnumerable<PositionEval> EnumerateMoveEvals(Edge[] edges)
        {
            var visitCountSum = edges.Sum(e => e.VisitCount);
            for (var i = 0; i < edges.Length; i++)
                yield return new PositionEval(edges[i], (float)edges[i].VisitCount / visitCountSum);
        }
    }

    public class UCT
    {
        const float DEFAULT_UCB_FACTOR = 1.0f;

        readonly int THREAD_NUM;
        readonly FastBoard[] ROLLOUT_BOARD;
        readonly BoardPosition[][] POSITIONS;
        readonly Xorshift[] RAND;
        readonly float UCB_FACTOR;

        int expansionThreshold = 1;
        int virtualLoss = 3;
        float moveProbTemperature = 0.0f;
        Edge edgeToRoot;
        Node root;
        int nodeCount = 0;
        int npsCounter = 0;
        int searchStartTime;
        int searchEndTime;

        public Func<FastBoard, int, float> ValueFunctionCallback { get; set; }

        public float Nps { get { return this.npsCounter / (this.SearchEllapsedTime / 1000.0f); } }

        public int Depth { get; private set; } = 0;
        public int NodeCount { get { return this.nodeCount; } private set { this.nodeCount = value; } }
        public bool IsSearching { get; private set; }
        public int SearchEllapsedTime { get { return (this.IsSearching) ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }

        public int ExpansionThreshold
        {
            get { return this.expansionThreshold; }
            set { if (value > 0) this.expansionThreshold = value; else throw new ArgumentOutOfRangeException("expansion threshold must be positive."); }
        }

        public int VirtualLoss
        {
            get { return this.virtualLoss; }
            set { if (value >= 0) this.virtualLoss = value; else throw new ArgumentOutOfRangeException("virtual loss must be positive or zero."); }
        }

        public float MoveProbabilityTemperature 
        {
            get { return this.moveProbTemperature; }
            set { if (value < 0.0f) throw new ArgumentOutOfRangeException("softmax temperature must be positive or zero."); else this.moveProbTemperature = value; }
        }

        public UCT() : this(DEFAULT_UCB_FACTOR) { }

        public UCT(float ucbFactor) : this(ucbFactor, Environment.ProcessorCount) { }

        public UCT(float ucbFactor, int threadNum)
        {
            this.UCB_FACTOR = ucbFactor;
            this.THREAD_NUM = threadNum;
            this.ROLLOUT_BOARD = (from i in Enumerable.Range(0, threadNum) select new FastBoard()).ToArray();
            this.POSITIONS = (from _ in Enumerable.Range(0, threadNum) select new BoardPosition[Board.MAX_MOVE_COUNT]).ToArray();
            this.RAND = (from i in Enumerable.Range(0, threadNum) select new Xorshift()).ToArray();
            this.edgeToRoot.NextPos = BoardPosition.Null;
            this.root = new Node();
            this.nodeCount = 1;
        }

        public PositionEval GetRootNodeEvaluation()
        {
            if (this.root == null)
                return new PositionEval();
            return new PositionEval(this.edgeToRoot.NextPos, 1.0f, 1.0f - this.edgeToRoot.Value, this.root.WinCount / this.root.VisitCount, this.root.VisitCount);
        }

        public IEnumerable<PositionEval> GetChildNodeEvaluations()
        {
            if (this.root == null || this.root.Edges == null)
                yield break;
            foreach (var n in PositionEval.EnumerateMoveEvals(this.root.Edges))
                yield return n;
        }

        public IEnumerable<PositionEval> GetBestPath(int childIdx)
        {
            if (this.root == null || this.root.ChildNodes == null || this.root.ChildNodes[childIdx] == null)
                yield break;

            var node = this.root.ChildNodes[childIdx];
            while (node != null && node.Edges != null)
            {
                var idx = SelectBestChildNode(node);
                var edge = node.Edges[idx];
                var vistCountSum = node.Edges.Sum(e => e.VisitCount);
                yield return new PositionEval(edge, edge.VisitCount / vistCountSum);

                if (node.ChildNodes != null && node.ChildNodes[idx] != null)
                    node = node.ChildNodes[idx];
                else
                    break;
            }
        }

        // Looks one move ahead, and if a child node which has same position as specified one is found, sets it to root,
        // otherwise sets new Node object to root.
        public void SetRoot(BoardPosition pos)      
        {
            if (pos != BoardPosition.Null && this.edgeToRoot.NextPos == pos)
                return;

            this.edgeToRoot.NextPos = pos;
            if (this.root != null && this.root.Edges != null)
                for (var i = 0; i < this.root.Edges.Length; i++)
                {
                    if (pos == this.root.Edges[i].NextPos && this.root.ChildNodes != null && this.root.ChildNodes[i] != null)
                    {
                        var prevRoot = this.root;
                        this.root = prevRoot.ChildNodes[i];
                        this.edgeToRoot = prevRoot.Edges[i];
                        prevRoot.ChildNodes[i] = null;
                        DecrementNodeCount(prevRoot);
                        return;
                    }
                }
            this.edgeToRoot = new Edge();
            this.edgeToRoot.NextPos = pos;
            this.root = new Node();
            this.NodeCount = 1;
        }

        public void Clear()
        {
            this.edgeToRoot = new Edge();
            this.edgeToRoot.NextPos = BoardPosition.Null;
            this.edgeToRoot.SetUnknown();
            this.root = new Node();
            this.NodeCount = 1;
        }

        public async Task<Move> SearchAsync(Board board, int count, int timeLimit, CancellationToken ct)
        {
            return await Task.Run(() => Search(board, count, timeLimit, ct)).ConfigureAwait(false);
        }

        public Move Search(Board board, int count, int timeLimit = int.MaxValue) 
        {
            return Search(board, count, timeLimit, new CancellationToken(false));
        }

        public Move Search(Board board, int count, int timeLimit, CancellationToken ct)
        {
            if (this.root == null)
                throw new NullReferenceException("Set root before searching.");

            if (this.ValueFunctionCallback == null)
                throw new NullReferenceException("Set value function before searching.");

            var rootBoard = new FastBoard(board);
            var currentBoard = (from _ in Enumerable.Range(0, this.THREAD_NUM) select new FastBoard()).ToArray();
            this.edgeToRoot.Value = 1.0f - this.ValueFunctionCallback(rootBoard, 0);
            this.searchStartTime = Environment.TickCount;
            this.IsSearching = true;
            this.npsCounter = 0;
            this.Depth = 0;

#if SINGLE_THREAD
            for(var threadID = 0; threadID < this.THREAD_NUM; threadID++)
            {
                var b = currentBoard[threadID];
                for (var i = 0; !stop() && i < count / this.THREAD_NUM; i++)
                {
                    board.CopyTo(b, false);
                    SearchKernel(this.root, ref this.edgeToRoot, b, 0, threadID);
                }
            }
#else
            Parallel.For(0, this.THREAD_NUM, (threadID) =>
            {
                var b = currentBoard[threadID];
                for (var i = 0; !stop() && i < count / this.THREAD_NUM; i++)
                {
                    rootBoard.CopyTo(b);
                    SearchKernel(this.root, ref this.edgeToRoot, b, 0, threadID);
                }
            });
#endif
            for (var i = 0; !stop() && i < count % this.THREAD_NUM; i++)
            {
                rootBoard.CopyTo(currentBoard[0]);
                SearchKernel(this.root, ref this.edgeToRoot, currentBoard[0], 0, 0);
            }
            this.IsSearching = false;
            this.searchEndTime = Environment.TickCount;
            return new Move(rootBoard.SideToMove, SelectPosition());

            bool stop()
            {
                return ct.IsCancellationRequested || Environment.TickCount - this.searchStartTime >= timeLimit;
            }
        }

        void DecrementNodeCount(Node node)
        {
            this.nodeCount--;
            if (node.ChildNodes == null)
                return;
            foreach (var childNode in node.ChildNodes)
                if (childNode != null)
                    DecrementNodeCount(childNode);
        }

        BoardPosition SelectPosition()       
        {
            var edges = this.root.Edges;
            var prob = new float[edges.Length];
            var t = this.moveProbTemperature;
            for(var i = 0; i < prob.Length; i++)
            {
                var n = edges[i].VisitCount;
                if (n != 0)
                {
                    for (var j = 0; j < prob.Length; j++)
                        prob[i] += MathF.Pow((float)edges[j].VisitCount / n, 1.0f / t);
                    prob[i] = 1.0f / prob[i];
                }
                else
                    prob[i] = 0.0f;
            }

            var drawIdx = 0;
            var lossCount = 0;
            var drawCount = 0;
            for (var i = 1; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (!edge.IsUnknown)
                    if (edge.IsWin)
                    {
                        edgeToRoot.Label = EdgeLabel.Loss;
                        return edge.NextPos;
                    }
                    else if (edge.IsLoss)
                    {
                        lossCount++;
                        continue;
                    }
                    else
                    {
                        drawIdx = i;
                        drawCount++;
                    }
            }

            if (lossCount == this.root.ChildNum)
                this.edgeToRoot.Label = EdgeLabel.Win;
            else if (lossCount + drawCount == this.root.ChildNum)
            {
                this.edgeToRoot.Label = EdgeLabel.Draw;
                return edges[drawIdx].NextPos;
            }

            var sum = 0.0f;
            var arrow = this.RAND[0].NextFloat();
            var k = -1;
            do
                sum += prob[++k];
            while (sum < arrow);
            return edges[k].NextPos;
        }

        float SearchKernel(Node currentNode, ref Edge edgeToCurrentNode, FastBoard currentBoard, int depth, int threadID)     // goes down to leaf node and back up to root node with updating score
        {
            int childNodeIdx;
            float value;

            if (depth > this.Depth)
                this.Depth++;

            var lockTaken = false;
            try
            {
                Monitor.Enter(currentNode, ref lockTaken);
                if (currentNode.Edges == null)      // not expanded
                {
                    var positions = this.POSITIONS[threadID];
                    var moveCount = currentBoard.GetNextPositions(positions);
                    currentNode.Expand(positions, moveCount);
                }

                childNodeIdx = SelectChildNode(currentNode, ref edgeToCurrentNode);
                AddVirtualLoss(currentNode, childNodeIdx); 
                var edges = currentNode.Edges;
                var edge = edges[childNodeIdx];
                currentBoard.Update(edge.NextPos);

                if (!edge.IsLabeled) 
                {
                    LabelEdge(ref edges[childNodeIdx], currentBoard);
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
                            AtomicOperations.Increment(ref this.nodeCount);
                            AtomicOperations.Increment(ref this.npsCounter);
                        }
                        Monitor.Exit(currentNode);
                        lockTaken = false;
                        value = SearchKernel(currentNode.ChildNodes[childNodeIdx], ref edges[childNodeIdx], currentBoard, ++depth, threadID);
                    }
                    else    // current node is a leaf node
                    {
                        Monitor.Exit(currentNode);
                        lockTaken = false;
                        edges[childNodeIdx].Value = 1.0f - ValueFunctionCallback(currentBoard, threadID);
                        value = edges[childNodeIdx].Value;
                    }
                }
                else
                {
                    Monitor.Exit(currentNode);
                    value = currentNode.Edges[childNodeIdx].ActionValue;
                }
            }
            catch
            {
                if (lockTaken)
                    Monitor.Exit(currentNode);
                throw;
            }

            UpdateResult(currentNode, childNodeIdx, value);
            return 1.0f - value;
        }

        void AddVirtualLoss(Node node, int childNodeIdx)
        {
            AtomicOperations.Add(ref node.VirtualLossSum, this.virtualLoss);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VirtualLossSum, this.virtualLoss);
        }

        int SelectChildNode(Node parentNode, ref Edge edgeToParentNode)        
        {
            // to avoid division by zero or log(0), calculates UCB score assuming lost the game at least one time.
            var childNum = parentNode.ChildNum;
            var maxIdx = 0;
            var maxScore = float.NegativeInfinity;
            var sum = parentNode.VisitCount + parentNode.VirtualLossSum + parentNode.Edges.Length;
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

                var edge = parentNode.Edges[i];
                var n = edge.VisitCount + edge.VirtualLossSum + 1;
                var q = parentNode.Edges[i].WinCount / n;       
                var u = this.UCB_FACTOR * MathF.Sqrt(twoLogSum / n);
                var score = q + u;

                if (score > maxScore)
                {
                    maxScore = score;
                    maxIdx = i;
                }
            }

            if (lossCount == childNum)
                edgeToParentNode.SetWin();
            else if (lossCount + drawCount == childNum)
            {
                edgeToParentNode.SetDraw();
                return drawEdgeIdx;     // when other nodes are loss, it is better to select draw.
            }
            return maxIdx;
        }

        void UpdateResult(Node node, int childNodeIdx, float value)
        {
            AtomicOperations.Increment(ref node.Edges[childNodeIdx].VisitCount);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VirtualLossSum, -this.virtualLoss);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].WinCount, value);
            AtomicOperations.Increment(ref node.VisitCount);
            AtomicOperations.Add(ref node.VirtualLossSum, -this.virtualLoss);
            AtomicOperations.Add(ref node.WinCount, value);
        }

        static void LabelEdge(ref Edge edge, FastBoard currentBoard)
        {
            switch (currentBoard.GetGameResult())
            {
                case GameResult.NotOver:
                    edge.SetUnknown();
                    return;

                case GameResult.Win:
                    edge.SetLoss();
                    return;

                case GameResult.Loss:
                    edge.SetWin();
                    return;

                case GameResult.Draw:
                    edge.SetDraw();
                    return;
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
