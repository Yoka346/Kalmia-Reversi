using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Kalmia.MCTS;
using Kalmia.Evaluation;
using Kalmia.EndGameSolver;
using Kalmia.GoTextProtocol;
using Kalmia.IO;
using Kalmia.Reversi;

namespace Kalmia.Engines
{
    /// <summary>
    /// Configuration of Kalmia Engine.
    /// </summary>
    public class KalmiaConfig
    {
        /// <summary>
        /// The number of search iteration count.
        /// </summary>
        public uint SearchCount { get; set; } = 300000;

        /// <summary>
        /// The number of search iteration count for fast search.
        /// Fast search is used for move ordering in end game solve and stopgap move generation when timeout.
        /// </summary>
        public uint FastSearchCount { get; set; } = 3200;

        /// <summary>
        /// If true, uses maximum time for move, else uses time for move wisely.
        /// </summary>
        public bool UseMaxTimeForMove { get; set; } = false;

        /// <summary>
        /// Time limit will be decreased by this value.
        /// </summary>
        public int LatencyCentiSec { get; set; } = 50;

        /// <summary>
        /// The number of opening moves. When the number of moves is within this value, Kalmia plays moves fastly.
        /// </summary>
        public int OpenningMoveNum { get; set; } = 15;

        /// <summary>
        /// Whether selects next move stochastically or not.
        /// </summary>
        public bool SelectMoveStochastically { get; set; }

        /// <summary>
        /// The number of stochastic move. After the number of move would be this value, Kalmia plays best move.
        /// </summary>
        public int StochasticMoveNum { get; set; } = int.MaxValue;

        /// <summary>
        /// The softmax temperature when selecting next move stochastically.
        /// </summary>
        public float SoftmaxTemperture { get; set; } = 1.0f;

        /// <summary>
        /// Whether reuses previous search result or not.
        /// </summary>
        public bool ReuseSubtree { get; set; } = true;

        /// <summary>
        /// Whether continue searching on opponent turn.
        /// </summary>
        public bool EnablePondering { get; set; }

        /// <summary>
        /// The path of value function parameter file.
        /// </summary>
        public string ValueFuncParamFile { get; set; }

        /// <summary>
        /// The search options.
        /// </summary>
        public UCTOptions TreeOptions { get; set; } = new();

        /// <summary>
        /// When the number of empty squares is less than or equal this value, the mate solver will be executed.
        /// </summary>
        public int MateSolverMoveNum { get; set; } = 21;

        /// <summary>
        /// When the number of empty squares is less than or equal this value,
        /// the final disc difference solver will be executed.
        /// </summary>
        public int FinalDiscDifferenceSolverMoveNum { get; set; } = 20;

        /// <summary>
        /// The size of memory for the end game solver's transposition table.
        /// The end game solver means the mate solver and the final disc difference solver.
        /// </summary>
        public ulong EndgameSolverMemorySize { get; set; } = 256 * 1024 * 1024; // 256MiB

        internal int EndGameMoveNum { get => Math.Max(this.MateSolverMoveNum, this.FinalDiscDifferenceSolverMoveNum); }
    }

    /// <summary>
    /// Provides reversi engine.
    /// </summary>
    public class KalmiaEngine : GTPEngine
    {
        const string _NAME = "Kalmia";
        const string _VERSION = "1.0";

        readonly Random RAND = new(Random.Shared.Next());
        readonly ValueFunction VALUE_FUNC;

        UCT tree;
        SearchInfo lastSearchInfo;
        MateSolver mateSolver;
        FinalDiscDifferenceSolver diskDiffSolver;
        Logger thoughtLog;
        TimeController timeController;
        bool quitFlag;
        Task searchTask;

        public KalmiaConfig Config { get; }
        public int SearchEllapsedMilliSec { get { return this.tree.SearchEllapsedMilliSec; } }
        public SearchInfo SearchInfo { get { return this.tree.IsSearching ? this.tree.SearchInfo : this.lastSearchInfo; } }

        bool timeControlEnabled { get { return this.timeController is not null; } set { if (!value) this.timeController = null; } }

        public KalmiaEngine(KalmiaConfig config):this(config, string.Empty) { }

