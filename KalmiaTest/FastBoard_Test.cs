using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia;
using Kalmia.Reversi;
using static Kalmia.Reversi.Board;

namespace KalmiaTest
{
    [TestClass]
    public class FastBoard_Test
    {
        [TestMethod]
        public void CalculateMobilityAndFlipped_Test()
        {
            var board = new FastBoard();
            var boardTest = new SlowBoard(DiscColor.Black, InitialBoardState.Cross);
            var positons = new BoardPosition[SQUARE_NUM];
            var moves = new Move[SQUARE_NUM];
            var rand = new Random();

            while (!boardTest.IsGameover())
            {
                var moveCount = board.GetNextPositions(positons);
                var moveCountTest = boardTest.GetNextMoves(moves);
                Assert.AreEqual(moveCountTest, moveCount);
                AssertMovesAreEqual(boardTest, moves, positons.Select(p=>new Move(board.SideToMove, p)).ToArray(), moveCount);
                var nextMove = moves[rand.Next(moveCount)];
                board.Update(nextMove.Pos);
                boardTest.Update(nextMove);
                AssertDiscsAreEqual(boardTest, board);
            }
        }

        [TestMethod]
        public void GetHashCode_Test()
        {
            var rand = new Xorshift32();
            var p = ((ulong)rand.Next() << 32) | rand.Next();
            var o = ((ulong)rand.Next() << 32) | rand.Next();
            var pAndO = p & o;
            p ^= pAndO;
            var board = new FastBoard(DiscColor.Black, new Bitboard(p, o));
            Assert.AreEqual(board.GetHashCode_CPU(), board.GetHashCode());
        }

        void AssertMovesAreEqual(SlowBoard board, Move[] expected, Move[] actual, int moveCount)
        {
            for (var i = 0; i < moveCount; i++)
            {
                var idx = Array.IndexOf(actual, expected[i]);
                if (idx == -1 || idx >= moveCount)
                    Assert.Fail($"Expected to contain move {expected[i]}, but it was not found." +
                                $"\nexpected = {MoveArrayToString(expected, moveCount)}\nactual = {MoveArrayToString(actual, moveCount)}" +
                                $"\n{DiscsToString((from n in Enumerable.Range(0, SQUARE_NUM) select board.GetDiscColor(n % BOARD_SIZE, n / BOARD_SIZE)).ToArray())}");
            }
        }

        void AssertDiscsAreEqual(SlowBoard expected, FastBoard actual)
        {
            bool equal = true;
            for(var x = 0; x < BOARD_SIZE; x++)
                for(var y = 0; y < BOARD_SIZE; y++)
                    if(expected.GetDiscColor(x, y) != actual.GetDiscColor((BoardPosition)(x + y * BOARD_SIZE)))
                    {
                        equal = false;
                        break;
                    }
            if (!equal)
                Assert.Fail();
        }

        string MoveArrayToString(Move[] moves, int moveCount)
        {
            var sb = new StringBuilder("{ ");
            for (var i = 0; i < moveCount - 1; i++)
                sb.Append(moves[i].ToString() + ", ");
            sb.Append(moves[moveCount - 1] + " }");
            return sb.ToString();
        }

        string DiscsToString(DiscColor[] discs)
        {
            var sb = new StringBuilder();
            for (var y = 0; y < BOARD_SIZE; y++)
            {
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if (discs[x + y * BOARD_SIZE] == DiscColor.Black)
                        sb.Append(" X");
                    else if (discs[x + y * BOARD_SIZE] == DiscColor.White)
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
