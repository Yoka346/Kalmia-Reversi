using System;
using System.Text;
using System.Linq;

using Kalmia.Reversi;
using Kalmia.IO;

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
            this.board = new Board(DiscColor.Black, InitialBoardState.Cross);
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
            this.board = new Board(DiscColor.Black, InitialBoardState.Cross);
        }

        public DiscColor GetColor(int posX, int posY)
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
            var result = this.board.GetGameResult(DiscColor.Black);
            if (result == GameResult.NotOver)
                return string.Empty;

            var blackCount = this.board.GetDiscCount(DiscColor.Black);
            var whiteCount = this.board.GetDiscCount(DiscColor.White);
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
            var result = this.board.GetGameResult(DiscColor.Black);
            if (result == GameResult.NotOver)
                return "Game is not over yet.";

            var resultMsg = new StringBuilder();
            var blackCount = this.board.GetDiscCount(DiscColor.Black);
            var whiteCount = this.board.GetDiscCount(DiscColor.White);
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

        public DiscColor GetSideToMove()
        {
            return this.board.SideToMove;
        }

        public virtual string[] GetOriginalCommands()
        {
            return new string[0];
        }

        public virtual string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new GTPException("not supported.");
        }

        public virtual string LoadSGF(string path)
        {
            return LoadSGF(path, int.MaxValue, BoardPosition.Null);
        }

        public virtual string LoadSGF(string path, int posX, int posY)
        {
            return LoadSGF(path, int.MaxValue, (BoardPosition)(posX + posY * Board.BOARD_SIZE));
        }

        public virtual string LoadSGF(string path, int moveCount) 
        {
            return LoadSGF(path, moveCount, BoardPosition.Null);
        }

        string LoadSGF(string path, int moveCount, BoardPosition pos)
        {
            this.board = new Board(DiscColor.Black, InitialBoardState.Cross);
            var node = SGFFile.LoadSGFFile(path);
            var currentMoveCount = 0;
            while (true)
            {
                var hasMove = node.HasMove(this.board.SideToMove);
                if (!hasMove && node.HasMove(this.board.Opponent))
                {
                    this.board.SwitchSideToMove();
                    hasMove = true;
                }

                if (hasMove)
                {
                    if (++currentMoveCount == moveCount)
                        break;

                    var sgfCoord = node.GetMove(this.board.SideToMove);
                    var move = new Move(this.board.SideToMove, SGFFile.SGFCoordinateToBoardPos(sgfCoord));
                    Console.Write(move);
                    if (move.Pos == pos)
                        break;
                    if (!this.board.Update(move))
                        throw new GTPException("specified SGF file contains invalid move.");
                }

                if (node.ChildNodes.Count == 0)
                    break;
                node = node.ChildNodes[0];
            }
            return this.board.SideToMove.ToString();
        }

        public abstract bool SetBoardSize(int size);
        public abstract Move GenerateMove(DiscColor color);
        public abstract Move RegGenerateMove(DiscColor color);
        public abstract void SetTime(int mainTime, int byoYomiTime, int byoYomiStones);
        public abstract void SendTimeLeft(DiscColor color, int timeLeft, int byoYomiStonesLeft);
    }
}
