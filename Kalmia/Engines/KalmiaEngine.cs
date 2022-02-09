using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Kalmia.MCTS;
using Kalmia.Evaluation;
using Kalmia.GoTextProtocol;
using Kalmia.IO;
using Kalmia.Reversi;


// ToDo: 線形評価関数の最適化プログラムの実装
//       確率的な着手の実装
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
        /// The time limit for one move. 
        /// </summary>
        public int MilliSecPerMove { get; set; } = int.MaxValue;

        /// <summary>
        /// Time limit will be decreased by this value.
        /// </summary>
        public int TimeLimitDelayMilliSec { get; set; } = 100;

        /// <summary>
        /// Whether selects next move stochastically or not.
        /// </summary>
        public bool SelectMoveStochastically { get; set; }

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
    }

    /// <summary>
    /// Provides reversi engine.
    /// </summary>
    public class KalmiaEngine : GTPEngine
    {
        const string _NAME = "Kalmia_LTF";
        const string _VERSION = "1.0";

        readonly Random RAND = new(Random.Shared.Next());

        UCT tree;
        Task searchTask;
        Logger thoughtLog;
        SearchInfo lastGenMoveSearchInfo;
        bool timeControlEnabled;
        int remainingTime;
        int byoYomiTime;
        bool quitFlag;

        public KalmiaConfig Config { get; }
        public int SearchEllapsedMilliSec { get { return this.tree.SearchEllapsedMilliSec; } }
        public SearchInfo SearchInfo { get { return this.tree.IsSearching ? this.tree.SearchInfo : this.lastGenMoveSearchInfo; } }

        public KalmiaEngine(KalmiaConfig config):this(config, string.Empty) { }

        public KalmiaEngine(KalmiaConfig config, string logFilePath):base(_NAME, _VERSION)
        {
            this.Config = config;
            this.tree = new UCT(config.TreeOptions, new LatentFactorValueFunction(config.ValueFuncParamFile));
            //this.tree = new UCT(config.TreeOptions, new ValueFunction(config.ValueFuncParamFile));
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

            var timeLimit = timeControlEnabled ? this.remainingTime / this.board.GetEmptyCount() + this.byoYomiTime : this.Config.MilliSecPerMove;
            if(timeLimit - this.Config.TimeLimitDelayMilliSec > 0)
                timeLimit -= this.Config.TimeLimitDelayMilliSec;
            this.tree.Search(this.Config.SearchCount, timeLimit);
            var searchInfo = this.tree.SearchInfo;
            this.lastGenMoveSearchInfo = searchInfo;

            bool additionalSearchRequired;
            var move = this.Config.SelectMoveStochastically ? SelectMoveStochastically(searchInfo, out additionalSearchRequired)
                                                            : SelectBestMove(searchInfo, out additionalSearchRequired);

            this.remainingTime -= this.tree.SearchEllapsedMilliSec;
            var byoYomiRest = this.byoYomiTime;
            if (this.remainingTime < 0)
            {
                byoYomiRest += this.remainingTime;
                if (byoYomiRest < 0)
                    byoYomiRest = 0;
                this.remainingTime = 0;
            }

            if(additionalSearchRequired && timeControlEnabled && timeLimit * 1.5 < this.remainingTime + byoYomiRest)
            {
                int additionalTimeLimit;
                if (timeLimit * 1.5 < this.remainingTime)
                    additionalTimeLimit = timeLimit;
                else if (byoYomiRest > 0)
                {
                    if (timeLimit <= this.remainingTime)
                        additionalTimeLimit = timeLimit;
                    else
                        additionalTimeLimit = this.remainingTime + byoYomiRest;
                }
                else
                    additionalTimeLimit = 0;

                additionalTimeLimit -= this.Config.TimeLimitDelayMilliSec;
                if (additionalTimeLimit > 0)
                {
                    this.thoughtLog.WriteLine("Additional thought is required.\n");
                    this.tree.Search(this.Config.SearchCount, timeLimit);
                    searchInfo = this.SearchInfo;
                    this.lastGenMoveSearchInfo = searchInfo;
                    this.remainingTime -= additionalTimeLimit;
                    if (this.remainingTime < 0)
                        this.remainingTime = 0;
                }
            }

            this.thoughtLog.WriteLine(GetSearchInfoString());
            this.thoughtLog.Flush();
            return move;
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {
            if(mainTime == 0 && byoYomiTime == 0 && byoYomiStones == 0)
            {
                this.timeControlEnabled = false;
                return;
            }

            this.remainingTime = mainTime * 1000;
            this.byoYomiTime = byoYomiTime * 1000;
            this.timeControlEnabled = true;
        }

        public override void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {
            this.remainingTime = timeLeft * 1000;
            this.timeControlEnabled = true;
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

        public Move SelectBestMove(SearchInfo searchInfo, out bool additionalSearchIsRequired)
        {
            if (searchInfo.ChildEvaluations.Count == 1)
            {
                additionalSearchIsRequired = false;
                return searchInfo.ChildEvaluations[0].Move;
            }
            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(key => key.RolloutCount).ToArray();
            (var first, var second) = (childEvals[0], childEvals[1]);
            additionalSearchIsRequired = second.Value > first.Value || second.RolloutCount < first.RolloutCount * 1.5;
            return first.Move;
        }

        public Move SelectMoveStochastically(SearchInfo searchInfo, out bool additionalSearchIsRequired)
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

            var childEvals = searchInfo.ChildEvaluations.OrderByDescending(key => key.RolloutCount).ToArray();
            (var first, var second) = (childEvals[0], childEvals[1]);
            additionalSearchIsRequired = second.Value > first.Value || second.RolloutCount < first.RolloutCount * 1.5;

            return searchInfo.ChildEvaluations[i].Move;
        }

        string GetSearchInfoString()
        {
            var searchInfo = this.SearchInfo;
            var sb = new StringBuilder($"ellapsed={this.SearchEllapsedMilliSec}[ms] {searchInfo.RootEvaluation.RolloutCount}[playouts] {this.tree.Pps}[pps] winning_rate={searchInfo.RootEvaluation.Value * 100.0f:f2}%\n");
            sb.AppendLine("|move|search_count|winnning_rate|probability|depth|pv");
            foreach(var childEval in searchInfo.ChildEvaluations.OrderByDescending(n => n.RolloutCount))
            {
                var m = (GTP.CoordinateRule != GTPCoordinateRule.Othello) ? GTP.ConvertCoordinateRule(childEval.Move) : childEval.Move;
                sb.Append($"| {m} |{childEval.RolloutCount,12}|{childEval.Value * 100.0f,12:f2}%|{childEval.MoveProbability * 100.0f,10:f2}%|{childEval.PrincipalVariation.Count,5}|");
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
