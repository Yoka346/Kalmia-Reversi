using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace KalmiaTest
{
    internal static class Utils
    {
        public static FastBoard CreateRandomBoard(Random rand, int emptyCount)
        {
            var board = new FastBoard();
            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            while (board.GetEmptyCount() != emptyCount && board.GetGameResult() == GameResult.NotOver)
            {
                var num = board.GetNextPositionCandidates(positions);
                board.Update(positions[rand.Next(num)]);
            }
            return board;
        }
    }
}
