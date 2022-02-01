////#define SINGLE_THREAD

//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//using Kalmia.Reversi;
//using Kalmia.Evaluation;

//namespace Kalmia.MCTS
//{
//    public struct PositionEval
//    {
//        public BoardPosition Position { get; }
//        public float MoveProbability { get; }
//        public float Value { get; }
//        public int PlayoutCount { get; }
//        public ReadOnlyCollection<PositionEval> PV { get; }

//        public PositionEval(Edge_old edge, float moveProb):this(edge, moveProb, new PositionEval[0]) { }

//        public PositionEval(Edge_old edge, float moveProb, IEnumerable<PositionEval> pv):this(edge, moveProb, new ReadOnlyCollection<PositionEval>(pv.ToArray())) { }

//        public PositionEval(BoardPosition pos, float moveProb, float value, int playoutCount)
//        {
//            this.Position = pos;
//            this.MoveProbability = moveProb;
//            this.Value = value;
//            this.PlayoutCount = playoutCount;
//            this.PV = new ReadOnlyCollection<PositionEval>(new PositionEval[0]);
//        }

//        PositionEval(Edge_old edge, float moveProb, ReadOnlyCollection<PositionEval> pv)
//        {
//            this.Position = edge.Pos;
//            this.MoveProbability = moveProb;
//            this.Value = edge.Value;
//            this.PlayoutCount = edge.VisitCount;
//            this.PV = pv;
//        }

//        public static IEnumerable<PositionEval> EnumerateMoveEvals(Edge_old[] edges)
//        {
//            var visitCountSum = edges.Sum(e => e.VisitCount);
//            for (var i = 0; i < edges.Length; i++)
//                yield return new PositionEval(edges[i], (edges[i].VisitCount != 0) ? (float)edges[i].VisitCount / visitCountSum : 0.0f);
//        }
//    }

//    public struct SearchStatistic
//    {
//        public long EdgeCount { get; }
//        public long SearchedEdgeCount { get; }
//        public long NodeCount { get; }
//        public long NonLeafNodeCount { get; }
//        public int MaxDepth { get; }

//        public SearchStatistic(long edgeCount, long searchedEdgeCount, long nodeCount, long nonLeafNodeCount, int maxDepthCount)
//        {
//            this.EdgeCount = edgeCount;
//            this.SearchedEdgeCount = searchedEdgeCount;
//            this.NodeCount = nodeCount;
//            this.NonLeafNodeCount = nonLeafNodeCount;
//            this.MaxDepth = maxDepthCount;
//        }
//    }

//    public class GameInfo
//    {
//        public FastBoard Board;
//        public BoardFeature Feature;
//        public Color SideToMove { get { return this.Board.SideToMove; } }

//        public GameInfo()
//        {
//            this.Board = new FastBoard();
//            this.Feature = new BoardFeature(this.Board);
//        }

//        public GameInfo(FastBoard board):this()
//        {
//            board.CopyTo(this.Board);
//            this.Feature = new BoardFeature(board);
//        }

//        public void Update(BoardPosition pos)
//        {
//            var flipped = this.Board.Update(pos);
//            this.Feature.Update(pos, flipped);
//        }

//        public void CopyTo(GameInfo dest)
//        {
//            this.Board.CopyTo(dest.Board);
//            this.Feature.CopyTo(dest.Feature);
//        }
//    }

//    public class UCT_Old
//    {
//        const int DEFAULT_VALUE_CACHE_SIZE = 1000000;

//        readonly int THREAD_NUM;
//        readonly long MAX_NODE_COUNT;
//        readonly BoardPosition[][] POSITIONS;
//        readonly Xorshift32[] RAND;
//        readonly float UCB_FACTOR_INIT;
//        readonly float UCB_FACTOR_BASE;
//        readonly ValueFunction VALUE_FUNC;

//        int virtualLoss = 3;
//        Edge_old edgeToRoot;
//        Node_old root;
//        LRUCache<ulong, (Bitboard board, Node_old node)>[] nodeCache;

//        long searchedNodeCount = 0L;
//        long playoutCount = 0L;
//        int searchStartTime;
//        int searchEndTime;

