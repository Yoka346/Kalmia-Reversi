using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

using Kalmia.GoTextProtocol;
using Kalmia.IO;
using Kalmia.MCTS;
using Kalmia.Reversi;

namespace Kalmia.Engines
{
    public class MCTSEngine : GTPEngine
    {
        new const string NAME = "MCTS Engine";
        new const string VERSION = "0.0";

        readonly int PLAYOUT_NUM;
        UCT tree;
        Move lastMove;

        public MCTSEngine(int playoutNum, int threadNum, string logPath):base(NAME, VERSION)
        {
            this.PLAYOUT_NUM = playoutNum;
            this.tree = new UCT(threadNum);
        }

        public MCTSEngine(int playoutNum, int threadNum, string logPath, Action<Board, Move[], float[], int, int> rolloutPolicy) : base(NAME, VERSION)
        {
            this.PLAYOUT_NUM = playoutNum;
            this.tree = new UCT(threadNum);
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
            this.tree.SetRoot(this.lastMove);
            var move = this.tree.Search(this.board, this.PLAYOUT_NUM);
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

        public override string LoadSGF(string path, int moveCount)
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
