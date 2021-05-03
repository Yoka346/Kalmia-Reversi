using System;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;
using Kalmia.GoTextProtocol;

namespace Kalmia.Engines
{
    public class RandomMoveEngine : IGTPEngine
    {
        const string NAME = "Random Move Engine";
        const string VERSION = "0.0";

        readonly Random RAND;
        Board board;

        public RandomMoveEngine()
        {
            this.RAND = new Random();
            this.board = new Board(Color.Black, InitialBoardState.Cross);
        }

        public void ClearBoard()
        {
            this.board = new Board(Color.Black, InitialBoardState.Cross);
        }

        public string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
        }

        public string GenerateMove(Color color)
        {
            if (this.board.Turn != color)
                this.board.SwitchTurn();
        }

        public Color GetColor(int posX, int posY)
        {
            throw new NotImplementedException();
        }

        public string GetFinalScore()
        {
            throw new NotImplementedException();
        }

        public string GetName()
        {
            throw new NotImplementedException();
        }

        public string[] GetOriginalCommands()
        {
            throw new NotImplementedException();
        }

        public string GetVersion()
        {
            throw new NotImplementedException();
        }

        public string LoadSGF(string path)
        {
            throw new NotImplementedException();
        }

        public string LoadSGF(string path, int posX, int posY)
        {
            throw new NotImplementedException();
        }

        public string LoadSGF(string path, int moveNum)
        {
            throw new NotImplementedException();
        }

        public bool Play(Color color, Move move)
        {
            throw new NotImplementedException();
        }

        public bool Put(Color color, (int posX, int posY) coord)
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            throw new NotImplementedException();
        }

        public string RegGenerateMove(Color color)
        {
            throw new NotImplementedException();
        }

        public void SendTimeLeft(int timeLeft, int countdownNumLeft)
        {
            throw new NotImplementedException();
        }

        public bool SetBoardSize(int size)
        {
            throw new NotImplementedException();
        }

        public List<(int x, int y)> SetHandicap(int num)
        {
            throw new NotImplementedException();
        }

        public void SetKomi(double komi)
        {
            throw new NotImplementedException();
        }

        public void SetTime(int mainTime, int countdownTime, int countdownNum)
        {
            throw new NotImplementedException();
        }

        public string ShowBoard()
        {
            throw new NotImplementedException();
        }

        public bool Undo()
        {
            throw new NotImplementedException();
        }
    }
}