//        public int Depth { get; private set; } = 0;
//        public long SearchedNodeCount { get { return this.searchedNodeCount; } }
//        public long PlayoutCount { get { return this.playoutCount; } }
//        public float Nps { get { return (this.SearchEllapsedTime != 0) ? this.searchedNodeCount / (this.SearchEllapsedTime * 1.0e-3f) : 0; } }
//        public float Pps { get { return (this.SearchEllapsedTime != 0) ? this.playoutCount / (this.SearchEllapsedTime * 1.0e-3f) : 0; } }
//        public bool IsSearching { get; private set; }
//        public int SearchEllapsedTime { get { return (this.IsSearching) ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }

//        public int VirtualLoss
//        {
//            get { return this.virtualLoss; }
//            set { if (value >= 0) this.virtualLoss = value; else throw new ArgumentOutOfRangeException("virtual loss must be positive or zero."); }
//        }

//        public UCT_Old(ValueFunction valueFunc, float ucbFactorInit, float ucbFactorBase, long maxNodeCount, int threadNum)
//               : this(valueFunc, ucbFactorInit, ucbFactorBase, maxNodeCount, DEFAULT_VALUE_CACHE_SIZE, threadNum) { }

//        public UCT_Old(ValueFunction valueFunc, float ucbFactorInit, float ucbFactorBase, long maxNodeCount, int valueCacheSize, int threadNum)
//        {
//            this.MAX_NODE_COUNT = maxNodeCount;
//            this.VALUE_FUNC = valueFunc;
//            this.UCB_FACTOR_INIT = ucbFactorInit;
//            this.UCB_FACTOR_BASE = ucbFactorBase;
//            this.THREAD_NUM = threadNum;
//            this.POSITIONS = (from _ in Enumerable.Range(0, threadNum) select new BoardPosition[Board.MAX_MOVE_COUNT]).ToArray();
//            this.RAND = (from i in Enumerable.Range(0, threadNum) select new Xorshift32()).ToArray();
//            this.edgeToRoot.Pos = BoardPosition.Null;
//            this.root = new Node_old();
//            this.searchedNodeCount = 1;
//#if RELEASE
//            this.nodeCache = new LRUCache<ulong, Node>[2] { new LRUCache<ulong, Node>(valueCacheSize), new LRUCache<ulong, Node>(valueCacheSize) };
//#elif DEBUG
//            this.nodeCache = new LRUCache<ulong, (Bitboard, Node_old)>[2] { new LRUCache<ulong, (Bitboard, Node_old)>(valueCacheSize), new LRUCache<ulong, (Bitboard, Node_old)>(valueCacheSize) };
//#endif
//        }

//        public PositionEval GetRootNodeEvaluation()
//        {
//            if (this.root == null)
//                return new PositionEval();
//            return new PositionEval(this.edgeToRoot.Pos, 1.0f, (this.edgeToRoot.IsProved) ? 1.0f - this.edgeToRoot.Value : this.root.Value, this.root.VisitCount);
//        }

//        public IEnumerable<PositionEval> GetChildNodeEvaluations()
//        {
//            if (this.root == null || this.root.Edges == null)
//                yield break;

//            var edges = this.root.Edges;
//            var visitCountSum = edges.Sum(e => e.VisitCount);
//            for (var i = 0; i < edges.Length; i++)
//                yield return new PositionEval(edges[i], (edges[i].VisitCount != 0) ? (float)edges[i].VisitCount / visitCountSum : 0.0f, GetPV(i));
//        }

//        // Looks one move ahead, and if a child node which has same position as specified one is found, sets it to root,
//        // otherwise sets new Node object to root.
//        public void SetRoot(BoardPosition pos)      
//        {
//            if (pos != BoardPosition.Null && this.edgeToRoot.Pos == pos)
//                return;

//            this.edgeToRoot.Pos = pos;
//            if (this.root != null && this.root.Edges != null)
//                for (var i = 0; i < this.root.Edges.Length; i++)
//                {
//                    if (pos == this.root.Edges[i].Pos && this.root.ChildNodes != null && this.root.ChildNodes[i] != null)
//                    {
//                        var prevRoot = this.root;
//                        this.root = prevRoot.ChildNodes[i];
//                        this.edgeToRoot = prevRoot.Edges[i];
//                        this.root.VisitCount += edgeToRoot.VisitCount;
//                        this.root.ValueSum += edgeToRoot.VisitCount - edgeToRoot.ValueSum;
//                        prevRoot.Edges = null;
//                        prevRoot.ChildNodes = null; 
//                        return;
//                    }
//                }
//            this.edgeToRoot = new Edge_old();
//            this.edgeToRoot.Pos = pos;
//            this.root = new Node_old();
//            this.nodeCache[0].Clear();
//            this.nodeCache[1].Clear();
//        }

