using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace Kalmia.Engines
{
    public class ValueFuncEngine : GTPEngine
    {
        new const string NAME = "ValueFunc Engine";
        new const string VERSION = "0.0";
        readonly ValueFunction VALUE_FUNC;

        public ValueFuncEngine(ValueFunction valueFunc) : base(NAME, VERSION)
        {
            this.VALUE_FUNC = valueFunc;
        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
        }

        public override Move GenerateMove(Color color)  
        {
            if (this.board.SideToMove != color)
                this.board.SwitchSideToMove();

            var board = new Board(this.board);
            var moves = board.GetNextMoves().ToArray();
            Move bestMove = moves[0];
            board.Update(bestMove);
            var maxValue = 1 - this.VALUE_FUNC.F(new BoardFeature(board));
            for(var i = 1; i < moves.Length; i++)
            {
                var move = moves[i];
                board.Undo();
                board.Update(move);
                var value = 1 - this.VALUE_FUNC.F(new BoardFeature(board));
                if(value >= maxValue)
                {
                    bestMove = move;
                    maxValue = value;
                }
            }
            this.board.Update(bestMove);
            return bestMove;
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