        public KalmiaEngine(KalmiaConfig config, string logFilePath):base(_NAME, _VERSION)
        {
            this.Config = config;
            this.tree = new UCT(config.TreeOptions, this.VALUE_FUNC = new ValueFunction(config.ValueFuncParamFile));
            this.mateSolver = new MateSolver(config.EndgameSolverMemorySize);
            this.diskDiffSolver = new FinalDiscDifferenceSolver(config.EndgameSolverMemorySize);
            this.timeController = new TimeController(0, 1, 0, 0, config.LatencyCentiSec);
            this.thoughtLog = new Logger(logFilePath, Console.OpenStandardError());
            ClearBoard();
        }

        public override void Quit()
        {
            this.quitFlag = true;
            if (this.tree.IsSearching)
            {
                this.tree.RequestToStopSearch();
                this.thoughtLog.WriteLine($"Kalmia recieved quit signal. Current calculation will be suspended.");
            }
            this.thoughtLog.Dispose();
        }

        public override void ClearBoard()
        {
            base.ClearBoard(); 
            if (this.searchTask is not null && !this.searchTask.IsCompleted)
                StopPondering();
            this.tree.SetRoot(this.board);
            this.timeController.Reset();
            this.thoughtLog.WriteLine("Tree was initialized.\n");
        }

        public override bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public override bool Play(Move move)
        {
            if (!base.Play(move))
                return false;

            if(this.searchTask is not null && !this.searchTask.IsCompleted)
            {
                StopPondering();
                this.thoughtLog.WriteLine("Stop pondering.");
                this.lastSearchInfo = this.tree.SearchInfo;
                this.thoughtLog.WriteLine(GetSearchInfoString());
            }

            if (!this.tree.TransitionToRootChildNode(move))
                this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine($"Opponent's move is {move}");
            this.thoughtLog.Flush();
            return true;
        }

        public override bool Undo()
        {
            if (!base.Undo())
                return false;

            if (this.searchTask is not null && !this.searchTask.IsCompleted)
                StopPondering();

            this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine("Undo\n");
            this.thoughtLog.WriteLine("Tree was cleared.\n");
            return true;
        }

        public override Move GenerateMove(DiscColor color)
        {
            if (this.searchTask != null && !this.searchTask.IsCompleted)
            {
                StopPondering();
                this.thoughtLog.WriteLine("Stop pondering.");
                this.lastSearchInfo = this.tree.SearchInfo;
                this.thoughtLog.WriteLine(GetSearchInfoString());
            }

            var move = RegGenerateMove(color);
            this.board.Update(move);
            if ((!this.Config.ReuseSubtree && !this.Config.EnablePondering) || !this.tree.TransitionToRootChildNode(move))
            { 
                this.tree.SetRoot(this.board);
                this.thoughtLog.WriteLine("Tree was initialized");
            }

            if (this.Config.EnablePondering && this.board.GetGameResult(DiscColor.Black) == GameResult.NotOver)
                StartPondering();
            return move;
        }

