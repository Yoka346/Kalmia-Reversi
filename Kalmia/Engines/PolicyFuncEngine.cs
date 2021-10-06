using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace Kalmia.Engines
{
    public class PolicyFuncEngine : GTPEngine
    {
        new const string NAME = "PolicyFunc Engine";
        new const string VERSION = "0.0";
        readonly PolicyFunction POLICY_FUNC;
        readonly Xorshift RAND = new Xorshift();

        public PolicyFuncEngine(PolicyFunction policyFunc) : base(NAME, VERSION)
        {
            this.POLICY_FUNC = policyFunc;
        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
        }

        public override Move GenerateMove(Color color)
        {
            if (this.board.SideToMove != color)
                this.board.SwitchSideToMove();

            var moves = this.board.GetNextMoves().ToArray();
            if (moves.Length == 1)
            {
                this.board.Update(moves[0]);
                return moves[0];
            }

            Span<float> y = stackalloc float[moves.Length];
            this.POLICY_FUNC.F(new BoardFeature(new FastBoard(board)), moves, y, moves.Length);
            //var arrorw = this.RAND.NextFloat();
            //var sum = 0.0f;
            //var i = 0;
            //while ((sum += y[i]) < arrorw)
            //    i++;
            //this.board.Update(moves[i]);
            //return moves[i];
            var maxIdx = 0;
            for (var i = 1; i < y.Length; i++)
                if (y[i] > y[maxIdx])
                    maxIdx = i;
            this.board.Update(moves[maxIdx]);
            return moves[maxIdx];
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

        public override void Quit()
        {

        }

        public override Move RegGenerateMove(Color color)
        {
            throw new NotImplementedException();
        }

        public override void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {
            throw new NotImplementedException();
        }

        public override bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {
            throw new NotImplementedException();
        }
    }
}

