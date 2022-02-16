using Kalmia.Reversi;
using System;
using System.Runtime.CompilerServices;

namespace Kalmia.EndGameSolver
{
    /// <summary>
    /// Provides reversi mate solver(Solves winner not max discs difference.)
    /// </summary>
    public class MateSolver
    {
        const int FAST_SEARCH_THRESHOLD = 6;

        TranspositionTable<GameResult> transpositionTable;
        int searchStartTime;
        int searchEndTime;

        public ulong InternalNodeCount { get; private set; }
        public ulong LeafNodeCount { get; private set; }
        public bool IsSearching { get; private set; }
        public int SearchEllapsedMilliSec { get { return this.IsSearching ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }
        public float Nps { get { return (this.InternalNodeCount + this.LeafNodeCount) / (this.SearchEllapsedMilliSec * 1.0e-3f); } }

        public MateSolver(ulong maxMemorySize)
        {
            this.transpositionTable = new TranspositionTable<GameResult>(maxMemorySize);
        }

        public void ClearSearchResults()
        {
            this.transpositionTable.Clear();
        }

        public BoardPosition SolveBestMove(FastBoard rootState, int timeLimit, out GameResult result, out bool timeout)
        {
            var board = new FastBoard(rootState);
            var bitboard = board.GetBitboard();
            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            SortPositions(board, positions, posNum);

            if(posNum == 0)
            {
                result = board.GetGameResult();
                timeout = false;
                return BoardPosition.Null;
            }

            var bestPos = positions[0];
            var bestScore = GameResult.Loss;
            this.InternalNodeCount = 0UL;
            this.LeafNodeCount = 0UL;
            this.searchStartTime = Environment.TickCount;
            timeout = false;
            this.IsSearching = true;
            for(var i = 0; i < posNum; i++)
            {
                board.Update(positions[i]);
                var score = (GameResult)(-(int)SearchWithTranspositionTable(board, GameResult.Loss, (GameResult)(-(int)bestScore), timeLimit, out timeout));
                if (timeout)
                {
                    this.IsSearching = false;
                    this.searchEndTime = Environment.TickCount;
                    result = bestScore;
                    return bestPos;
                }
                board.SetBitboard(bitboard);

                if(score == GameResult.Win)
                {
                    result = GameResult.Win;
                    return positions[i];
                }

                if (score > bestScore)
                {
                    bestPos = positions[i];
                    bestScore = score;
                }
            }

            this.IsSearching = false;
            this.searchEndTime = Environment.TickCount;
            result = bestScore;
            return bestPos;
        }

        [SkipLocalsInit]
        unsafe GameResult SearchWithTranspositionTable(FastBoard board, GameResult alpha, GameResult beta, int timeLimit, out bool timeout)
        {
            if (this.SearchEllapsedMilliSec >= timeLimit)
            {
                timeout = true;
                return 0.0f;
            }
            timeout = false;

            TTEntry<GameResult>? ttEntry = null;
            var hashCode = board.GetHashCode();
            ttEntry = this.transpositionTable.GetEntry(hashCode);

            if (ttEntry.HasValue)   // hash hit
            {
                if (beta <= ttEntry.Value.LowerBound)
                    return ttEntry.Value.LowerBound;

                if (alpha >= ttEntry.Value.UpperBound)
                    return ttEntry.Value.UpperBound;

                if (ttEntry.Value.UpperBound == ttEntry.Value.LowerBound)
                    return ttEntry.Value.LowerBound;

                if (ttEntry.Value.LowerBound > alpha)
                    alpha = ttEntry.Value.LowerBound;

                if (ttEntry.Value.UpperBound < beta)
                    beta = ttEntry.Value.UpperBound;
            }

            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            if (posNum == 1 && positions[0] == BoardPosition.Pass && board.GetNextPositionsCandidatesNumAfter(BoardPosition.Pass) == 0)  // gameover
            {
                this.LeafNodeCount++;
                var playerCount = board.GetCurrentPlayerDiscCount();
                var opponentCount = board.GetOpponentPlayerDiscCount();
                GameResult result;
                if (playerCount == opponentCount)
                    result = GameResult.Draw;
                else
                    result = (playerCount > opponentCount) ? GameResult.Win : GameResult.Loss;
                this.transpositionTable.SetEntry(hashCode, result, result);
                return result;
            }

            this.InternalNodeCount++;
            var bitboard = board.GetBitboard();
            var emptyCount = board.GetEmptyCount();
            var enableNextTransposition = (emptyCount - 1) > FAST_SEARCH_THRESHOLD;
            SortPositions(board, positions, posNum);
            var newAlpha = alpha;
            var bestScore = GameResult.Loss;
            for (var i = 0; i < posNum; i++)
            {
                var pos = positions[i];
                board.Update(pos);
                GameResult score;
                if (enableNextTransposition)
                    score = (GameResult)(-(int)SearchWithTranspositionTable(board, (GameResult)(-(int)beta), (GameResult)(-(int)newAlpha), timeLimit, out timeout));
                else
                    score = (GameResult)(-(int)SearchFastly(board, (GameResult)(-(int)beta), (GameResult)(-(int)newAlpha), timeLimit, out timeout));
                board.SetBitboard(bitboard);

                if (score > bestScore)
                {
                    if (score > newAlpha)
                        newAlpha = score;
                    bestScore = score;
                }
                if (bestScore >= beta)
                {
                    this.transpositionTable.SetEntry(hashCode, alpha, GameResult.Win);
                    return bestScore;
                }
            }

            if (bestScore > alpha)
                this.transpositionTable.SetEntry(hashCode, bestScore, bestScore);
            else
                this.transpositionTable.SetEntry(hashCode, GameResult.Loss, bestScore);

            return bestScore;
        }

        [SkipLocalsInit]
        unsafe GameResult SearchFastly(FastBoard board, GameResult lowerBound, GameResult upperBound, int timeLimit, out bool timeout)
        {
            if (this.SearchEllapsedMilliSec >= timeLimit)
            {
                timeout = true;
                return 0.0f;
            }
            timeout = false;

            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            if (posNum == 1 && positions[0] == BoardPosition.Pass && board.GetNextPositionsCandidatesNumAfter(BoardPosition.Pass) == 0)  // gameover
            {
                this.LeafNodeCount++;
                var playerCount = board.GetCurrentPlayerDiscCount();
                var opponentCount = board.GetOpponentPlayerDiscCount();
                GameResult result;
                if (playerCount == opponentCount)
                    result = GameResult.Draw;
                else
                    result = (playerCount > opponentCount) ? GameResult.Win : GameResult.Loss;
                return result;
            }

            this.InternalNodeCount++;
            var bitboard = board.GetBitboard();
            var emptyCount = board.GetEmptyCount();
            for (var i = 0; i < posNum; i++)
            {
                var pos = positions[i];
                board.Update(pos);
                GameResult score;
                score = (GameResult)(-(int)SearchFastly(board, (GameResult)(-(int)upperBound), (GameResult)(-(int)lowerBound), timeLimit, out timeout));
                board.SetBitboard(bitboard);

                if (score > lowerBound)
                    lowerBound = score;
                if (lowerBound >= upperBound)
                    return lowerBound;
            }

            return lowerBound;
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void SortPositions(FastBoard board, Span<BoardPosition> positions, int posNum)
        {
            Span<int> nextPosNums = stackalloc int[posNum];
            for (var i = 0; i < nextPosNums.Length; i++)
                nextPosNums[i] = board.GetNextPositionsCandidatesNumAfter(positions[i]);

            for(var i = 1; i < nextPosNums.Length; i++)
            {
                if(nextPosNums[i - 1] > nextPosNums[i])
                {
                    var j = i;
                    var tmpPos = positions[i];
                    var tmpNextPosNum = nextPosNums[i];
                    do
                    {
                        nextPosNums[j] = nextPosNums[j - 1];
                        positions[j] = positions[j - 1];
                        j--;
                    } while (j > 0 && nextPosNums[j - 1] > tmpNextPosNum);
                    positions[j] = tmpPos;
                    nextPosNums[j] = tmpNextPosNum;
                }
            }
        }
    }
}
