using Kalmia.Reversi;
using System;
using System.Runtime.CompilerServices;

namespace Kalmia.EndGameSolver
{
    /// <summary>
    /// Provides reversi mate solver(Solves winner not max discs difference.)
    /// </summary>
    internal class MateSolver
    {
        const int STOP_MOVE_ORDERING_THRESHOLD = 6;
        const int STOP_TRANSPOSITION_THRESHOLD = 6;

        TranspositionTable<GameResult> transpositionTable;
        ulong npsCounter;
        int searchStartTime;
        int searchEndTime;

        public bool IsSearching { get; private set; }
        public int SearchEllapsedMilliSec { get { return this.IsSearching ? Environment.TickCount - this.searchStartTime : this.searchEndTime - this.searchStartTime; } }
        public float Nps { get { return this.npsCounter / (this.SearchEllapsedMilliSec * 1.0e-3f); } }

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
            this.searchStartTime = Environment.TickCount;
            timeout = false;
            this.IsSearching = true;
            for(var i = 0; i < posNum; i++)
            {
                board.Update(positions[i]);
                var score = (GameResult)(-(int)Search(board, GameResult.Loss, (GameResult)(-(int)bestScore), timeLimit, out timeout));
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
        unsafe GameResult Search(FastBoard board, GameResult lowerBound, GameResult upperBound, int timeLimit, out bool timeout)
        {
            if(this.SearchEllapsedMilliSec >= timeLimit)
            {
                timeout = true;
                return 0.0f;
            }
            timeout = false;

            TTEntry<GameResult>? ttEntry = null;
            var emptyCount = board.GetEmptyCount();
            var enableTransposition = emptyCount > STOP_TRANSPOSITION_THRESHOLD;
            var hashCode = 0UL;
            if (enableTransposition)
            {
                hashCode = board.GetHashCode();
                ttEntry = this.transpositionTable.GetEntry(hashCode);
            }

            if (ttEntry.HasValue)   // hash hit
            {
                if (upperBound <= ttEntry.Value.LowerBound)
                    return ttEntry.Value.LowerBound;

                if (lowerBound >= ttEntry.Value.UpperBound)
                    return ttEntry.Value.UpperBound;

                if (ttEntry.Value.UpperBound == ttEntry.Value.LowerBound)
                    return ttEntry.Value.LowerBound;

                if (ttEntry.Value.LowerBound > lowerBound)
                    lowerBound = ttEntry.Value.LowerBound;

                if (ttEntry.Value.UpperBound < upperBound)
                    upperBound = ttEntry.Value.UpperBound;
            }

            GameResult result;
            if ((result = board.GetGameResult()) != GameResult.NotOver)
            {
                var score = result;
                if(enableTransposition)
                    this.transpositionTable.SetEntry(hashCode, score, score);
                return score;
            }

            var bitboard = board.GetBitboard();
            Span<BoardPosition> positions = stackalloc BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var posNum = board.GetNextPositionCandidates(positions);
            if (emptyCount > STOP_MOVE_ORDERING_THRESHOLD)
                SortPositions(board, positions, posNum);

            for(var i = 0; i < posNum; i++)
            {
                var pos = positions[i];
                board.Update(pos);
                this.npsCounter++;
                var score = (GameResult)(-(int)Search(board, (GameResult)(-(int)upperBound), (GameResult)(-(int)lowerBound), timeLimit, out timeout));
                board.SetBitboard(bitboard);

                if (score > lowerBound)
                    lowerBound = score;
                if (lowerBound >= upperBound)
                {
                    if (enableTransposition)
                        this.transpositionTable.SetEntry(hashCode, lowerBound, GameResult.Win);
                    return lowerBound;
                }
            }

            if(enableTransposition)
                this.transpositionTable.SetEntry(hashCode, lowerBound, upperBound);

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
