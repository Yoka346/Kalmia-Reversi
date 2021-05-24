using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

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

        public MonteCarloEngine(int playoutNum) :this(playoutNum, Environment.ProcessorCount){ }

        public MonteCarloEngine(int playoutNum, int threadNum)
        {
            this.currrentBoard = new Board(Color.Black, InitialBoardState.Cross);
            this.PlayoutNum = playoutNum;
            this.THREAD_NUM = threadNum;
            this.parallelOptions = new ParallelOptions();
            this.RAND = new Random[this.THREAD_NUM];
            this.parallelOptions.MaxDegreeOfParallelism = this.THREAD_NUM;

            var rand = new Random();
            for (var i = 0; i < this.RAND.Length; i++)
                this.RAND[i] = new Random(rand.Next());
        }

        public void ClearBoard()
        {
            this.currrentBoard = new Board(Color.Black, InitialBoardState.Cross);
        }

        public Move GenerateMove(Color color)
        {
            var move = RegGenerateMove(color);
            this.currrentBoard.Update(move);
            return move;
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
            if (!this.currrentBoard.IsLegalMove(move))
                return false;
            this.currrentBoard.Update(move);
            return true;
        }

        public void Quit()
        {
            throw new NotImplementedException();
        }

        public Move RegGenerateMove(Color color)
        {
            var moves = this.currrentBoard.GetNextMoves();
            if (moves.Length == 1)
                return moves[0];

            var moveValues = new double[moves.Length];
            for (var i = 0; i < moves.Length; i++)
            {
                var board = new Board(this.currrentBoard);
                board.Update(moves[i]);
                moveValues[i] = Playout(board, color);
            }

            var maxIdx = 0;
            for (var i = 0; i < moveValues.Length; i++)
                if (moveValues[i] > moveValues[maxIdx])
                    maxIdx = i;
            return moves[maxIdx];
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
            return this.currrentBoard.ToString();
        }

        public bool Undo()
        {
            return this.currrentBoard.Undo();
        }

        double Playout(Board board, Color color)
        {
            var boards = new Board[this.THREAD_NUM];
            for (var i = 0; i < this.THREAD_NUM; i++)
                boards[i] = new Board(Color.Black, InitialBoardState.Cross);

            var sum = new double[this.THREAD_NUM];
            Parallel.For(0, this.THREAD_NUM, (threadID) =>
            {
                var b = boards[threadID];
                for (var i = 0; i < this.PlayoutNum / this.THREAD_NUM; i++)
                {
                    board.CopyTo(b);
                    sum[threadID] += Simulate(b, color, threadID);
                }
            });

            var b = boards[0];
            for (var i = 0; i < this.PlayoutNum % this.THREAD_NUM; i++)
            {
                board.CopyTo(b);
                sum[0] += Simulate(b, color, 0);
            }
            return sum.Sum() / this.PlayoutNum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double Simulate(Board board, Color color, int threadID)
        {
            var rand = this.RAND[threadID];
            int moveNum;
            while ((moveNum = board.GetNextMovesNum()) != 0)
                board.Update(board.GetNextMove(rand.Next(moveNum)));

            switch (board.GetGameResult(color))
            {
                case GameResult.Win:
                    return 1.0;

                case GameResult.Lose:
                    return 0.0;

                default:
                    return 0.5;
            }
        }
    }
}
