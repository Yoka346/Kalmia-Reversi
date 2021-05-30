using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;
using Kalmia.MCTS;

namespace Kalmia.Engines
{
    public class MCTSEngine : GTPEngine
    {
        const string NAME = "MCTS Engine";
        const string VERSION = "0.0";

        readonly int PLAYOUT_NUM;
        UCT tree;
        Move lastMove;
        StreamWriter logger;

        public MCTSEngine(int playoutNum, int threadNum, string logPath):base(NAME, VERSION)
        {
            this.PLAYOUT_NUM = playoutNum;
            this.tree = new UCT(threadNum);
            this.logger = new StreamWriter(logPath);
        }

        string MoveEvalToString(MoveEval moveEval)
        {
            return $"Move: {moveEval.Move}\nRate: {moveEval.Rate * 100.0f}%\nWinRate: {moveEval.Value * 100.0f}%\nSimulationCount: {moveEval.SimulationCount}";
        }

        public override void Quit() { }

        public override bool Play(Move move)
        {
            if (base.Play(move))
            {
                this.lastMove = move;
                return true;
            }
            return false;
        }

        public override Move GenerateMove(Color color)
        {
            var move = RegGenerateMove(color);
            this.board.Update(move);
            this.lastMove = move;
            this.tree.SetRoot(this.lastMove);
            return move;
        }

        public override Move RegGenerateMove(Color color)
        {
            var sw = new Stopwatch();
            sw.Start();
            this.tree.SetRoot(this.lastMove);
            var move = this.tree.Search(this.board, this.PLAYOUT_NUM);
            sw.Stop();
            this.logger.WriteLine($"Ellapsed: {sw.ElapsedMilliseconds}ms");
            this.logger.WriteLine($"[ROOT_EVAL]\n{MoveEvalToString(this.tree.GetRootNodeEvaluation())}\n");
            this.logger.WriteLine($"[NEXT_MOVES_EVAL]\n");
            foreach (var moveEval in this.tree.GetChildNodeEvaluations())
                this.logger.WriteLine($"{MoveEvalToString(moveEval)}\n");
            this.logger.Flush();
            return move;
        }

        public override bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public override string LoadSGF(string path)
        {
            throw new NotImplementedException();
        }

        public override string LoadSGF(string path, int posX, int posY)
        {
            throw new NotImplementedException();
        }

        public override string LoadSGF(string path, int moveNum)
        {
            throw new NotImplementedException();
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {
            throw new NotImplementedException();
        }

        public override void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {
            throw new NotImplementedException();
        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
        }
    }
}
