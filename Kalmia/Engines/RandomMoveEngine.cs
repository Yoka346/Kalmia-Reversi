using System;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;
using Kalmia.GoTextProtocol;

namespace Kalmia.Engines
{
    public class RandomMoveEngine : GTPEngine
    {
        const string NAME = "Random Move Engine";
        const string VERSION = "0.0";

        readonly Random RAND;

        public RandomMoveEngine():base(NAME, VERSION)
        {
            this.RAND = new Random();
        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
        }

        public override Move GenerateMove(Color color)
        {
            if (this.board.Turn != color)
                this.board.SwitchTurn();
            var move = this.board.GetNextMove(this.RAND.Next(this.board.GetNextMovesNum()));
            this.board.Update(move);
            return move;
        }

        public override Move RegGenerateMove(Color color)
        {
            if (this.board.Turn != color)
                this.board.SwitchTurn();
            var move = this.board.GetNextMove(this.RAND.Next(this.board.GetNextMovesNum()));
            return move;
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

        public override void Quit()
        {

        }

        public override void SendTimeLeft(int timeLeft, int countdownNumLeft)
        {

        }

        public override bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public override void SetTime(int mainTime, int countdownTime, int countdownNum)
        {

        }

        public override string[] GetOriginalCommands()
        {
            return new string[0];
        }
    }
}
