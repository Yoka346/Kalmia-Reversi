using System;
using System.Runtime.CompilerServices;

using Kalmia.Reversi;

namespace Kalmia.EndGameSolver
{
    /// <summary>
    /// Provides reversi final disc diffrence solver.
    /// </summary>
    public class FinalDiscDifferenceSolver : IEndGameSolver
    {
        const int FAST_SEARCH_THRESHOLD = 6;
        const sbyte MAX_SCORE = 64;
        const sbyte MIN_SCORE = -64;

        TranspositionTable<sbyte> transpositionTable;
        int searchStartTime;
        int searchEndTime;

        public ulong InternalNodeCount { get; private set; }
        public ulong LeafNodeCount { get; private set; }
        public bool IsSearching { get; private set; }
        public int SearchEllapsedMilliSec { get { return this.IsSearching ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }
        public float Nps { get { return (this.InternalNodeCount + this.LeafNodeCount) / (this.SearchEllapsedMilliSec * 1.0e-3f); } }

        public FinalDiscDifferenceSolver(ulong maxMemorySize)
        {
            this.transpositionTable = new TranspositionTable<sbyte>(maxMemorySize);
        }

        public void ClearSearchResults()
        {
            this.transpositionTable.Clear();
        }

        public BoardCoordinate SolveBestMove(FastBoard rootState, int timeLimitSentiSec, out sbyte finalDiscDiff, out bool timeout)
        {
            var board = new FastBoard(rootState);
            var bitboard = board.GetBitboard();
            Span<BoardCoordinate> positions = stackalloc BoardCoordinate[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            SortPositions(board, positions, posNum);

            if (posNum == 0)
            {
                finalDiscDiff = (sbyte)(board.GetCurrentPlayerDiscCount() - board.GetOpponentPlayerDiscCount());
                timeout = false;
                return BoardCoordinate.Null;
            }

            var bestPos = positions[0];
            var bestScore = MIN_SCORE;
            this.InternalNodeCount = 0UL;
            this.LeafNodeCount = 0UL;
            this.searchStartTime = Environment.TickCount;
            timeout = false;
            this.IsSearching = true;
            for (var i = 0; i < posNum; i++)
            {
                board.Update(positions[i]);
                var score = (sbyte)-SearchWithTranspositionTable(board, MIN_SCORE, (sbyte)-bestScore, timeLimitSentiSec * 10, out timeout);
                if (timeout)
                {
                    this.IsSearching = false;
                    this.searchEndTime = Environment.TickCount;
                    finalDiscDiff = bestScore;
                    return bestPos;
                }
                board.SetBitboard(bitboard);

                if (score > bestScore)
                {
                    bestPos = positions[i];
                    bestScore = score;
                }
            }

            this.IsSearching = false;
            this.searchEndTime = Environment.TickCount;
            finalDiscDiff = bestScore;
            return bestPos;
        }


        [SkipLocalsInit]
        unsafe sbyte SearchWithTranspositionTable(FastBoard board, sbyte alpha, sbyte beta, int timeLimitMilliSec, out bool timeout)
        {
            if (this.SearchEllapsedMilliSec >= timeLimitMilliSec)
            {
                timeout = true;
                return 0;
            }
            timeout = false;

            TTEntry<sbyte>? ttEntry = null;
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

            Span<BoardCoordinate> positions = stackalloc BoardCoordinate[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            if (posNum == 1 && positions[0] == BoardCoordinate.Pass && board.GetNextPositionsCandidatesNumAfter(BoardCoordinate.Pass) == 0)  // gameover
            {
                this.LeafNodeCount++;
                var playerCount = board.GetCurrentPlayerDiscCount();
                var opponentCount = board.GetOpponentPlayerDiscCount();
                var score = (sbyte)(board.GetCurrentPlayerDiscCount() - board.GetOpponentPlayerDiscCount());
                this.transpositionTable.SetEntry(hashCode, score, score);
                return score;
            }

            this.InternalNodeCount++;
            var bitboard = board.GetBitboard();
            var emptyCount = board.GetEmptyCount();
            var enableNextTransposition = (emptyCount - 1) > FAST_SEARCH_THRESHOLD;
            SortPositions(board, positions, posNum);
            var newAlpha = alpha;
            var bestScore = MIN_SCORE;
            for (var i = 0; i < posNum; i++)
            {
                var pos = positions[i];
                board.Update(pos);
                sbyte score;
                if (enableNextTransposition)
                    score = (sbyte)-SearchWithTranspositionTable(board, (sbyte)-beta, (sbyte)-newAlpha, timeLimitMilliSec, out timeout);
                else
                    score = (sbyte)-SearchFastly(board, (sbyte)-beta, (sbyte)-newAlpha, timeLimitMilliSec, out timeout);
                board.SetBitboard(bitboard);

                if (score >= beta)   // beta cut
                {
                    this.transpositionTable.SetEntry(hashCode, score, MAX_SCORE);
                    return score;
                }

                if (score > bestScore)
                {
                    if (score > newAlpha)   // shrinks window
                        newAlpha = score;
                    bestScore = score;
                }
            }

            if (bestScore > alpha)  // Found best score in the search window([alpha, beta]), so best score is true score.
                this.transpositionTable.SetEntry(hashCode, bestScore, bestScore);
            else    // Found best score out of the search window.
                this.transpositionTable.SetEntry(hashCode, MIN_SCORE, bestScore);

            return bestScore;
        }

        sbyte SearchFastly(FastBoard board, sbyte alpha, sbyte beta, int timeLimit, out bool timeout)
        {
            if (this.SearchEllapsedMilliSec >= timeLimit)
            {
                timeout = true;
                return 0;
            }
            timeout = false;

            var mobility = board.GetCurrentPlayerMobility(out int mobilityNum);
            if (mobilityNum == 0)
            {
                if (board.GetNextPositionsCandidatesNumAfter(BoardCoordinate.Pass) == 0)  // gameover
                {
                    this.LeafNodeCount++;
                    return (sbyte)(board.GetCurrentPlayerDiscCount() - board.GetOpponentPlayerDiscCount());
                }
                else    // pass
                {
                    board.SwitchSideToMove();
                    var score = (sbyte)-SearchFastly(board, (sbyte)-beta, (sbyte)-alpha, timeLimit, out timeout);
                    board.SwitchSideToMove();
                    return (score > alpha) ? score : alpha;
                }
            }

            this.InternalNodeCount++;
            var bitboard = board.GetBitboard();
            var posCount = 0;
            var mask = 1UL;
            for (var pos = BoardCoordinate.A1; posCount < mobilityNum; pos++)
            {
                if ((mobility & mask) != 0)
                {
                    board.Update(pos);
                    sbyte score;
                    score = (sbyte)-SearchFastly(board, (sbyte)-beta, (sbyte)-alpha, timeLimit, out timeout);
                    board.SetBitboard(bitboard);

                    if (score > alpha)
                        alpha = score;
                    if (alpha >= beta)
                        return alpha;
                    posCount++;
                }
                mask <<= 1;
            }

            return alpha;
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void SortPositions(FastBoard board, Span<BoardCoordinate> positions, int posNum)
        {
            Span<int> nextPosNums = stackalloc int[posNum];
            for (var i = 0; i < nextPosNums.Length; i++)
                nextPosNums[i] = board.GetNextPositionsCandidatesNumAfter(positions[i]);

            for (var i = 1; i < nextPosNums.Length; i++)
            {
                if (nextPosNums[i - 1] > nextPosNums[i])
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
