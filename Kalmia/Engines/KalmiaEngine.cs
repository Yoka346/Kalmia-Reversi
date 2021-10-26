using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Kalmia.Evaluation;
using Kalmia.GoTextProtocol;
using Kalmia.IO;
using Kalmia.MCTS;
using Kalmia.Reversi;

namespace Kalmia.Engines
{
    public struct KalmiaConfig 
    {
        public int SimulationCount { get; set; }
        public bool SetTimeLimit { get; set; }
        public int TimeLimit { get; set; }
        public bool DoAdditionalSearch { get; set; }
        public int AdditionalSimulationCount { get; set; }
        public int AdditionalTimeLimit { get; set; }
        public bool SelectMoveByProbability { get; set; }
        public float Temperature { get; set; }
        public float UCBFactorInit { get; set; }
        public float UCBFactorBase { get; set; }
        public bool ReuseSubTree { get; set; }
        public bool EnablePondering { get; set; }
        public int MaxNodeCount { get; set; }
        public string ValueFuncParamsFilePath { get; set; }
        public int ThreadNum { get; set; }

        public KalmiaConfig(string jsonString)
        {
            this = (KalmiaConfig)JsonSerializer.Deserialize(jsonString, typeof(KalmiaConfig));
            if (this.ThreadNum == 0)
                this.ThreadNum = Environment.ProcessorCount;
        }
    }

    public class KalmiaEngine : GTPEngine
    {
        new const string NAME = "Kalmia";
        new const string VERSION = "Prototype";

        readonly ReadOnlyDictionary<string, Func<string[], string>> COMMANDS;
        readonly KalmiaConfig CONFIG;

        UCT tree;
        Move lastMove;
        ValueFunction valueFunc;
        PositionEval[] lastGenerateMoveResult;     // next positions evaluations that were calculated by the latest genmove.

        Task searchTask;
        CancellationTokenSource cts;
        Logger thoughtLog;

        public int SearchedBoardCount { get { return this.tree.VisitedEdgeCount; } }

        public KalmiaEngine(KalmiaConfig config, string logFilePath) : base(NAME, VERSION)
        {
            this.COMMANDS = new ReadOnlyDictionary<string, Func<string[], string>>(InitCommands());
            this.CONFIG = config;
            this.valueFunc = new ValueFunction(config.ValueFuncParamsFilePath);
            this.tree = new UCT(this.valueFunc, config.UCBFactorInit, config.UCBFactorBase, config.MaxNodeCount, config.ThreadNum);
            this.lastMove = new Move(Color.Black, BoardPosition.Null);
            this.thoughtLog = new Logger(logFilePath, false);
        }

        Dictionary<string, Func<string[], string>> InitCommands()
        {
            var commands = new Dictionary<string, Func<string[], string>>();
            commands.Add("benchmark_nps", ExecuteBenchmarkNPSCommand);
            commands.Add("benchmark_pps", ExecuteBenchmarkPPSCommand);
            return commands;
        }

        public float GetValueFunctionEvaluation()
        {
            return this.valueFunc.F(new BoardFeature(new FastBoard(this.board)));
        }

        public PositionEval GetCurrentBoardEvaluation()
        {
            return this.tree.GetRootNodeEvaluation();
        }

        public PositionEval[] GetNextPositionsEvaluation()
        {
            return this.tree.GetChildNodeEvaluations().ToArray();
        }

        public PositionEval[] GetLastGenerateMoveResult()
        {
            if (this.lastGenerateMoveResult == null)
                return new PositionEval[0];
            return (PositionEval[])this.lastGenerateMoveResult.Clone();
        }

        public override void Quit()
        {
            if (this.thoughtLog != null && !this.thoughtLog.IsDisposed)
                this.thoughtLog.Dispose();
        }

        public override void ClearBoard()
        {
            base.ClearBoard();
            this.tree.Clear();
            this.lastMove = new Move(Color.Black, BoardPosition.Null);
            this.thoughtLog.WriteLine("\nclear board and tree.\n");
        }

