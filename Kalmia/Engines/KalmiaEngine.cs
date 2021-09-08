using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        readonly BoardFeature[] BOARD_FEATURE;
        readonly int SIMULATION_COUNT;
        readonly bool PONDERING_ENABLED;
        readonly bool REUSE_SUBTREE;

        UCT tree;
        Move lastMove;
        Task searchTask;
        CancellationTokenSource cts;
        Logger thoughtLog;

        public ValueFunction ValueFunction { get; set; }

        public KalmiaEngine(KalmiaConfig config, string logFilePath) : base(NAME, VERSION)
        {
            this.BOARD_FEATURE = (from _ in Enumerable.Range(0, config.ThreadNum) select new BoardFeature()).ToArray();
            this.SIMULATION_COUNT = config.SimulationCount;
            this.ValueFunction = new ValueFunction(config.ValueFuncParamsFilePath);
            this.tree = new UCT(config.UCBFactor, config.ThreadNum);
            this.tree.ValueFunctionCallback = CalculateValue;
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
                this.tree.SetRoot(move);

                this.thoughtLog.WriteLine($"opponent's move is {move}\n\n");

                this.lastMove = move;
                return true;
            }

            if (board.GetNextMovesCount() == 0 && move.Pos == BoardPosition.Pass)
                return true;
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
                this.tree.SetRoot(this.lastMove);
            else
                this.tree.Clear();

            if (this.PONDERING_ENABLED && this.board.GetGameResult(Color.Black) == GameResult.NotOver)
                StartPondering();
            return move;
        }

        public override Move RegGenerateMove(Color color)
        {
            if (this.board.SideToMove != color)
                this.board.SwitchSideToMove();

            this.tree.SetRoot(this.lastMove);

            var moveCount = this.board.GetNextMovesCount();
            if (this.board.GetNextMovesCount() == 1)
                return this.board.GetNextMove(0);
            else if (moveCount == 0)
                return new Move(this.board.SideToMove, BoardPosition.Pass);

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
            return string.Empty;
        }

        float CalculateValue(Board board, int threadID)
        {
            var boardFeature = this.BOARD_FEATURE[threadID];
            boardFeature.SetBoard(board);
            return this.ValueFunction.F(boardFeature);
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

        string SearchInfoToString(MoveEval rootEval, MoveEval[] childrenEval)
        {
            var sb = new StringBuilder($"simulation_count={rootEval.SimulationCount}\tellapsed={this.tree.SearchEllapsedTime}[ms]");
            sb.AppendLine($"{this.tree.NodeCount}[nodes], {this.tree.Nps}[nps], value={rootEval.Value * 100.0f}, winning_rate={rootEval.ActionValue * 100.0f}%");
            sb.AppendLine("|move|simulation_count|value|winnning_rate|probability|depth|pv");
            for(var i = 0; i < childrenEval.Length; i++)
            {
                var moveEval = childrenEval[i];
                var bestPath = this.tree.GetBestPath(i).ToArray();
                sb.Append($"| {moveEval.Move} |{moveEval.SimulationCount,16}|{moveEval.Value * 100.0f,5:f2}|{moveEval.ActionValue * 100.0f,12:f2}%|{moveEval.MoveProbability * 100.0f,10:f2}%|{bestPath.Length - 1,5}|");
                foreach (var move in bestPath.Select(n => n.Move))
                    sb.Append($"{move} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
