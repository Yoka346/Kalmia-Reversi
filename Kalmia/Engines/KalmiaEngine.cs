using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

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
    public struct KalmiaConfig
    {
        /// <summary>
        /// The number of search iteration count.
        /// </summary>
        public uint SearchCount { get; set; } = 300000;

        /// <summary>
        /// If true, uses maximum time for move, else uses time for move wisely.
        /// </summary>
        public bool UseMaxTimeForMove { get; set; } = false;

        /// <summary>
        /// Time limit will be decreased by this value.
        /// </summary>
        public int LatencyCentiSec { get; set; } = 100;

        /// <summary>
        /// The number of opening moves. When the number of moves is within this value, Kalmia plays moves fastly.
        /// </summary>
        public int OpenningMoveNum { get; set; } = 15;

        /// <summary>
        /// The empty count of the board when mate solver would be executed.
        /// </summary>
        public int MateSolverMoveNum { get; set; } = 22;

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
        /// The size of memory for end game solver's transposition table.
        /// </summary>
        public ulong MateSolverMemorySize { get; set; } = 256 * 1024 * 1024; // 256MiB
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
        Task searchTask;
        MateSolver mateSolver;
        Logger thoughtLog;
        TimeController timeController;
        SearchInfo lastGenMoveSearchInfo;
        bool quitFlag;

        public KalmiaConfig Config { get; }
        public int SearchEllapsedMilliSec { get { return this.tree.SearchEllapsedMilliSec; } }
        public SearchInfo SearchInfo { get { return this.tree.IsSearching ? this.tree.SearchInfo : this.lastGenMoveSearchInfo; } }

        bool timeControlEnabled { get { return this.timeController is not null; } set { if (!value) this.timeController = null; } }

        public KalmiaEngine(KalmiaConfig config):this(config, string.Empty) { }

        public KalmiaEngine(KalmiaConfig config, string logFilePath):base(_NAME, _VERSION)
        {
            this.Config = config;
            this.tree = new UCT(config.TreeOptions, this.VALUE_FUNC = new ValueFunction(config.ValueFuncParamFile));
            this.mateSolver = new MateSolver(this.Config.MateSolverMemorySize);
            this.timeController = new TimeController(0, 1, 0, 0, config.LatencyCentiSec);
            this.thoughtLog = new Logger(logFilePath, Console.OpenStandardError());
            ClearBoard();
        }

        public override void Quit()
        {
            this.quitFlag = true;
            if (this.tree.IsSearching)
            {
                this.tree.RequestToStopSearching();
                this.thoughtLog.WriteLine($"Kalmia recieved quit signal. Current calculation will be suspended.");
            }
            this.thoughtLog.Dispose();
        }

        public override void ClearBoard()
        {
            base.ClearBoard();
            this.tree.SetRoot(this.board);
            this.mateSolver.ClearSearchResults();
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
                this.thoughtLog.WriteLine(GetSearchInfoString());
            }

            if (!this.tree.TransitionToRootChildNode(move))
                this.tree.SetRoot(this.board);
            this.thoughtLog.WriteLine($"Opponent's move is {move}");
            return true;
        }

        public override bool Undo()
        {
            if (!base.Undo())
                return false;

            if (this.searchTask is not null && !this.searchTask.IsCompleted)
                StopPondering();

            this.tree.SetRoot(this.board);
            this.mateSolver.ClearSearchResults();
            this.thoughtLog.WriteLine("Undo\n");
            this.thoughtLog.WriteLine("Tree was cleared.\n");
            return true;
        }

        public override Move GenerateMove(DiscColor color)
        {
            if (this.searchTask != null && !this.searchTask.IsCompleted)
                StopPondering();

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
            if(this.board.SideToMove != color)
            {
                this.board.SwitchSideToMove();
                this.tree.SetRoot(this.board);
                this.thoughtLog.WriteLine("Tree was initialized\n");
            }

            var nextMoves = this.board.GetNextMoves();
            if (nextMoves.Length == 1)
                return nextMoves[0];

            if (this.board.GetEmptyCount() > this.Config.MateSolverMoveNum)
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
            this.timeController = new TimeController(mainTime, byoYomiTime, byoYomiStones, 0, this.Config.LatencyCentiSec / 10);
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
            this.searchTask = this.tree.SearchAsync(int.MaxValue, int.MaxValue);
        }

        void StopPondering()
        {
            this.tree.RequestToStopSearching();
            this.searchTask.Wait();
        }

        Move GenerateMidGameMove(DiscColor color)
        {
            this.timeController.Start(color);
            var timeLimit = this.timeController.GetMaxTimeCentiSecForMove(color, this.board.GetEmptyCount()) * 10;
            this.tree.Search(this.Config.SearchCount, this.Config.UseMaxTimeForMove || this.timeController.InByoYomi(color) ? timeLimit : timeLimit / 2);
            var searchInfo = this.tree.SearchInfo;
            this.lastGenMoveSearchInfo = searchInfo;

            var moveNum = Board.SQUARE_NUM - this.board.GetEmptyCount();
            var move = this.Config.SelectMoveStochastically && (Board.SQUARE_NUM - this.board.GetEmptyCount()) < this.Config.StochasticMoveNum
                       ? SelectMoveStochastically(searchInfo, out bool additionalSearchRequired)
                       : SelectBestMove(searchInfo, out additionalSearchRequired);

            if (additionalSearchRequired && !this.Config.UseMaxTimeForMove && !this.timeController.InByoYomi(color) && moveNum >= this.Config.OpenningMoveNum)
            {
                this.thoughtLog.WriteLine("Additional search is required.");
                this.thoughtLog.Flush();
                this.tree.Search(this.Config.SearchCount, timeLimit / 2);
                searchInfo = this.tree.SearchInfo;
                this.lastGenMoveSearchInfo = searchInfo;
                move = this.Config.SelectMoveStochastically && moveNum < this.Config.StochasticMoveNum
                       ? SelectMoveStochastically(searchInfo, out _)
                       : SelectBestMove(searchInfo, out _);
            }
            this.timeController.Stop(color);

            this.thoughtLog.WriteLine(GetSearchInfoString());
            this.thoughtLog.Flush();
            return move;
        }

        Move GenerateEndGameMove(DiscColor color)
        {
            this.timeController.Start(color);

            this.thoughtLog.WriteLine("Execute mate solver.");
            this.thoughtLog.Flush();

           var timeLimit = this.timeController.GetMaxTimeCentiSecForMove(color, this.board.GetEmptyCount()) * 10;   // ToDo: This is not enough time. considers the other way.
            var movePos = this.mateSolver.SolveBestMove(this.board.GetFastBoard(), timeLimit, out GameResult result, out bool timeout);

            Move move;
            if (timeout)     // if timeout, selects the maximum value move with value function.
            {
                this.thoughtLog.WriteLine("timeout!!");
                this.thoughtLog.WriteLine("Select move by one move ahead search.");
                move = GenerateMoveByOneMoveAheadSearch();
            }
            else
            {
                this.thoughtLog.WriteLine($"ellapsed = {this.mateSolver.SearchEllapsedMilliSec}[ms]   nps = {this.mateSolver.Nps}[nps]");
                this.thoughtLog.WriteLine($"Final result is {result}.");
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

            var arrow = this.RAND.NextDouble();
            var sum = 0.0;
            var i = -1;
            while ((sum += searchInfo.ChildEvaluations[++i].MoveProbability) < arrow) ;

            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(key => key.PlayoutCount).ToArray();
            (var first, var second) = (childEvals[0], childEvals[1]);
            additionalSearchIsRequired = second.Value > first.Value || second.PlayoutCount < first.PlayoutCount * 1.5;

            return searchInfo.ChildEvaluations[i].Move;
        }

        Move GenerateMoveByOneMoveAheadSearch()
        {
            var board = this.board.GetFastBoard();
            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            var evals = new (BoardPosition pos, float value)[posNum];

            for(var i = 0; i < posNum; i++)
            {
                var bitboard = board.GetBitboard();
                board.Update(positions[i]);
                evals[i] = (positions[i], this.VALUE_FUNC.F(new BoardFeature(board)));
                board.SetBitboard(bitboard);
            }

            evals = evals.OrderByDescending(e => e.value).ToArray();
            var sb = new StringBuilder("|move|winning_rate");
            foreach (var eval in evals)
            {
                var pos = (GTP.CoordinateRule != GTPCoordinateRule.Othello) ? GTP.ConvertCoordinateRule(eval.pos) : eval.pos;
                sb.AppendLine($"| {pos} |{eval.value * 100.0f,12:f2}%");
            }
            this.thoughtLog.WriteLine(sb.ToString());
            return new Move(this.board.SideToMove, evals[0].pos);
        }

        string GetSearchInfoString()
        {
            var searchInfo = this.SearchInfo;
            var sb = new StringBuilder($"ellapsed={this.SearchEllapsedMilliSec}[ms] {searchInfo.RootEvaluation.PlayoutCount}[playouts] {this.tree.Pps}[pps] winning_rate={searchInfo.RootEvaluation.Value * 100.0f:f2}%\n");
            sb.AppendLine("|move|search_count|winnning_rate|probability|depth|pv");
            foreach(var childEval in searchInfo.ChildEvaluations.OrderByDescending(n => n.PlayoutCount))
            {
                var m = (GTP.CoordinateRule != GTPCoordinateRule.Othello) ? GTP.ConvertCoordinateRule(childEval.Move) : childEval.Move;
                sb.Append($"| {m} |{childEval.PlayoutCount,12}|{childEval.Value * 100.0f,12:f2}%|{childEval.MoveProbability * 100.0f,10:f2}%|{childEval.PrincipalVariation.Count,5}|");
                foreach (var move in childEval.PrincipalVariation) 
                {
                    if (GTP.CoordinateRule != GTPCoordinateRule.Othello)
                        sb.Append($"{GTP.ConvertCoordinateRule(move)} ");
                    else
                        sb.Append($"{move} ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