//        public void Clear()
//        {
//            this.edgeToRoot = new Edge_old();
//            this.edgeToRoot.Pos = BoardPosition.Null;
//            this.edgeToRoot.SetUnknown();
//            this.root = new Node_old();
//            searchedNodeCount = 1;
//        }

//        public async Task SearchAsync(Board board, int count, int timeLimit, CancellationToken ct)
//        {
//            await Task.Run(() => Search(board, count, timeLimit, ct)).ConfigureAwait(false);
//        }

//        public void Search(Board board, int count, int timeLimit = int.MaxValue) 
//        {
//            Search(board, count, timeLimit, new CancellationToken(false));
//        }

//        public void Search(Board board, int count, int timeLimit, CancellationToken ct)
//        {
//            if (this.root == null)
//                throw new NullReferenceException("Set root before searching.");

//            var rootGameInfo = new GameInfo(new FastBoard(board));
//            var gameInfo = (from _ in Enumerable.Range(0, this.THREAD_NUM) select new GameInfo()).ToArray();
//            this.searchStartTime = Environment.TickCount;
//            this.IsSearching = true;
//            this.searchedNodeCount = 0L;
//            this.playoutCount = 0L;
//            this.Depth = 0;

//#if SINGLE_THREAD
//            for(var threadID = 0; threadID < this.THREAD_NUM; threadID++)
//            {
//                var gInfo = gameInfo[threadID];
//                for (var i = 0; !stop() && i < count / this.THREAD_NUM; i++)
//                {
//                    rootGameInfo.CopyTo(gInfo);
//                    SearchKernel(this.root, ref this.edgeToRoot, gInfo, 0, threadID);
//                }
//            }
//#else
//            Parallel.For(0, this.THREAD_NUM, (threadID) =>
//            {
//                var gInfo = gameInfo[threadID];
//                for (var i = 0; !stop() && i < count / this.THREAD_NUM; i++)
//                {
//                    rootGameInfo.CopyTo(gInfo);
//                    SearchKernel(this.root, ref this.edgeToRoot, gInfo, 0, threadID);
//                    if (this.searchedNodeCount >= this.MAX_NODE_COUNT)
//                        break;
//                }
//            });
//#endif
//            rootGameInfo.CopyTo(gameInfo[0]);
//            for (var i = 0; !stop() && i < count % this.THREAD_NUM; i++)
//            {
//                rootGameInfo.CopyTo(gameInfo[0]);
//                SearchKernel(this.root, ref this.edgeToRoot, gameInfo[0], 0, 0);
//                if (this.searchedNodeCount >= this.MAX_NODE_COUNT)
//                    break;
//            }
//            this.IsSearching = false;
//            this.searchEndTime = Environment.TickCount;

//            bool stop()
//            {
//                return ct.IsCancellationRequested || Environment.TickCount - this.searchStartTime >= timeLimit;
//            }
//        }

//        public BoardPosition SelectMaxVisitCountAndMaxValuePosition()
//        {
//            var edges = this.root.Edges;
//            var maxIdx = 0;
//            for (var i = 1; i < edges.Length; i++)
//                if (edges[i].VisitCount > edges[maxIdx].VisitCount)
//                    maxIdx = i;
//                else if (edges[i].VisitCount == edges[maxIdx].VisitCount && edges[i].Value > edges[maxIdx].Value)
//                    maxIdx = i;
//            return edges[maxIdx].Pos;
//        }

//        public BoardPosition SelectNextPositionByMoveProbability(float temperature)
//        {
//            var edges = this.root.Edges;
//            var prob = new float[edges.Length];
//            for (var i = 0; i < prob.Length; i++)
//            {
//                var n = edges[i].VisitCount;
//                if (n != 0)
//                {
//                    for (var j = 0; j < prob.Length; j++)
//                        prob[i] += MathF.Pow((float)edges[j].VisitCount / n, 1.0f / temperature);
//                    prob[i] = 1.0f / prob[i];
//                }
//                else
//                    prob[i] = 0.0f;
//            }

//            var drawIdx = 0;
//            var lossCount = 0;
//            var drawCount = 0;
//            for (var i = 1; i < edges.Length; i++)
//            {
//                var edge = edges[i];
//                if (!edge.IsUnknown)
//                    if (edge.IsWin)
//                    {
//                        edgeToRoot.Label = EdgeLabel.Loss;
//                        return edge.Pos;
//                    }
//                    else if (edge.IsLoss)
//                    {
//                        lossCount++;
//                        continue;
//                    }
//                    else
//                    {
//                        drawIdx = i;
//                        drawCount++;
//                    }
//            }

