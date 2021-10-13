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
        public float Temperature { get; set; }
        public float UCBFactor { get; set; }
        public bool ReuseSubTree { get; set; }
        public bool EnablePondering { get; set; }
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

        readonly int SIMULATION_COUNT;
        readonly bool PONDERING_ENABLED;
        readonly bool REUSE_SUBTREE;

        UCT tree;
        Move lastMove;
        ValueFunction valueFunc;
        Task searchTask;
        CancellationTokenSource cts;
        Logger thoughtLog;

        public KalmiaEngine(KalmiaConfig config, string logFilePath) : base(NAME, VERSION)
        {
            this.COMMANDS = new ReadOnlyDictionary<string, Func<string[], string>>(InitCommands());
            this.SIMULATION_COUNT = config.SimulationCount;
            this.valueFunc = new ValueFunction(config.ValueFuncParamsFilePath);
            this.tree = new UCT(this.valueFunc, config.UCBFactor, config.ThreadNum);
            this.tree.MoveProbabilityTemperature = config.Temperature;
            this.lastMove = new Move(Color.Black, BoardPosition.Null);
            this.REUSE_SUBTREE = config.ReuseSubTree;
            this.PONDERING_ENABLED = config.EnablePondering;
            if (this.PONDERING_ENABLED)
                this.REUSE_SUBTREE = true;
            this.thoughtLog = new Logger(logFilePath, false);
        }

        ~KalmiaEngine()
        {
            if (this.thoughtLog != null && !this.thoughtLog.IsDisposed)
                this.thoughtLog.Close();
        }

        Dictionary<string, Func<string[], string>> InitCommands()
        {
            var commands = new Dictionary<string, Func<string[], string>>();
            commands.Add("benchmark_nps", ExecuteBenchmarkNPSCommand);
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

        public override void Quit()
        {
            if (this.thoughtLog != null && !this.thoughtLog.IsDisposed)
                this.thoughtLog.Close();
            GC.SuppressFinalize(this);
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
                if (this.PONDERING_ENABLED && this.searchTask != null && !this.searchTask.IsCompleted)
                {
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
                if (this.PONDERING_ENABLED && this.searchTask != null && !this.searchTask.IsCompleted)
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
            if (this.PONDERING_ENABLED && this.searchTask != null && !this.searchTask.IsCompleted)
                StopPondering();

            var move = RegGenerateMove(color);
            this.board.Update(move);
            this.lastMove = move;
            if (this.REUSE_SUBTREE)
                this.tree.SetRoot(this.lastMove.Pos);
            else
                this.tree.Clear();

            if (this.PONDERING_ENABLED && this.board.GetGameResult(Color.Black) == GameResult.NotOver)
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

            var move = this.tree.Search(this.board, this.SIMULATION_COUNT);
            var rootEval = this.tree.GetRootNodeEvaluation();
            var moveEvals = this.tree.GetChildNodeEvaluations().ToArray();
            this.thoughtLog.WriteLine(SearchInfoToString(rootEval, moveEvals));
            this.thoughtLog.WriteLine($"kalmia selects {move}.");
            return move;
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

        string SearchInfoToString(PositionEval rootEval, PositionEval[] childrenEval)
        {
            var sb = new StringBuilder($"ellapsed={this.tree.SearchEllapsedTime}[ms] {rootEval.PlayoutCount}[playout] {this.tree.Pps}[pps] {this.tree.NodeCount}[nodes] {this.tree.Nps}[nps] winning_rate={rootEval.Value * 100.0f}%\n");
            sb.AppendLine("|move|playout_count|winnning_rate|probability|depth|pv");
            for(var i = 0; i < childrenEval.Length; i++)
            {
                var moveEval = childrenEval[i];
                var bestPath = this.tree.GetPV(i).ToArray();
                sb.Append($"| {moveEval.Position} |{moveEval.PlayoutCount,13}|{moveEval.Value * 100.0f,12:f2}%|{moveEval.MoveProbability * 100.0f,10:f2}%|{bestPath.Length - 1,5}|");
                foreach (var move in bestPath.Select(n => n.Position))
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
