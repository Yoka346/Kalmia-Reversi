using System;
using System.Text;
using System.Linq;

using Kalmia.Reversi;

namespace Kalmia.GoTextProtocol
{
    public abstract class GTPEngine
    {
        protected readonly string NAME;
        protected readonly string VERSION;
        protected Board board;

        public GTPEngine(string name, string version)
        {
            this.NAME = name;
            this.VERSION = version;
            this.board = new Board(StoneColor.Black, InitialBoardState.Cross);
        }

        public abstract void Quit();

        public string GetName() 
        {
            return this.NAME;
        }

        public string GetVersion() 
        {
            return this.VERSION;
        }

        public int GetBoardSize() 
        {
            return Board.BOARD_SIZE;
        }

        public virtual void ClearBoard()
        {
            this.board = new Board(StoneColor.Black, InitialBoardState.Cross);
        }

        public StoneColor GetColor(int posX, int posY)
        {
            return this.board.GetColor(posX, posY);
        }

        public virtual bool Play(Move move)
        {
            return this.board.Update(move);
        }

        public virtual string ShowBoard()
        {
            return $"\n{this.board}";
        }

        public virtual bool Undo()
        {
            return this.board.Undo();
        }

        public string GetFinalScore()
        {
            var result = this.board.GetGameResult(StoneColor.Black);
            if (result == GameResult.NotOver)
                return string.Empty;

            var blackCount = this.board.GetDiscCount(StoneColor.Black);
            var whiteCount = this.board.GetDiscCount(StoneColor.White);
            if (result == GameResult.Draw)
                return "Draw";
            else
            {
                var winner = (result == GameResult.Win) ? "B" : "W";
                return $"{winner}+{Math.Abs(blackCount - whiteCount)}";
            }
        }

        public virtual string GetFinalResult()
        {
            var result = this.board.GetGameResult(StoneColor.Black);
            if (result == GameResult.NotOver)
                return "Game is not over yet.";

            var resultMsg = new StringBuilder();
            var blackCount = this.board.GetDiscCount(StoneColor.Black);
            var whiteCount = this.board.GetDiscCount(StoneColor.White);
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

        public Move[] GetLegalMoves()
        {
            return this.board.GetNextMoves().ToArray();
        }

        public StoneColor GetSideToMove()
        {
            return this.board.SideToMove;
        }

        public virtual string[] GetOriginalCommands()
        {
            return new string[0];
        }

        public abstract bool SetBoardSize(int size);
        public abstract string LoadSGF(string path);
        public abstract string LoadSGF(string path, int posX, int posY);
        public abstract string LoadSGF(string path, int moveCount);
        public abstract Move GenerateMove(StoneColor color);
        public abstract Move RegGenerateMove(StoneColor color);
        public abstract void SetTime(int mainTime, int byoYomiTime, int byoYomiStones);
        public abstract void SendTimeLeft(int timeLeft, int byoYomiStonesLeft);
        public abstract string ExecuteOriginalCommand(string command, string[] args);
    }
}
