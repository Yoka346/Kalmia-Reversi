using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia;
using static Kalmia.Board;

namespace KalmiaTest
{
    [TestClass]
    public class Board_Test
    {
        [TestMethod]
        public void CalculateMobilityAndFlipped_Test()
        {
            var board = new Board(Color.Black, InitialBoardState.Cross);
            var boardTest = new BoardForTest(Color.Black, InitialBoardState.Cross);
            var moves = new Move[GRID_NUM];
            var movesTest = new Move[GRID_NUM];
            bool passed = false;
            bool gameover = false;
            var rand = new Random();

            while (!gameover)
            {
                var moveNum = board.GetNextMoves(moves);
                var moveNumTest = boardTest.GetNextMoves(movesTest);
                Assert.AreEqual(moveNumTest, moveNum);
                AssertMovesAreEqual(boardTest, movesTest, moves, moveNum);
                var nextMove = moves[rand.Next(moveNum)];
                board.Update(nextMove);
                boardTest.Update(nextMove);
                AssertDiscsAreEqual(boardTest.GetDiscsArray(), board.GetDiscsArray());
                if (nextMove.Pos == Move.PASS)
                    if (!passed)
                        passed = true;
                    else
                        gameover = true;
                else if (passed)
                    passed = false;
            }
        }

        void AssertMovesAreEqual(BoardForTest board, Move[] expected, Move[] actual, int moveNum)
        {
            for (var i = 0; i < moveNum; i++)
            {
                var idx = Array.IndexOf(actual, expected[i]);
                if (idx == -1 || idx >= moveNum)
                    Assert.Fail($"Expected to contain move {expected[i]}, but it was not found." +
                                $"\nexpected = {MoveArrayToString(expected, moveNum)}\nactual = {MoveArrayToString(actual, moveNum)}" +
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

        string MoveArrayToString(Move[] moves, int moveNum)
        {
            var sb = new StringBuilder("{ ");
            for (var i = 0; i < moveNum - 1; i++)
                sb.Append(moves[i].ToString() + ", ");
            sb.Append(moves[moveNum - 1] + " }");
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
                        sb.Append(" O");
                    else if (discs[x, y] == Color.White)
                        sb.Append(" X");
                    else
                        sb.Append(" *");
                }
                sb.Append("\n");
            }
            return sb.ToString();
        }
    }
}
