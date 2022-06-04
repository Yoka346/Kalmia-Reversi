using System;
using System.Linq;
using System.Collections.Generic;

using Kalmia.Reversi;

namespace KalmiaTest
{
    class SlowBoard  // Low speed implementation of board. This is only used for test.
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;

        readonly DiscColor[,] DISCS = new DiscColor[BOARD_SIZE, BOARD_SIZE];

        public DiscColor SideToMove { get; private set; }

        public SlowBoard(DiscColor firstPlayer, InitialBoardState initState)
        {
            for (var i = 0; i < this.DISCS.GetLength(0); i++)
                for (var j = 0; j < this.DISCS.GetLength(1); j++)
                    this.DISCS[i, j] = DiscColor.Null;

            var secondPlayer = firstPlayer ^ DiscColor.White;
            if (initState == InitialBoardState.Cross)
            {
                Put(firstPlayer, "E4");
                Put(firstPlayer, "D5");
                Put(secondPlayer, "D4");
                Put(secondPlayer, "E5");
            }
            else
            {
                Put(firstPlayer, "D5");
                Put(firstPlayer, "E5");
                Put(secondPlayer, "D4");
                Put(secondPlayer, "E4");
            }
            this.SideToMove = firstPlayer;
        }

        public static (int x, int y) ConvertBoardPositionToCoordinate(BoardCoordinate pos)
        {
            return ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
        }

        public DiscColor GetDiscColor(int x, int y)
        {
            return this.DISCS[x, y];
        }

        public DiscColor[,] GetDiscsArray()
        {
            return (DiscColor[,])this.DISCS.Clone();
        }

        public void SwitchSideToMove()
        {
            this.SideToMove ^= DiscColor.White;
        }

        public void Put(DiscColor color, string pos)
        {
            Put(color, StringToPos(pos));
        }

        public void Put(DiscColor color, int posX, int posY)
        {
            this.DISCS[posX, posY] = color;
        }

        public void Put(DiscColor color, int pos)
        {
            Put(color, pos % BOARD_SIZE, pos / BOARD_SIZE);
        }

        public void Update(Move move)
        {
            foreach (var pos in FlipDiscs(this.SideToMove, move.Coord))
                this.DISCS[(int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE] = this.SideToMove ^ DiscColor.White;
            SwitchSideToMove();
        }

        public bool IsGameover()
        {
            return CalculateMobility(DiscColor.Black).Cast<bool>().Where(x => x).Count() == 0 && CalculateMobility(DiscColor.White).Cast<bool>().Where(x => x).Count() == 0;
        }

        public int GetDiscCount(DiscColor color)
        {
            var count = 0;
            foreach (var disc in this.DISCS)
                if (disc == color)
                    count++;
            return count;
        }   

        public Move[] GetNextMoves()
        {
            var mobility = CalculateMobility(this.SideToMove);
            if (mobility.Length == 0)
            {
                if (CalculateMobility(this.SideToMove ^ DiscColor.White).Length > 0)
                    return new Move[1] { new Move(this.SideToMove, BoardCoordinate.Pass) };
                return new Move[0];
            }
            return (from pos in mobility select new Move(this.SideToMove, pos)).ToArray();
        }

        BoardCoordinate[] CalculateMobility(DiscColor color)
        {
            var mobility = new List<BoardCoordinate>();
            for (var pos = 0; pos < this.DISCS.Length; pos++)
            {
                if (this.DISCS[pos % BOARD_SIZE, pos / BOARD_SIZE] != DiscColor.Null)
                    continue;
                if (CheckMobility(color, (BoardCoordinate)pos))
                    mobility.Add((BoardCoordinate)pos);
            }
            return mobility.ToArray();
        }

        bool CheckMobility(DiscColor color, BoardCoordinate pos)
        {
            return CheckMobility(color, pos, 1, 0)
                || CheckMobility(color, pos, -1, 0)
                || CheckMobility(color, pos, 0, 1)
                || CheckMobility(color, pos, 0, -1)
                || CheckMobility(color, pos, 1, 1)
                || CheckMobility(color, pos, -1, -1)
                || CheckMobility(color, pos, -1, 1)
                || CheckMobility(color, pos, 1, -1);
        }

        BoardCoordinate[] FlipDiscs(DiscColor color, BoardCoordinate pos)
        {
            var flipped = new List<BoardCoordinate>();
            FlipDiscs(color, pos, 1, 0, flipped);
            FlipDiscs(color, pos, -1, 0, flipped);
            FlipDiscs(color, pos, 0, 1, flipped);
            FlipDiscs(color, pos, 0, -1, flipped);
            FlipDiscs(color, pos, 1, 1, flipped);
            FlipDiscs(color, pos, -1, -1, flipped);
            FlipDiscs(color, pos, -1, 1, flipped);
            FlipDiscs(color, pos, 1, -1, flipped);
            return flipped.ToArray();
        }

        void FlipDiscs(DiscColor color, BoardCoordinate pos, int dirX, int dirY, List<BoardCoordinate> flipped)
        {
            var oppColor = (DiscColor)(-(int)color);
            (int x, int y) = ConvertBoardPositionToCoordinate(pos);

            if (!CheckMobility(color, pos, dirX, dirY))
                return;

            (int nextX, int nextY) = (x + dirX, y + dirY);
            while (nextX >= 0 && nextX < BOARD_SIZE && nextY >= 0 && nextY < BOARD_SIZE && this.DISCS[nextX, nextY] == oppColor)
            {
                var flippedPos = nextX + nextY * BOARD_SIZE;
                this.DISCS[flippedPos % BOARD_SIZE, flippedPos / BOARD_SIZE] = color;
                flipped.Add((BoardCoordinate)flippedPos);
                nextX += dirX;
                nextY += dirY;
            }
        }

        bool CheckMobility(DiscColor color, BoardCoordinate pos, int dirX, int dirY)
        {
            var oppColor = (DiscColor)(-(int)color);
            (int x, int y) = ConvertBoardPositionToCoordinate(pos);

            (int nextX, int nextY) = (x + dirX, y + dirY);
            Func<int, int, bool> outOfRange = (x, y) => nextX < 0 || nextX >= BOARD_SIZE || nextY < 0 || nextY >= BOARD_SIZE;
            if (outOfRange(nextX, nextY) || this.DISCS[nextX, nextY] != oppColor)
                return false;

            do
            {
                nextX += dirX;
                nextY += dirY;
            } while (!outOfRange(nextX, nextY) && this.DISCS[nextX, nextY] == oppColor);

            if (!outOfRange(nextX, nextY) && this.DISCS[nextX, nextY] == color)
                return true;
            return false;
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * BOARD_SIZE;
        }
    }
}

