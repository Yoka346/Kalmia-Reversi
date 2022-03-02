using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using Kalmia.Evaluation;
using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    enum EdgeLabel : byte
    {
        NotVisited = 0x00,
        Evaluated = 0x01,
        Proved = 0xf0,
        Win = Proved | 0x01,
        Loss = Proved | 0x02,
        Draw = Proved | 0x03
    }

    // To avoid random access to child node, Node object has Edge array,
    // each Edge object is located sequentially in memory, and informations about each child node are cached.
    struct Edge   
    {
        static readonly double[] EDGE_LABEL_TO_REWARD = new double[] { 1.0, 0.0, 0.5 };

        public BoardPosition Pos;
        public uint VisitCount;
        public double RewardSum;
        public EdgeLabel Label;

        public double ExpReward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.IsProved ? EDGE_LABEL_TO_REWARD[(int)(this.Label ^ EdgeLabel.Proved) - 1] :this.RewardSum / this.VisitCount; 
        }

        public bool IsVisited { get { return this.Label != EdgeLabel.NotVisited; } }
        public bool IsProved { get { return (this.Label & EdgeLabel.Proved) != 0; } }
        public bool IsWin { get { return this.Label == EdgeLabel.Win; } }
        public bool IsLoss { get { return this.Label == EdgeLabel.Loss; } }
        public bool IsDraw { get { return this.Label == EdgeLabel.Draw; } }
    }

    class Node
    {
        static uint _ObjectCount = 0u;
        public static uint ObjectCount { get { return _ObjectCount; } }

        public uint VisitCount;
        public Edge[] Edges;     
        public Node[] ChildNodes;
        public int ChildNum { get { return this.Edges.Length; } }
        public bool ChildNodesAreInitialized { get { return this.ChildNodes is not null; } }
        public bool IsExpanded { get { return this.Edges is not null; } }

        public double ExpReward
        {
            get
            {
                var lossCount = 0;
                var drawCount = 0;
                var rewardSum = 0.0;
                var visitCount = 0u;
                foreach (var edge in this.Edges)
                {
                    if (edge.IsProved)
                    {
                        if (edge.IsWin)
                            return 1.0;
                        else if (edge.IsLoss)
                            lossCount++;
                        else
                        {
                            drawCount++;
                            rewardSum += 0.5;
                        }
                    }
                    else
                        rewardSum += edge.RewardSum;
                    visitCount += edge.VisitCount;
                }

                if (lossCount == this.ChildNum)
                    return 0.0f;

                return (lossCount + drawCount == this.ChildNum) ? 0.5 : rewardSum / visitCount;
            }
        }

        public Node()
        {
            AtomicOperations.Increment(ref _ObjectCount);
        }

        ~Node()
        {
            AtomicOperations.Decrement(ref _ObjectCount);
        }

        public void InitChildNodes()
        {
            this.ChildNodes = new Node[this.Edges.Length];
        }
    }

    class Searcher
    {
        public GameInfo GameInfo;
        public BoardPosition[] Positions;

        public Searcher(GameInfo gameInfo)
        {
            this.GameInfo = new GameInfo(gameInfo);
            this.Positions = new BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
        }
    }

    public class UCTOptions
    {
        /// <summary>
        /// One of the UCB parameter.
        /// </summary>
        public float UCBFactorInit { get; set; } = 0.35f;

        /// <summary>
        /// One of the UCB parameter.
        /// </summary>
        public float UCBFactorBase { get; set; } = 19652.0f;

        /// <summary>
        /// The virtual loss to prevent from searching a single node by multiple threads. 
        /// </summary>
        public uint VirtualLoss { get; set; } = 3u;

        /// <summary>
        /// The number of search threads.
        /// </summary>
        public int ThreadNum { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// The limitation of Node object count.
        /// </summary>
        public uint NodeNumLimit { get; set; } = (uint)2.0e+7;

        /// <summary>
        /// The limitation of managed memory usage.
        /// </summary>
        public long ManagedMemoryLimit { get; set; } = long.MaxValue;
    }

    public record class MoveEvaluation(Move Move, double MoveProbability, double Value, bool IsProved, uint PlayoutCount, ReadOnlyCollection<Move> PrincipalVariation);

    public record class SearchInfo(MoveEvaluation RootEvaluation, ReadOnlyCollection<MoveEvaluation> ChildEvaluations);

    public class UCT
    {
        const float FPU_ROOT = 1.0f;

        static readonly EdgeLabel[] GAME_RESULT_TO_EDGE_LABEL = new EdgeLabel[3] { EdgeLabel.Loss, EdgeLabel.Draw, EdgeLabel.Win };

        readonly float UCB_FACTOR_INIT;
        readonly float UCB_FACTOR_BASE;
        readonly uint VIRTUAL_LOSS;
        readonly ValueFunction VALUE_FUNC;
        readonly int THREAD_NUM;
        readonly uint NODE_NUM_LIMIT;
        readonly long MANAGED_MEM_LIMIT;

        Board rootState;
        Node root;
        int ppsCounter;
        int searchStartTime;
        int searchEndTime;
        CancellationTokenSource cts;

        public bool IsSearching { get; private set; }
        public float Pps { get { return this.ppsCounter / (this.SearchEllapsedMilliSec * 1.0e-3f); } }
        public int SearchEllapsedMilliSec { get { return this.IsSearching ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }

        public SearchInfo SearchInfo
        {
            get
            {
                lock (this.root)
                {
                    if (this.root is null)
                        return null;

                    var rootIsProved = this.root.Edges.Any(e => e.IsProved);
                    var rootEval = new MoveEvaluation(Move.Null, 1.0f, this.root.ExpReward, rootIsProved, this.root.VisitCount, new ReadOnlyCollection<Move>(new Move[0]));
                    var childEvals = new List<MoveEvaluation>();
                    if (this.root.IsExpanded)
                    {
                        var edges = this.root.Edges;
                        var childNodes = this.root.ChildNodes;
                        var visitCountSum = edges.Sum(e => e.VisitCount);
                        for(var i = 0; i < edges.Length; i++)
                        {
                            var move = new Move(this.rootState.SideToMove, edges[i].Pos);
                            childEvals.Add(new MoveEvaluation(move, (visitCountSum != 0) ? (float)edges[i].VisitCount / visitCountSum : 1.0f / this.root.Edges.Length,
                                                              edges[i].ExpReward, edges[i].IsProved, edges[i].VisitCount,
                                                              (childNodes is not null && childNodes[i] is not null) ? GetPrincipalVariation(childNodes[i], edges[i], this.rootState.Opponent)
                                                                                                                    : new ReadOnlyCollection<Move>(new Move[] { move })));
                        }
                    }
                    return new SearchInfo(rootEval, new ReadOnlyCollection<MoveEvaluation>(childEvals));
                }
            }
        }

        public UCT(UCTOptions options, ValueFunction valueFunc)
        {
            this.UCB_FACTOR_INIT = options.UCBFactorInit;
            this.UCB_FACTOR_BASE = options.UCBFactorBase;
            this.VIRTUAL_LOSS = options.VirtualLoss;
            this.VALUE_FUNC = valueFunc;
            this.THREAD_NUM = options.ThreadNum;
            this.NODE_NUM_LIMIT = options.NodeNumLimit;
            this.MANAGED_MEM_LIMIT = options.ManagedMemoryLimit;
        }

        public void SetRoot(Board board)
        {
            this.rootState = new Board(board);
            this.root = new Node();
            InitRootChildNodes();
        }

        public bool TransitionToRootChildNode(Move move)
        {
            if (move.Color != this.rootState.SideToMove)
                return false;

            if (this.root is not null && this.root.Edges != null)
                for (var i = 0; i < this.root.Edges.Length; i++)
                    if (move.Pos == this.root.Edges[i].Pos && this.root.ChildNodes is not null && this.root.ChildNodes[i] is not null)
                    {
                        var prevRoot = this.root;
                        this.root = prevRoot.ChildNodes[i];
                        this.root.VisitCount += prevRoot.Edges[i].VisitCount;
                        this.rootState.Update(move);
                        InitRootChildNodes();
                        prevRoot.ChildNodes[i] = null;
                        return true;
                    }
            return false;
        }

#if DEBUG
        public void SearchOnSingleThread(uint searchCount, int timeLimitCentiSec)
        {
            var board = this.rootState.GetFastBoard();
            this.cts = new CancellationTokenSource();
            SearchKernel(new Searcher(new GameInfo(new FastBoard(board), new BoardFeature(board))), searchCount, timeLimitCentiSec, this.cts.Token);
        }
#endif

        public void Search(uint searchCount)
        {
            Search(searchCount, int.MaxValue / 10);
        }

        public void Search(uint searchCount, int timeLimitCentiSec)
        {
            if (this.rootState == null)
                throw new NullReferenceException("Set root before search.");

            this.cts = new CancellationTokenSource();
            var board = this.rootState.GetFastBoard();
            this.ppsCounter = 0;
            var searchers = (from _ in Enumerable.Range(0, this.THREAD_NUM) select new Searcher(new GameInfo(new FastBoard(board), new BoardFeature(board)))).ToArray();
            this.IsSearching = true;
            this.searchStartTime = Environment.TickCount;
            Parallel.For(0, this.THREAD_NUM, threadID => SearchKernel(searchers[threadID], searchCount / (uint)this.THREAD_NUM, timeLimitCentiSec, this.cts.Token));
            SearchKernel(searchers[0], searchCount % (uint)this.THREAD_NUM, timeLimitCentiSec, this.cts.Token);
            this.IsSearching = false;
            this.searchEndTime = Environment.TickCount;
            this.cts.Dispose();
            this.cts = null;
        }

        public async Task SearchAsync(uint searchCount, int timeLimitCentiSec)
        {
            await Task.Run(() => Search(searchCount, timeLimitCentiSec)).ConfigureAwait(false);
        }

        public void RequestToStopSearch()
        {
            if (this.IsSearching)
                this.cts.Cancel();
        }

        void InitRootChildNodes()
        {
            if (this.root.Edges is null)
                ExpandNode(new Searcher(new GameInfo(this.rootState.GetFastBoard(), 
                           new BoardFeature(this.rootState.GetFastBoard()))), this.root);

            var edges = this.root.Edges;
            if (this.root.ChildNodes is null)
                this.root.InitChildNodes();

            var childNodes = this.root.ChildNodes;
            for (var i = 0; i < edges.Length; i++)
                if (childNodes[i] is null)
                    childNodes[i] = new Node();
        }

        void SearchKernel(Searcher searcher, uint searchCount, int timeLimitCentiSec, CancellationToken ct)
        {
            var timeLimitMilliSec = timeLimitCentiSec * 10;
            var rootGameInfo = new GameInfo(searcher.GameInfo);
            for (var i = 0u; i < searchCount && !IsTimeout(timeLimitMilliSec, ct); i++)
            {
                rootGameInfo.CopyTo(searcher.GameInfo);
                VisitRootNode(searcher);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsTimeout(int timeLimitMilliSec, CancellationToken ct) => this.SearchEllapsedMilliSec >= timeLimitMilliSec || ct.IsCancellationRequested || Node.ObjectCount >= this.NODE_NUM_LIMIT || Environment.WorkingSet >= this.MANAGED_MEM_LIMIT;

        void VisitRootNode(Searcher searcher)
        {
            int childIdx;
            var edges = this.root.Edges;

            lock (this.root)
            {
                childIdx = SelectRootChildNode();
                AddVirtualLoss(this.root, childIdx);
            }

            searcher.GameInfo.Update(edges[childIdx].Pos);
            if (edges[childIdx].IsVisited)
                UpdateResult(this.root, childIdx, VisitNode(searcher, this.root.ChildNodes[childIdx], ref this.root.Edges[childIdx]));
            else
            {
                edges[childIdx].Label = EdgeLabel.Evaluated;
                UpdateResult(this.root, childIdx, EstimateReward(searcher.GameInfo.Feature));
            }
        }

        double VisitNode(Searcher searcher, Node currentNode, ref Edge edgeToCurrentNode)
        {
            int childIdx;
            double reward;
            var nodeLocked = false;

            try
            {
                Monitor.Enter(currentNode, ref nodeLocked);

                if (!currentNode.IsExpanded)
                    ExpandNode(searcher, currentNode);

                var edges = currentNode.Edges;
                childIdx = SelectChildNode(currentNode, ref edgeToCurrentNode);
                AddVirtualLoss(currentNode, childIdx);
                searcher.GameInfo.Update(edges[childIdx].Pos);

                if (!edges[childIdx].IsVisited)
                    LabelEdge(ref edges[childIdx], searcher.GameInfo);

                if (!edges[childIdx].IsProved)
                {
                    if (edges[childIdx].IsVisited)    // child node is not a leaf node.
                    {
                        if (currentNode.ChildNodes is null)
                            currentNode.InitChildNodes();

                        if (currentNode.ChildNodes[childIdx] is null)
                            currentNode.ChildNodes[childIdx] = new Node();

                        Monitor.Exit(currentNode);
                        nodeLocked = false;

                        reward = VisitNode(searcher, currentNode.ChildNodes[childIdx], ref edges[childIdx]);
                    }
                    else    // child node is a leaf node.
                    {
                        Monitor.Exit(currentNode);
                        nodeLocked = false;

                        edges[childIdx].Label = EdgeLabel.Evaluated;
                        reward = EstimateReward(searcher.GameInfo.Feature);
                    }
                }
                else    // child node is proved node.
                {
                    Monitor.Exit(currentNode);
                    nodeLocked = false;

                    reward = edges[childIdx].ExpReward;
                }
            }
            catch
            {
                if (nodeLocked)
                    Monitor.Exit(currentNode);
                throw;
            }

            UpdateResult(currentNode, childIdx, reward);
            return 1.0f - reward;
        }

        void ExpandNode(Searcher searcher, Node node)
        {
            var positions = searcher.Positions;
            var edges = node.Edges = new Edge[searcher.GameInfo.GetNextPositionCandidates(positions)];
            for (var i = 0; i < edges.Length; i++)
                edges[i].Pos = positions[i];
        }

        int SelectRootChildNode()
        {
            var edges = this.root.Edges;
            var maxIdx = 0;
            var maxScore = float.NegativeInfinity;
            var sum = this.root.VisitCount;
            var logSum = FastMath.Log(sum);
            var cBase = this.UCB_FACTOR_BASE;
            var c = this.UCB_FACTOR_INIT + FastMath.Log((1.0f + sum + cBase) / cBase);
            var defaultU = (sum == 0) ? 0.0f : MathF.Sqrt(logSum);

            var lossCount = 0;
            var drawCount = 0;
            var drawIdx = 0;
            for(var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge.IsWin)
                    return i;
                else if (edge.IsLoss)
                {
                    lossCount++;
                    continue;
                }else if (edge.IsDraw)
                {
                    drawCount++;
                    drawIdx = i;
                }

                var n = edge.VisitCount;
                float q, u;
                if(n == 0)
                {
                    q = FPU_ROOT;
                    u = defaultU;
                }
                else
                {
                    q = (float)edge.ExpReward;
                    u = c * MathF.Sqrt(logSum / n);
                }

                var score = q + u;
                if (score > maxScore)
                {
                    maxScore = score;
                    maxIdx = i;
                }
            }

            return (lossCount + drawCount == edges.Length) ? drawIdx : maxIdx;
        }

        int SelectChildNode(Node parentNode, ref Edge edgeToParentNode)
        {
            var edges = parentNode.Edges;
            var maxIdx = 0;
            var maxScore = float.NegativeInfinity;
            var sum = parentNode.VisitCount;
            var logSum = FastMath.Log(sum);
            var cBase = this.UCB_FACTOR_BASE;
            var c = this.UCB_FACTOR_INIT + FastMath.Log((1.0f + sum + cBase) / cBase);
            var defaultU = (sum == 0) ? 0.0f : MathF.Sqrt(logSum);
            var parentQ = (float)(edgeToParentNode.RewardSum / edgeToParentNode.VisitCount);

            var lossCount = 0;
            var drawCount = 0;
            var drawIdx = 0;
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge.IsWin)
                {
                    // when at least one child node is win, parent node is win,
                    // so edge to parent node is lose. (current player's loss means opponent player's win) 
                    edgeToParentNode.Label = EdgeLabel.Loss;    
                    return i;   // definitely select win node
                }
                else if (edge.IsLoss)
                {
                    lossCount++;
                    continue;   // do not select loss node
                }
                else if (edge.IsDraw)
                {
                    drawCount++;
                    drawIdx = i;
                }

                var n = edge.VisitCount;
                float q, u;
                if (n == 0)
                {
                    q = parentQ;
                    u = defaultU;
                }
                else
                {
                    q = (float)edge.ExpReward;
                    u = c * MathF.Sqrt(logSum / n);
                }

                var score = q + u;
                if (score > maxScore)
                {
                    maxScore = score;
                    maxIdx = i;
                }
            }

            if (lossCount == edges.Length)
                edgeToParentNode.Label = EdgeLabel.Win;     // when all child nodes are loss, parent node is loss, so edge to parent node is win. 
            else if (lossCount + drawCount == edges.Length)
            {
                edgeToParentNode.Label = EdgeLabel.Draw;
                return drawIdx;     // when other nodes are loss, it is better to select draw.
            }
            return maxIdx;
        }

        float EstimateReward(BoardFeature feature)
        {
            AtomicOperations.Increment(ref this.ppsCounter);
            return 1.0f - this.VALUE_FUNC.F(feature);
        }

        void UpdateResult(Node node, int childNodeIdx, double reward)
        {
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VisitCount, 1 - this.VIRTUAL_LOSS);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].RewardSum, reward);
            AtomicOperations.Add(ref node.VisitCount, 1 - this.VIRTUAL_LOSS);
        }

        void AddVirtualLoss(Node node, int childNodeIdx)
        {
            AtomicOperations.Add(ref node.VisitCount, this.VIRTUAL_LOSS);
            AtomicOperations.Add(ref node.Edges[childNodeIdx].VisitCount, this.VIRTUAL_LOSS);
        }

        static void LabelEdge(ref Edge edge, GameInfo gameInfo)
        {
            var result = gameInfo.GetGameResult();
            if (result != GameResult.NotOver)
                edge.Label = GAME_RESULT_TO_EDGE_LABEL[-(int)result + 1];
        }

        ReadOnlyCollection<Move> GetPrincipalVariation(Node root, Edge edgeToRoot, DiscColor rootSide)
        {
            var pv = new List<Move>();
            addMovesToPV(root, edgeToRoot, rootSide);
            return new ReadOnlyCollection<Move>(pv);

            void addMovesToPV(Node node, Edge edge, DiscColor side)
            {
                if (node.Edges is null)
                    return;

                var maxIdx = 0;
                for (var i = 0; i < node.Edges.Length; i++)
                    if (node.Edges[i].VisitCount > node.Edges[maxIdx].VisitCount)
                        maxIdx = i;
                pv.Add(new Move(side, node.Edges[maxIdx].Pos));
                if (node.ChildNodes is not null && node.ChildNodes[maxIdx] is not null)
                    addMovesToPV(node.ChildNodes[maxIdx], node.Edges[maxIdx], FastBoard.GetOpponentColor(side));
            }
        }
    }
}