//            if (lossCount == this.root.ChildNum)
//                this.edgeToRoot.Label = EdgeLabel.Win;
//            else if (lossCount + drawCount == this.root.ChildNum)
//            {
//                this.edgeToRoot.Label = EdgeLabel.Draw;
//                return edges[drawIdx].Pos;
//            }

//            var sum = 0.0f;
//            var arrow = this.RAND[0].NextFloat();
//            var k = -1;
//            do
//                sum += prob[++k];
//            while (sum < arrow);
//            return edges[k].Pos;
//        }

//        int hashHitCount = 0;
//        float SearchKernel(Node_old currentNode, ref Edge_old edgeToCurrentNode, GameInfo currentGameInfo, int depth, int threadID)     // goes down to leaf node and back up to root node with updating score
//        {
//            var currentBoard = currentGameInfo.Board;
//            int childNodeIdx;
//            float value;

//            if (depth > this.Depth)
//                this.Depth++;

//            var lockTaken = false;
//            try
//            {
//                Monitor.Enter(currentNode, ref lockTaken);
//                if (currentNode.Edges == null)      // not expanded
//                {
//                    var positions = this.POSITIONS[threadID];
//                    var moveNum = currentBoard.GetNextPositions(positions);
//                    currentNode.Expand(positions, moveNum);
//                }

//                childNodeIdx = SelectChildNode(currentNode, ref edgeToCurrentNode);
//                AddVirtualLoss(currentNode, childNodeIdx); 
//                var edges = currentNode.Edges;
//                var edge = edges[childNodeIdx];
//                currentGameInfo.Update(edge.Pos);

//                if (!edge.IsLabeled)
//                {
//                    LabelEdge(ref edges[childNodeIdx], currentBoard);
//                    edge = edges[childNodeIdx];
//                }
                
//                if (edge.IsUnknown)
//                {
//                    if (edge.VisitCount >= 1)     // current node is not a leaf node 
//                    {
//                        if (currentNode.ChildNodes == null)
//                        {
//                            currentNode.InitChildNodes();
//                            AtomicOperations.Increment(ref this.searchedNodeCount); 
//                        }

//                        if (currentNode.ChildNodes[childNodeIdx] == null)
//                        {
//                            var hashCode = currentBoard.GetHashCode();
//                            var cache = this.nodeCache[(int)currentBoard.SideToMove];
//                            if (cache.Contains(hashCode) && cache[hashCode].board == currentBoard.GetBitboard())
//                            {
//                                currentNode.ChildNodes[childNodeIdx] = cache[hashCode].node;
//                                AtomicOperations.Increment(ref this.hashHitCount);
//                            }
//                            else
//                                cache[hashCode] = (currentBoard.GetBitboard(), currentNode.CreateChildNode(childNodeIdx));
//                        }
//                        Monitor.Exit(currentNode);
//                        lockTaken = false;
//                        value = SearchKernel(currentNode.ChildNodes[childNodeIdx], ref edges[childNodeIdx], currentGameInfo, ++depth, threadID);
//                    }
//                    else    // current node is a leaf node
//                    {
//                        Monitor.Exit(currentNode);
//                        lockTaken = false;
//                        value = 1.0f - this.VALUE_FUNC.F(currentGameInfo.Feature);
//                        AtomicOperations.Increment(ref this.playoutCount);
//                    }
//                }
//                else    // current node was proved
//                {
//                    Monitor.Exit(currentNode);
//                    value = currentNode.Edges[childNodeIdx].Value;
//                }
//            }
//            catch
//            {
//                if (lockTaken)
//                    Monitor.Exit(currentNode);
//                throw;
//            }

//            UpdateResult(currentNode, childNodeIdx, value);
//            return 1.0f - value;
//        }

//        void AddVirtualLoss(Node_old node, int childNodeIdx)
//        {
//            AtomicOperations.Add(ref node.VirtualLossSum, this.virtualLoss);
//            AtomicOperations.Add(ref node.Edges[childNodeIdx].VirtualLossSum, this.virtualLoss);
//        }

