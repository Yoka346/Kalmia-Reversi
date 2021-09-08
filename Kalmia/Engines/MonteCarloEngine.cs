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
    public class MonteCarloEngine : GTPEngine
    {
        new const string NAME = "MonteCarloEngine";
        new const string VERSION = "0.0";

        readonly int PLAYOUT_NUM;
        readonly int THREAD_NUM;
        readonly Random[] RAND;
        ParallelOptions parallelOptions;

        public MonteCarloEngine(int playoutNum) :this(playoutNum, Environment.ProcessorCount){ }

        public MonteCarloEngine(int playoutNum, int threadNum) : base(NAME, VERSION)
        {
            this.PLAYOUT_NUM = playoutNum;
            this.THREAD_NUM = threadNum;
            this.parallelOptions = new ParallelOptions();
            this.RAND = new Random[this.THREAD_NUM];
            this.parallelOptions.MaxDegreeOfParallelism = this.THREAD_NUM;

            var rand = new Random();
            for (var i = 0; i < this.RAND.Length; i++)
                this.RAND[i] = new Random(rand.Next());
        }

        public override Move GenerateMove(Color color)
        {
            var move = RegGenerateMove(color);
            this.board.Update(move);
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

        public override string LoadSGF(string path, int moveCount)
        {
            throw new NotImplementedException();
        }

        public override void Quit()
        {

        }

        public override Move RegGenerateMove(Color color)
        {
            var moves = this.board.GetNextMoves().ToArray();
            if (moves.Length == 1)
                return moves[0];

            var moveValues = new double[moves.Length];
            for (var i = 0; i < moves.Length; i++)
            {
                var board = new Board(this.board);
                board.Update(moves[i]);
                moveValues[i] = Playout(board, color);
            }

            var maxIdx = 0;
            for (var i = 0; i < moveValues.Length; i++)
                if (moveValues[i] > moveValues[maxIdx])
                    maxIdx = i;
            return moves[maxIdx];
        }

        public override void SendTimeLeft(int timeLeft, int byoYomiStonesLeft)
        {
            throw new NotImplementedException();
        }

        public override bool SetBoardSize(int size)
        {
            return Board.BOARD_SIZE == size;
        }

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones)
        {
            throw new NotImplementedException();
        }

        public override string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new NotImplementedException();
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
                for (var i = 0; i < this.PLAYOUT_NUM / this.THREAD_NUM; i++)
                {
                    board.CopyTo(b);
                    sum[threadID] += Simulate(b, color, threadID);
                }
            });

            var b = boards[0];
            for (var i = 0; i < this.PLAYOUT_NUM % this.THREAD_NUM; i++)
            {
                board.CopyTo(b);
                sum[0] += Simulate(b, color, 0);
            }
            return sum.Sum() / this.PLAYOUT_NUM;
        }

        double Simulate(Board board, Color color, int threadID)
        {
            var rand = this.RAND[threadID];
            int moveCount;
            while ((moveCount = board.GetNextMovesCount()) != 0)
                board.Update(board.GetNextMove(rand.Next(moveCount)));

            switch (board.GetGameResult(color))
            {
                case GameResult.Win:
                    return 1.0;

                case GameResult.Loss:
                    return 0.0;

                default:
                    return 0.5;
            }
        }
    }
}
