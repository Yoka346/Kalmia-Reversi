using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Kalmia.Reversi;
using Kalmia.GoTextProtocol;

namespace Kalmia.Engines
{
    public class MonteCarloEngine : IGTPEngine
    {
        public int PlayoutNum { get; }
        Board currrentBoard;
        readonly int THREAD_NUM;
        readonly Random[] RAND;
        ParallelOptions parallelOptions;
        Board[] boards;

        public MonteCarloEngine(int playoutNum) :this(playoutNum, Environment.ProcessorCount){ }

        public MonteCarloEngine(int playoutNum, int threadNum)
        {
            this.PlayoutNum = playoutNum;
            this.THREAD_NUM = threadNum;
            this.parallelOptions = new ParallelOptions();
            this.RAND = new Random[this.THREAD_NUM];
            this.parallelOptions.MaxDegreeOfParallelism = this.THREAD_NUM;
            this.boards = new Board[this.THREAD_NUM];

            var rand = new Random();
            for (var i = 0; i < this.RAND.Length; i++)
                this.RAND[i] = new Random(rand.Next());
        }

        public void ClearBoard()
        {
            this.currrentBoard = new Board(Color.Black, InitialBoardState.Cross);
            for (var i = 0; i < this.boards.Length; i++)
                this.boards[i] = new Board(this.currrentBoard);
        }

        public Move GenerateMove(Color color)
        {
            var moves = this.currrentBoard.GetNextMoves();
            Parallel.For(0, moves.Length, i =>
            {

            });
            throw new NotImplementedException();
        }

        public int GetBoardSize()
        {
            throw new NotImplementedException();
        }

        public Color GetColor(int posX, int posY)
        {
            throw new NotImplementedException();
        }

        public string GetFinalResult()
        {
            throw new NotImplementedException();
        }

        public Move[] GetLegalMoves()
        {
            throw new NotImplementedException();
        }

        public string GetName()
        {
            throw new NotImplementedException();
        }

        public Color GetSideToMove()
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

        public bool Play(Move move)
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            throw new NotImplementedException();
        }

        public Move RegGenerateMove(Color color)
        {
            throw new NotImplementedException();
        }

        public void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {
            throw new NotImplementedException();
        }

        public bool SetBoardSize(int size)
        {
            throw new NotImplementedException();
        }

        public void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
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

        double Playout(Board board)
        {
            int moveNum;
            while((moveNum = board.GetNextMovesNum()) != 0)
            {

            }
            throw new NotImplementedException();
        }
    }
}