        public override bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public override bool Play(Move move)
        {
            if (base.Play(move))
            {
                if (this.CONFIG.EnablePondering && this.searchTask != null)
                {
                    if(!this.searchTask.IsCompleted)
                        StopPondering();
                    var rootEval = this.tree.GetRootNodeEvaluation();
                    var childEvals = this.tree.GetChildNodeEvaluations().ToArray();
                    this.thoughtLog.WriteLine(SearchInfoToString(rootEval, childEvals));
                }
                this.tree.SetRoot(move.Pos);

                this.thoughtLog.WriteLine($"opponent's move is {move}\n\n");

                this.lastMove = move;
                return true;
            }
            return false;
        }

        public override bool Undo()
        {
            if (base.Undo())
            {
                if (this.CONFIG.EnablePondering && this.searchTask != null && !this.searchTask.IsCompleted)
                    StopPondering();
                this.tree.Clear();
                this.lastMove = new Move(Color.Black, BoardPosition.Null);
                this.thoughtLog.WriteLine("\nundo.\n");
                return true;
            }
            return false;
        }

        public override Move GenerateMove(Color color)
        {
            if (this.CONFIG.EnablePondering && this.searchTask != null && !this.searchTask.IsCompleted)
                StopPondering();

            var move = RegGenerateMove(color);
            this.board.Update(move);
            this.lastMove = move;
            this.lastGenerateMoveResult = GetNextPositionsEvaluation();
            if (this.CONFIG.ReuseSubTree || this.CONFIG.EnablePondering)
                this.tree.SetRoot(this.lastMove.Pos);
            else
                this.tree.Clear();

            if (this.CONFIG.EnablePondering && this.board.GetGameResult(Color.Black) == GameResult.NotOver)
                StartPondering();
            return move;
        }

        public override Move RegGenerateMove(Color color)
        {
            if (this.board.SideToMove != color)
            {
                this.board.SwitchSideToMove();
                this.tree.Clear();
            }

            this.tree.SetRoot(this.lastMove.Pos);

            var moves = this.board.GetNextMoves();
            if (moves.Length == 1)
                return moves[0];

            this.tree.Search(this.board, this.CONFIG.SimulationCount, this.CONFIG.SetTimeLimit ? this.CONFIG.TimeLimit : int.MaxValue);

            PositionEval rootEval;
            PositionEval[] childEvals;
            if (this.CONFIG.DoAdditionalSearch && CheckIfAdditionalSearchIsRequired())
            {
                rootEval = this.tree.GetRootNodeEvaluation();
                childEvals = this.tree.GetChildNodeEvaluations().ToArray();
                this.thoughtLog.WriteLine(SearchInfoToString(rootEval, childEvals));
                this.thoughtLog.WriteLine($"kalmia decides to do additional search.");
                this.tree.Search(this.board, this.CONFIG.AdditionalSimulationCount,
                                 this.CONFIG.SetTimeLimit ? Math.Max(this.CONFIG.TimeLimit - this.tree.SearchEllapsedTime, 0) + this.CONFIG.AdditionalTimeLimit : int.MaxValue);
            }
            BoardPosition pos = (this.CONFIG.SelectMoveByProbability) ? this.tree.SelectNextPositionByMoveProbability(this.CONFIG.Temperature)
                                                                      : this.tree.SelectMaxVisitCountAndMaxValuePosition();
            rootEval = this.tree.GetRootNodeEvaluation();
            childEvals = this.tree.GetChildNodeEvaluations().ToArray();
            this.thoughtLog.WriteLine(SearchInfoToString(rootEval, childEvals));
            this.thoughtLog.WriteLine($"kalmia selects {pos}.");
            return new Move(this.board.SideToMove, pos);
        }

        public override string LoadSGF(string path)
        {
            return "not surported.";
        }

        public override string LoadSGF(string path, int posX, int posY)
        {
            return "not surported.";
        }

