using Kalmia.Reversi;
using System;
using System.Diagnostics;

namespace Kalmia
{
    /// <summary>
    /// Provides the time controller for game. 
    /// See also: https://senseis.xmp.net/?CanadianTiming
    /// Source code reference: https://github.com/leela-zero/leela-zero/blob/next/src/TimeControl.cpp
    /// </summary>
    internal class TimeController
    {
        readonly int[] remainingTimeCentiSec = new int[2];
        readonly bool[] inByoYomi = new bool[2];    // Byo yomi is a Japanese Go (Chinese game) term, which means countdown. See also: https://senseis.xmp.net/?ByoYomi
        readonly int[] byoYomiStonesLeft = new int[2];  // Byo yomi stones is the number of moves which can be played in byo yomi. See also: https://senseis.xmp.net/?CanadianTiming
        readonly int[] byoYomiPeriodsLeft = new int[2]; // Byo yomi periods, which is the system of Japanese timing, means the number of byo yomi. See also: https://senseis.xmp.net/?ByoYomi
        readonly Stopwatch[] stopWatch = new Stopwatch[2] { new Stopwatch(), new Stopwatch() };

        int mainTimeCentiSec;
        int byoYomiTimeCentiSec;
        int byoYomiStones;
        int byoYomiPeriods;

        /// <summary>
        /// The latency of GUI or network.
        /// </summary>
        public int LatencyCentiSec { get; set; }

        public TimeController(int mainTimeSec, int byoYomiTimeSec, int byoYomiStone, int byoYomiPeriods, int latencyCentiSec)
        {
            this.mainTimeCentiSec = mainTimeSec * 100;
            this.byoYomiTimeCentiSec = byoYomiTimeSec * 100;
            this.byoYomiStones = byoYomiStone;
            this.byoYomiPeriods = byoYomiPeriods;
            this.LatencyCentiSec = latencyCentiSec;
            Reset();
        }

        public bool InByoYomi(DiscColor color)
        {
            return this.inByoYomi[(int)color];
        }

        public void Start(DiscColor color)
        {
            this.stopWatch[(int)color].Restart();
        }

        public void Stop(DiscColor color)
        {
            var sw = this.stopWatch[(int)color];
            sw.Stop();
            var ellapsedCentiSec = (int)(sw.ElapsedMilliseconds / 10);
            var player = (int)color;
            this.remainingTimeCentiSec[player] -= ellapsedCentiSec;
            if (this.inByoYomi[player])
                if (this.byoYomiStones != 0)
                    this.byoYomiStonesLeft[player]--;
                else if (this.byoYomiPeriods != 0)
                    if (ellapsedCentiSec > this.byoYomiTimeCentiSec)
                        this.byoYomiPeriodsLeft[player]--;

            if (!this.inByoYomi[player] && this.remainingTimeCentiSec[player] <= 0)  // main time is up, then start byo yomi.
            {
                this.remainingTimeCentiSec[player] = this.byoYomiTimeCentiSec;
                this.byoYomiStonesLeft[player] = this.byoYomiStones;
                this.byoYomiPeriodsLeft[player] = this.byoYomiPeriods;
                this.inByoYomi[player] = true;
            }
            else if (this.inByoYomi[player] && this.byoYomiStones != 0 && this.byoYomiStonesLeft[player] <= 0)  // played all byo yomi stones, then reset byo yomi time and stones.
            {
                this.remainingTimeCentiSec[player] = this.byoYomiTimeCentiSec;
                this.byoYomiStonesLeft[player] = this.byoYomiStones;
            }
            else if (this.inByoYomi[player] && this.byoYomiPeriods != 0)
                this.remainingTimeCentiSec[player] = this.byoYomiTimeCentiSec;
        }

        public void Reset()
        {
            this.remainingTimeCentiSec[(int)DiscColor.Black] = this.remainingTimeCentiSec[(int)DiscColor.White] = this.mainTimeCentiSec;
            this.byoYomiStonesLeft[(int)DiscColor.Black] = this.byoYomiStonesLeft[(int)DiscColor.White] = this.byoYomiStones;
            this.byoYomiPeriodsLeft[(int)DiscColor.Black] = this.byoYomiPeriodsLeft[(int)DiscColor.White] = this.byoYomiPeriods;
            this.inByoYomi[(int)DiscColor.Black] = this.inByoYomi[(int)DiscColor.White] = this.mainTimeCentiSec <= 0;
            for (var color = DiscColor.Black; color <= DiscColor.White; color++)
                if (this.inByoYomi[(int)color])
                    this.remainingTimeCentiSec[(int)color] = this.byoYomiTimeCentiSec;
        }

        public int GetMaxTimeCentiSecForMove(DiscColor color, int emptyCount)
        {
            var player = (int)color;
            var remainingTime = this.remainingTimeCentiSec[player];
            var remainingMoves = (emptyCount % 2 == 0) ? emptyCount / 2 : emptyCount / 2 + 1;
            var extraTimePerMove = 0;

            if (this.byoYomiTimeCentiSec != 0)
            {
                if (this.byoYomiStones == 0 && this.byoYomiPeriods == 0)
                    return 7 * 24 * 60 * 60 * 100; // 1 weeks

                if (this.inByoYomi[player])
                {
                    if (this.byoYomiStones != 0)
                        remainingMoves = this.byoYomiStonesLeft[player];
                    else
                    {
                        remainingTime = 0;
                        extraTimePerMove = this.byoYomiTimeCentiSec;
                    }
                }
                else
                {
                    if(this.byoYomiStones != 0)
                    {
                        var byoYomiExtra = (this.byoYomiStones != 1) ? this.byoYomiTimeCentiSec / this.byoYomiStones : 0;
                        remainingTime = this.remainingTimeCentiSec[player] + byoYomiExtra;
                        extraTimePerMove = byoYomiExtra;
                    }
                    else
                    {
                        var byoYomiExtra = this.byoYomiTimeCentiSec * (this.byoYomiPeriodsLeft[player] - 1);
                        remainingTime = this.remainingTimeCentiSec[player] + byoYomiExtra;
                        extraTimePerMove = this.byoYomiTimeCentiSec;
                    }
                }
            }

            var baseTime = Math.Max(remainingTime - this.LatencyCentiSec, 0) / Math.Max(remainingMoves, 1);
            var incTime = Math.Max(extraTimePerMove - this.LatencyCentiSec, 0);
            return baseTime + incTime;
        }

        public void AdjustTime(DiscColor color, int timeSec, int stones)
        {
            var player = (int)color;
            this.remainingTimeCentiSec[player] = timeSec * 100;
            if(timeSec == 0 && stones == 0)
            {
                this.inByoYomi[player] = true;
                this.remainingTimeCentiSec[player] = this.byoYomiTimeCentiSec;
                this.byoYomiStonesLeft[player] = this.byoYomiStones;
                this.byoYomiPeriodsLeft[player] = this.byoYomiPeriods;
            }

            if (stones != 0)
                this.inByoYomi[player] = true;

            if (this.inByoYomi[player])
                if (this.byoYomiStones != 0)
                    this.byoYomiStonesLeft[player] = stones;
                else if (this.byoYomiPeriods != 0)
                    this.byoYomiPeriodsLeft[player] = stones;
        }
    }
}