//        int SelectChildNode(Node_old parentNode, ref Edge_old edgeToParentNode)
//        {
//            // to avoid division by zero or log(0), calculates UCB score assuming lost the game at least one time.
//            var childNum = parentNode.ChildNum;
//            var maxIdx = 0;
//            var maxScore = float.NegativeInfinity;
//            var sum = parentNode.VisitCount + parentNode.VirtualLossSum + parentNode.Edges.Length;
//            var twoLogSum = 2.0f * FastMath.Log(sum);
//            var cBase = this.UCB_FACTOR_BASE;
//            var c = this.UCB_FACTOR_INIT + FastMath.Log((1.0f + sum + cBase) / cBase);

//            var lossCount = 0;
//            var drawCount = 0;
//            var drawEdgeIdx = 0;
//            for (var i = 0; i < childNum; i++)
//            {
//                if (parentNode.Edges[i].IsWin)
//                {
//                    edgeToParentNode.SetLoss();
//                    return i;       // definitely select win
//                }
//                else if (parentNode.Edges[i].IsLoss)
//                {
//                    lossCount++;        // do not select loss
//                    continue;
//                }
//                else if (parentNode.Edges[i].IsDraw)
//                {
//                    drawCount++;
//                    drawEdgeIdx = i;
//                }

//                var edge = parentNode.Edges[i];
//                var n = edge.VisitCount + edge.VirtualLossSum + 1;
//                var q = parentNode.Edges[i].ValueSum / n;
//                var u = c * MathF.Sqrt(twoLogSum / n);
//                var score = q + u;

//                if (score > maxScore)
//                {
//                    maxScore = score;
//                    maxIdx = i;
//                }
//            }

//            if (lossCount == childNum)
//                edgeToParentNode.SetWin();
//            else if (lossCount + drawCount == childNum)
//            {
//                edgeToParentNode.SetDraw();
//                return drawEdgeIdx;     // when other nodes are loss, it is better to select draw.
//            }
//            return maxIdx;
//        }

//        void UpdateResult(Node_old node, int childNodeIdx, float value)
//        {
//            AtomicOperations.Increment(ref node.Edges[childNodeIdx].VisitCount);
//            AtomicOperations.Add(ref node.Edges[childNodeIdx].VirtualLossSum, -this.virtualLoss);
//            AtomicOperations.Add(ref node.Edges[childNodeIdx].ValueSum, value);
//            AtomicOperations.Increment(ref node.VisitCount);
//            AtomicOperations.Add(ref node.VirtualLossSum, -this.virtualLoss);
//            AtomicOperations.Add(ref node.ValueSum, value);
//        }

//        static void LabelEdge(ref Edge_old edge, FastBoard currentBoard)
//        {
//            switch (currentBoard.GetGameResult())
//            {
//                case GameResult.NotOver:
//                    edge.SetUnknown();
//                    return;

//                case GameResult.Win:
//                    edge.SetLoss();
//                    return;

//                case GameResult.Loss:
//                    edge.SetWin();
//                    return;

//                case GameResult.Draw:
//                    edge.SetDraw();
//                    return;
//            }
//        }

//        static int SelectBestChildNode(Node_old node)   // best node means the node which has the largest visit count
//        {
//            var edges = node.Edges;
//            var bestEdgeIdx = 0;
//            var lossCount = 0;
//            var drawCount = 0;
//            for (var i = 1; i < edges.Length; i++)
//            {
//                var edge = edges[i];
//                if (!edge.IsUnknown)
//                    if (edge.IsWin)
//                        return i;
//                    else if (edge.IsLoss)
//                    {
//                        lossCount++;
//                        continue;
//                    }
//                    else
//                        drawCount++;

//                if (edge.VisitCount > edges[bestEdgeIdx].VisitCount)
//                    bestEdgeIdx = i;
//            }
//            return bestEdgeIdx;
//        }

//        IEnumerable<PositionEval> GetPV(int childIdx)    // PV = Principal Variation
//        {
//            if (this.root == null || this.root.ChildNodes == null || this.root.ChildNodes[childIdx] == null)
//                yield break;

//            var node = this.root.ChildNodes[childIdx];
//            while (node != null && node.Edges != null)
//            {
//                var idx = SelectBestChildNode(node);
//                var edge = node.Edges[idx];
//                var vistCountSum = node.Edges.Sum(e => e.VisitCount);
//                yield return new PositionEval(edge, edge.VisitCount / vistCountSum);

//                if (node.ChildNodes != null && node.ChildNodes[idx] != null)
//                    node = node.ChildNodes[idx];
//                else
//                    break;
//            }
//        }
//    }
//}
