using System.Runtime.CompilerServices;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class GameInfo
    {
        public FastBoard Board;
        public BoardFeature Feature;

        public GameInfo(FastBoard board, BoardFeature feature)
        {
            this.Board = new FastBoard(board);
            this.Feature = new BoardFeature(feature);
        }

        public GameInfo(GameInfo gameInfo) : this(gameInfo.Board, gameInfo.Feature) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(GameInfo dest)
        {
            this.Board.CopyTo(dest.Board);
            this.Feature.CopyTo(dest.Feature);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(BoardCoordinate pos)
        {
            this.Feature.Update(pos, this.Board.Update(pos));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextPositionCandidates(BoardCoordinate[] positions)
        {
            return this.Board.GetNextPositionCandidates(positions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameResult GetGameResult()
        {
            return this.Board.GetGameResult();
        }
    }
}