        public override Move RegGenerateMove(DiscColor color)
        {
            if (this.board.SideToMove != color)
            {
                this.board.SwitchSideToMove();
                this.tree.SetRoot(this.board);
                this.thoughtLog.WriteLine("Tree was initialized\n");
            }

            var nextMoves = this.board.GetNextMoves();
            if (nextMoves.Length == 1)
                return nextMoves[0];

            var emptyCount = this.board.GetEmptyCount();
            if(emptyCount > this.Config.EndGameMoveNum)
                return GenerateMidGameMove(color);
            return GenerateEndGameMove(color);
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {
            if(mainTime == 0 && byoYomiTime == 0 && byoYomiStones == 0)
            {
                this.timeControlEnabled = false;
                return;
            }
            this.timeController = new TimeController(mainTime, byoYomiTime, byoYomiStones, 0, this.Config.LatencyCentiSec);
        }

        public override void SendTimeLeft(DiscColor color, int timeLeft, int byoYomiStonesLeft)
        {
            if (this.timeControlEnabled)
                this.timeController.AdjustTime(color, timeLeft, byoYomiStonesLeft);
            else
                throw new GTPException("Set main time before adjusting time left.");
        }

        public override string LoadSGF(string path)
        {
            var ret = base.LoadSGF(path);
            this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine("Tree was initialized.");
            return ret;
        }

        public override string LoadSGF(string path, int moveCount)
        {
            var ret = base.LoadSGF(path, moveCount);
            this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine("Tree was initialized.");
            return ret;
        }

        public override string LoadSGF(string path, int posX, int posY)
        {
            var ret = base.LoadSGF(path, posX, posY);
            this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine("Tree was initialized.");
            return ret;
        }

        void StartPondering()
        {
            this.searchTask = this.tree.SearchAsync(int.MaxValue, int.MaxValue / 10);
        }

        void StopPondering()
        {
            this.tree.RequestToStopSearch();
            this.searchTask.Wait();
        }

        void WaitForSearch(DiscColor color, int timeLimitCentiSec)
        {
            while (this.tree.IsSearching)
            {
                Thread.Sleep(10);
                if (!IsFurtherSearchNecessary(color, timeLimitCentiSec))
                {
                    this.tree.RequestToStopSearch();
                    break;
                }
            }
            this.searchTask.Wait();
        }

        bool IsFurtherSearchNecessary(DiscColor color, int timeLimitCentiSec)
        {
            if (this.timeController.GetEllapsedCentiSec(color) < timeLimitCentiSec * 0.1)
                return true;
            var searchInfo = this.SearchInfo;
            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(e => e.PlayoutCount).ToArray();
            var playoutDiff = childEvals[1].PlayoutCount - childEvals[0].PlayoutCount;
            return playoutDiff < this.tree.Pps * (timeLimitCentiSec * 10 - this.tree.SearchEllapsedMilliSec) * 1.0e-3f;
        }

        Move GenerateMidGameMove(DiscColor color)
        {
            var moveNum = Board.SQUARE_NUM - this.board.GetEmptyCount();
            this.timeController.Start(color);
            var timeLimit = this.timeController.GetMaxTimeCentiSecForMove(color, this.board.GetEmptyCount());
            if (moveNum < this.Config.OpenningMoveNum)
                timeLimit /= 2;
            this.searchTask = this.tree.SearchAsync(this.Config.SearchCount, timeLimit);
            WaitForSearch(color, timeLimit);
            var searchInfo = this.tree.SearchInfo;
            this.lastSearchInfo = searchInfo;

            this.thoughtLog.WriteLine(GetSearchInfoString());

            var move = this.Config.SelectMoveStochastically && moveNum < this.Config.StochasticMoveNum
                       ? SelectMoveStochastically(searchInfo, out bool additionalSearchRequired)
                       : SelectBestMove(searchInfo, out additionalSearchRequired);

            if (moveNum >= this.Config.OpenningMoveNum && additionalSearchRequired && !this.timeController.InByoYomi(color))
            {
                timeLimit += timeLimit - this.timeController.GetEllapsedCentiSec(color);
                if (this.timeController.RemainingTimeCentiSec[(int)color] > timeLimit * 1.5)
                {
                    this.thoughtLog.WriteLine("\n\nAdditional search is required.");
                    this.thoughtLog.Flush();
                    this.searchTask = this.tree.SearchAsync(this.Config.SearchCount, timeLimit);
                    WaitForSearch(color, timeLimit);
                    searchInfo = this.tree.SearchInfo;
                    this.lastSearchInfo = searchInfo;
                    move = this.Config.SelectMoveStochastically && moveNum < this.Config.StochasticMoveNum
                           ? SelectMoveStochastically(searchInfo, out _)
                           : SelectBestMove(searchInfo, out _);
                    this.thoughtLog.WriteLine(GetSearchInfoString());
                }
            }

            this.thoughtLog.Flush();
            this.timeController.Stop(color);
            return move;
        }

        Move GenerateEndGameMove(DiscColor color)
        {
            this.timeController.Start(color);

            int timeLimitCentiSec;
            if (this.timeController.IsUnlimitedTime)
                timeLimitCentiSec = int.MaxValue / 10;
            else if (this.timeController.InByoYomi(color))
                timeLimitCentiSec = this.timeController.GetMaxTimeCentiSecForMove(color, this.board.GetEmptyCount());
            else
                timeLimitCentiSec = (int)(this.timeController.RemainingTimeCentiSec[(int)color] * 0.7);

            IEndGameSolver solver;
            BoardPosition movePos;
            string resultStr;
            bool timeout;
            var foundWin = false;

            if (this.board.GetEmptyCount() > this.Config.FinalDiscDifferenceSolverMoveNum)
            {
                this.thoughtLog.WriteLine("Execute mate solver.");
                this.thoughtLog.Flush();
                solver = this.mateSolver;
                movePos = this.mateSolver.SolveBestMove(this.board.GetFastBoard(), timeLimitCentiSec, out GameResult result, out timeout);
                resultStr = result.ToString();
            }
            else
            {
                this.thoughtLog.WriteLine("Execute final disc difference solver.");
                this.thoughtLog.Flush();
                solver = this.diskDiffSolver;
                movePos = this.diskDiffSolver.SolveBestMove(this.board.GetFastBoard(), timeLimitCentiSec, out sbyte result, out timeout);
                if (result > 0)
                {
                    resultStr = $"+{result}";
                    foundWin = true;
                }
                else
                    resultStr = result.ToString();
                if (timeout)
                    resultStr += "(LCB)";
            }

            Move move;
            if (timeout && !foundWin)    // When timeout, executes stopgap move generation by fast MCTS. 
            {
                this.thoughtLog.WriteLine("timeout!!");
                this.thoughtLog.WriteLine("Select move by fast MCTS.");
                this.tree.Search(this.Config.FastSearchCount);
                var searchInfo = this.lastSearchInfo = this.tree.SearchInfo;
                this.thoughtLog.WriteLine(GetSearchInfoString());
                move = SelectBestMove(searchInfo, out _);
            }
            else
            {
                this.thoughtLog.WriteLine($"ellapsed = {solver.SearchEllapsedMilliSec}[ms]   nps = {solver.Nps}[nps]");
                this.thoughtLog.WriteLine($"Final result is {resultStr}.");
                move = new Move(this.board.SideToMove, movePos);
            }
            this.thoughtLog.Flush();
            this.timeController.Stop(color);
            return move;
        }

        Move SelectBestMove(SearchInfo searchInfo, out bool additionalSearchIsRequired)
        {
            if (searchInfo.ChildEvaluations.Count == 1)
            {
                additionalSearchIsRequired = false;
                return searchInfo.ChildEvaluations[0].Move;
            }
            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(key => key.PlayoutCount).ToArray();
            (var first, var second) = (childEvals[0], childEvals[1]);
            additionalSearchIsRequired = !first.IsProved && (second.Value > first.Value || first.PlayoutCount < second.PlayoutCount * 1.5);
            return first.Move;
        }

        Move SelectMoveStochastically(SearchInfo searchInfo, out bool additionalSearchIsRequired)
        {
            if (searchInfo.ChildEvaluations.Count == 1)
            {
                additionalSearchIsRequired = false;
                return searchInfo.ChildEvaluations[0].Move;
            }

            var tInverse = 1.0f / this.Config.SoftmaxTemperture;
            var expPlayoutCount = (from e in searchInfo.ChildEvaluations select MathF.Exp(e.PlayoutCount)).ToArray();
            var prob = new float[expPlayoutCount.Length];
            for(var i = 0; i < expPlayoutCount.Length; i++)
                prob[i] = 1.0f / (from ep in expPlayoutCount select MathF.Pow(ep / expPlayoutCount[i], tInverse)).Sum();

            var arrow = this.RAND.NextSingle();
            var sum = 0.0;
            var j = -1;
            while ((sum += prob[++j]) < arrow) ;

            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(key => key.PlayoutCount).ToArray();
            (var first, var second) = (childEvals[0], childEvals[1]);
            additionalSearchIsRequired = second.Value > first.Value || second.PlayoutCount < first.PlayoutCount * 1.5;

            return searchInfo.ChildEvaluations[j].Move;
        }

        string GetSearchInfoString()
        {
            var searchInfo = this.SearchInfo;
            var sb = new StringBuilder($"ellapsed={this.SearchEllapsedMilliSec}[ms] {searchInfo.RootEvaluation.PlayoutCount}[playouts] {this.tree.Pps}[pps] winning_rate={searchInfo.RootEvaluation.Value * 100.0f:f2}%\n");
            sb.AppendLine("|move|search_count|winnning_rate|probability|depth|pv");
            foreach(var childEval in searchInfo.ChildEvaluations.OrderByDescending(n => n.PlayoutCount))
            {
                sb.Append($"| {childEval.Move} |{childEval.PlayoutCount,12}|{childEval.Value * 100.0f,12:f2}%|{childEval.MoveProbability * 100.0f,10:f2}%|{childEval.PrincipalVariation.Count,5}|");
                foreach (var move in childEval.PrincipalVariation) 
                        sb.Append($"{move} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