        public override string LoadSGF(string path, int moveCount)
        {
            return "not surported.";
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {

        }

        public override void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {

        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            return this.COMMANDS[command](args);
        }

        public override string[] GetOriginalCommands()
        {
            return this.COMMANDS.Keys.ToArray();
        }

        string ExecuteBenchmarkNPSCommand(string[] args)
        {
            try
            {
                var sampleNum = int.Parse(args[0]);
                var simulationCount = int.Parse(args[1]);
                var boards = GenerateBoards(sampleNum);
                var npsSum = 0.0f;
                foreach (var board in boards)
                {
                    this.tree.SetRoot(BoardPosition.Null);
                    this.tree.Search(board, simulationCount);
                    npsSum += this.tree.Nps;
                }
                return $"{npsSum / sampleNum} [nps]";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                return "? invalid option.";
            }
        }

        string ExecuteBenchmarkPPSCommand(string[] args)
        {
            try
            {
                var sampleNum = int.Parse(args[0]);
                var simulationCount = int.Parse(args[1]);
                var boards = GenerateBoards(sampleNum);
                var ppsSum = 0.0f;
                foreach (var board in boards)
                {
                    this.tree.SetRoot(BoardPosition.Null);
                    this.tree.Search(board, simulationCount);
                    ppsSum += this.tree.Pps;
                }
                return $"{ppsSum / sampleNum} [pps]";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                return "? invalid option.";
            }
        }

        void StartPondering()
        {
            this.cts = new CancellationTokenSource();
            this.searchTask = this.tree.SearchAsync(this.board, int.MaxValue, int.MaxValue, this.cts.Token);
        }

        void StopPondering()
        {
            this.cts.Cancel();
            this.searchTask.Wait();
        }

        bool CheckIfAdditionalSearchIsRequired()
        {
            // If the playout count of the most visited child node is less than SEARCH_EXTEND_CRITERIA times of the playout count of the second visited node, additional searching is required.
            const double SEARCH_EXTEND_CRITERIA = 1.2;  

            var childNodeEvals = this.tree.GetChildNodeEvaluations().ToArray();
            var first = childNodeEvals[0];
            PositionEval? second = null; 
            for(var i = 1; i < childNodeEvals.Length; i++)
            {
                var childNodeEval = childNodeEvals[i];
                if (childNodeEval.PlayoutCount > first.PlayoutCount)
                {
                    if (!second.HasValue)
                        second = first;
                    first = childNodeEval;
                }
                else if (!second.HasValue || childNodeEval.PlayoutCount > second.Value.PlayoutCount)
                    second = childNodeEval;
            }
            return first.PlayoutCount < second.Value.PlayoutCount * SEARCH_EXTEND_CRITERIA;
        }

        string SearchInfoToString(PositionEval rootEval, PositionEval[] childrenEval)
        {
            var sb = new StringBuilder($"ellapsed={this.tree.SearchEllapsedTime}[ms] {this.tree.PlayoutCount}[playouts] {this.tree.Pps}[pps] {this.tree.NodeCount}[nodes] {this.tree.VisitedEdgeCount}[boards] {this.tree.Nps}[nps] winning_rate={rootEval.Value * 100.0f}%\n");
            sb.AppendLine("|move|search_count|winnning_rate|probability|depth|pv");
            for(var i = 0; i < childrenEval.Length; i++)
            {
                var posEval = childrenEval[i];
                var pv = posEval.PV;
                sb.Append($"| {posEval.Position} |{posEval.PlayoutCount,12}|{posEval.Value * 100.0f,12:f2}%|{posEval.MoveProbability * 100.0f,10:f2}%|{pv.Count,5}|");
                foreach (var move in pv.Select(n => n.Position))
                    sb.Append($"{move} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static Board[] GenerateBoards(int count)
        {
            const int MAX_DISC_COUNT = 36;

            var boards = new Board[count];
            var rand = new Xorshift();
            for(var i = 0; i < count; i++)
            {
                var n = rand.Next(MAX_DISC_COUNT);
                var board = new Board(Color.Black, InitialBoardState.Cross);
                var moveCount = 0;
                while(moveCount < n && board.GetGameResult(Color.Black) == GameResult.NotOver)
                {
                    var moves = board.GetNextMoves();
                    board.Update(moves[rand.Next((uint)moves.Length)]);
                    moveCount++;
                }
                boards[i] = board;
            }
            return boards;
        }
    }
}
