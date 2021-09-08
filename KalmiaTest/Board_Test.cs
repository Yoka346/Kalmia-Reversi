using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia.Reversi;
using static Kalmia.Reversi.Board;

namespace KalmiaTest
{
    [TestClass]
    public class Board_Test
    {
        [TestMethod]
        public void CalculateMobilityAndFlipped_Test()
        {
            var board = new Board(Color.Black, InitialBoardState.Cross);
            var boardTest = new SlowBoard(Color.Black, InitialBoardState.Cross);
            var moves = new Move[SQUARE_NUM];
            var movesTest = new Move[SQUARE_NUM];
            var rand = new Random();

            while (!boardTest.IsGameover())
            {
                var moveCount = board.GetNextMoves(moves);
                var moveCountTest = boardTest.GetNextMoves(movesTest);
                Assert.AreEqual(moveCountTest, moveCount);
                AssertMovesAreEqual(boardTest, movesTest, moves, moveCount);
                var nextMove = moves[rand.Next(moveCount)];
                board.Update(nextMove);
                boardTest.Update(nextMove);
                AssertDiscsAreEqual(boardTest.GetDiscsArray(), board.GetDiscsArray());
            }
        }

        void AssertMovesAreEqual(SlowBoard board, Move[] expected, Move[] actual, int moveCount)
        {
            for (var i = 0; i < moveCount; i++)
            {
                var idx = Array.IndexOf(actual, expected[i]);
                if (idx == -1 || idx >= moveCount)
                    Assert.Fail($"Expected to contain move {expected[i]}, but it was not found." +
                                $"\nexpected = {MoveArrayToString(expected, moveCount)}\nactual = {MoveArrayToString(actual, moveCount)}" +
                                $"\n{DiscsToString(board.GetDiscsArray())}");
            }
        }

        void AssertDiscsAreEqual(Color[,] expected, Color[,] actual)
        {
            bool equal = true;
            for(var x = 0; x < expected.GetLength(0); x++)
                for(var y = 0; y < expected.GetLength(1); y++)
                    if(expected[x, y] != actual[x, y])
                    {
                        equal = false;
                        break;
                    }
            if (!equal)
                Assert.Fail($"\nexpected = \n{DiscsToString(expected)}\nactual = \n{DiscsToString(actual)}");
        }

        string MoveArrayToString(Move[] moves, int moveCount)
        {
            var sb = new StringBuilder("{ ");
            for (var i = 0; i < moveCount - 1; i++)
                sb.Append(moves[i].ToString() + ", ");
            sb.Append(moves[moveCount - 1] + " }");
            return sb.ToString();
        }

        string DiscsToString(Color[,] discs)
        {
            var sb = new StringBuilder();
            for (var y = 0; y < discs.GetLength(1); y++)
            {
                for (var x = 0; x < discs.GetLength(0); x++)
                {
                    if (discs[x, y] == Color.Black)
                        sb.Append(" X");
                    else if (discs[x, y] == Color.White)
                        sb.Append(" O");
                    else
                        sb.Append(" .");
                }
                sb.Append("\n");
            }
            return sb.ToString();
        }
    }
}
