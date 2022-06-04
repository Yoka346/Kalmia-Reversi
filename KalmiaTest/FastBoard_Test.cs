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
        public void CalculateMobilityAndFlip_Test()
        {
            const int SAMPLE_NUM = 1000;
            for (var i = 0; i < SAMPLE_NUM; i++)
            {
                var board = new FastBoard();
                var boardTest = new SlowBoard(DiscColor.Black, InitialBoardState.Cross);
                var positons = new BoardCoordinate[SQUARE_NUM];
                var rand = new Random();

                while (!boardTest.IsGameover())
                {
                    var moveCount = board.GetNextPositionCandidates(positons);
                    var moves = boardTest.GetNextMoves();
                    Assert.AreEqual(moves.Length, moveCount);
                    AssertMovesAreEqual(boardTest, moves, positons.Select(p => new Move(board.SideToMove, p)).ToArray(), moveCount);
                    var nextMove = moves[rand.Next(moveCount)];
                    board.Update(nextMove.Coord);
                    boardTest.Update(nextMove);
                    AssertDiscsAreEqual(boardTest, board);
                }
            }
        }

        [TestMethod]
        public void CountLastFlip_Test()
        {
            const int SAMPLE_NUM = 1000;
            var rand = new Random();
            var pos = new BoardCoordinate[1];
            for (var i = 0; i < SAMPLE_NUM; i++)
            {
                var board = CreateRandomBoard(rand, 1);
                var num = board.GetNextPositionCandidates((Span<BoardCoordinate>)pos);
                if (num == 0 || pos[0] == BoardCoordinate.Pass)
                    continue;
                var actual = board.GetLastFlippedDiscDoubleCount(pos[0]) / 2;
                var discCount = board.GetOpponentPlayerDiscCount();
                board.Update(pos[0]);
                var expected = Math.Abs(board.GetCurrentPlayerDiscCount() - discCount);
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void GetHashCode_Test()
        {
            var rand = new Random();
            var p = (ulong)rand.NextInt64();
            var o = (ulong)rand.NextInt64();
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
                    if(expected.GetDiscColor(x, y) != actual.GetDiscColor((BoardCoordinate)(x + y * BOARD_SIZE)))
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

        FastBoard CreateRandomBoard(Random rand, int emptyCount)
        {
            var board = new FastBoard();
            Span<BoardCoordinate> positions = stackalloc BoardCoordinate[MAX_MOVE_CANDIDATE_COUNT];
            while(board.GetEmptyCount() != emptyCount && board.GetGameResult() == GameResult.NotOver)
            {
                var num = board.GetNextPositionCandidates(positions);
                board.Update(positions[rand.Next(num)]);
            }
            return board;
        }
    }
}
