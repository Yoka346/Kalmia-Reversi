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
        Move[] moves = new Move[Board.MAX_MOVES_NUM];

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

        public Move GenerateMove(Color color)
        {
            if (this.board.Turn != color)
                this.board.SwitchTurn();
            var move = this.board.GetNextMove(this.RAND.Next(this.board.GetNextMovesNum()));
            this.board.Update(move);
            return move;
        }

        public Move RegGenerateMove(Color color)
        {
            if (this.board.Turn != color)
                this.board.SwitchTurn();
            var move = this.board.GetNextMove(this.RAND.Next(this.board.GetNextMovesNum()));
            return move;
        }

        public Color GetColor(int posX, int posY)
        {
            return this.board.GetColor(posX, posY);
        }

        public Move[] GetLegalMoves()
        {
            return this.board.GetNextMoves();
        }

        public Color GetSideToMove()
        {
            return this.board.Turn;
        }

        public string GetFinalResult()
        {
            var result = this.board.GetGameResult(Color.Black);
            if (result == GameResult.NotOver)
                return "Game is not over yet.";

            var resultMsg = new StringBuilder();
            var blackCount = this.board.GetDiscCount(Color.Black);
            var whiteCount = this.board.GetDiscCount(Color.White);
            if (result == GameResult.Draw)
                resultMsg.Append("Draw. ");
            else
            {
                var winner = (result == GameResult.Win) ? "Black" : "White";
                resultMsg.Append($"{winner} wins by {Math.Abs(blackCount - whiteCount)} points. ");
            }
            resultMsg.Append($"Final score is B {blackCount} and W {whiteCount}");
            return resultMsg.ToString();
        }

        public string GetFinalScore()
        {
            var result = this.board.GetGameResult(Color.Black);
            if (result == GameResult.NotOver)
                return string.Empty;

            var blackCount = this.board.GetDiscCount(Color.Black);
            var whiteCount = this.board.GetDiscCount(Color.White);
            if (result == GameResult.Draw)
                return "Draw";
            else
            {
                var winner = (result == GameResult.Win) ? "B" : "W";
                return $"{winner}+{Math.Abs(blackCount - whiteCount)}";
            }
        }

        public string GetName()
        {
            return NAME;
        }

        public string[] GetOriginalCommands()
        {
            return new string[0];
        }

        public string GetVersion()
        {
            return VERSION;
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

        public bool Play(Move move)
        {
            if (this.board.Turn != move.Color)
                this.board.SwitchTurn();
            this.board.GetNextMoves(this.moves);
            if (Array.IndexOf(this.moves, move) == -1)
                return false;
            this.board.Update(move);
            return true;
        }

        public void Quit()
        {

        }

        public void SendTimeLeft(int timeLeft, int countdownNumLeft)
        {

        }

        public int GetBoardSize()
        {
            return Board.BOARD_SIZE;
        }

        public bool SetBoardSize(int size)
        {
            return size == Board.BOARD_SIZE;
        }

        public void SetTime(int mainTime, int countdownTime, int countdownNum)
        {

        }

        public string ShowBoard()
        {
            return this.board.ToString();
        }

        public bool Undo()
        {
            return this.board.Undo();
        }
    }
}
