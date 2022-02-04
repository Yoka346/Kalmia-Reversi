using System.Linq;

using Kalmia.Reversi;

namespace KalmiaTest
{
    class SlowBoard  // Low speed implementation of board. This is only used for test.
    {
        public const int LINE_LENGTH = 8;
        public const int SQUARE_NUM = LINE_LENGTH * LINE_LENGTH;

        readonly DiscColor[,] DISCS = new DiscColor[LINE_LENGTH, LINE_LENGTH];

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
            Put(color, pos % LINE_LENGTH, pos / LINE_LENGTH);
        }

        public void Update(Move move)
        {
            var posX = (int)move.Pos % LINE_LENGTH;
            var posY = (int)move.Pos / LINE_LENGTH;

            if(move.Pos != BoardPosition.Pass)
            {
                var flip = CalculateFlippedDiscs(posX, posY);
                this.DISCS[posX, posY] = this.SideToMove;
                for (var x = 0; x < flip.GetLength(0); x++)
                    for (var y = 0; y < flip.GetLength(1); y++)
                        if(flip[x, y])
                            this.DISCS[x, y] = this.SideToMove;
            }
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

        public int GetNextMoves(Move[] moves)
        {
            var mobility = CalculateMobility(this.SideToMove);
            var moveCount = 0;
            for (var x = 0; x < mobility.GetLength(0); x++)
                for (var y = 0; y < mobility.GetLength(1); y++)
                    if (mobility[x, y])
                        moves[moveCount++] = new Move(this.SideToMove, (BoardPosition)(x + y * LINE_LENGTH));

            if (moveCount == 0)
            {
                var opponentMobility = CalculateMobility(this.SideToMove ^ DiscColor.White);
                for (var x = 0; x < opponentMobility.GetLength(0); x++)
                    for (var y = 0; y < opponentMobility.GetLength(1); y++)
                        if (opponentMobility[x, y])
                        {
                            moves[0] = new Move(this.SideToMove, BoardPosition.Pass);
                            return 1;
                        }
            }

            return moveCount;
        }

        bool[,] CalculateMobility(DiscColor currentDiscColor)
        {
            var opponentDiscColor = currentDiscColor ^ DiscColor.White;
            var mobility = new bool[LINE_LENGTH, LINE_LENGTH];
            for (var posX = 0; posX < mobility.GetLength(0); posX++)
                for (var posY = 0; posY < mobility.GetLength(1); posY++)
                {
                    if (this.DISCS[posX, posY] != DiscColor.Null)
                        continue;

                    // right 
                    if (posX != LINE_LENGTH - 1 && this.DISCS[posX + 1, posY] == opponentDiscColor)
                        for (var x = posX + 2; x < LINE_LENGTH; x++)
                            if (this.DISCS[x, posY] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[x, posY] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // left 
                    if (posX != 0 && this.DISCS[posX - 1, posY] == opponentDiscColor)
                        for (var x = posX - 2; x >= 0; x--)
                            if (this.DISCS[x, posY] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[x, posY] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // downward
                    if (posY != LINE_LENGTH - 1 && this.DISCS[posX, posY + 1] == opponentDiscColor)
                        for (var y = posY + 2; y < LINE_LENGTH; y++)
                            if (this.DISCS[posX, y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[posX, y] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // upward
                    if (posY != 0 && this.DISCS[posX, posY - 1] == opponentDiscColor)
                        for (var y = posY - 2; y >= 0; y--)
                            if (this.DISCS[posX, y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[posX, y] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    (int x, int y) pos = (posX, posY);
                    // diagonal(upper left to lower right)
                    if (pos.x != LINE_LENGTH - 1 && pos.y != LINE_LENGTH - 1 && this.DISCS[pos.x + 1, pos.y + 1] == opponentDiscColor)
                        for ((int x, int y) p = (pos.x + 2, pos.y + 2); p.x - LINE_LENGTH!= 0 && p.y - LINE_LENGTH != 0; p = (p.x + 1, p.y + 1))
                            if (this.DISCS[p.x, p.y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(lower right to upper left)
                    if (pos.x != 0 && pos.y != 0 && this.DISCS[pos.x - 1, pos.y - 1] == opponentDiscColor)
                        for ((int x, int y) p = (pos.x - 2, pos.y - 2); p.x != -1 && p.y != -1; p = (p.x - 1, p.y - 1))
                            if (this.DISCS[p.x, p.y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(upper right to lower left)
                    if (pos.x != 0 && pos.y != LINE_LENGTH - 1 && this.DISCS[pos.x - 1, pos.y + 1] == opponentDiscColor)
                        for ((int x, int y) p = (pos.x - 2, pos.y + 2); p.x != -1 && p.y - LINE_LENGTH != 0; p = (p.x - 1, p.y + 1))
                            if (this.DISCS[p.x, p.y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(lower left to upper right)
                    if (pos.x != LINE_LENGTH - 1 && pos.y != 0 && this.DISCS[pos.x + 1, pos.y - 1] == opponentDiscColor)
                        for ((int x, int y) p = (pos.x + 2, pos.y - 2); p.x - LINE_LENGTH != 0 && p.y !=-1; p = (p.x + 1, p.y - 1))
                            if (this.DISCS[p.x, p.y] == currentDiscColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                                break;
                }
            return mobility;
        }

        bool[,] CalculateFlippedDiscs(int posX, int posY)
        {
            var currentDiscColor = this.SideToMove;
            var opponentDiscColor = this.SideToMove ^ DiscColor.White;
            var flipped = new bool[LINE_LENGTH, LINE_LENGTH];

            // right 
            if (posX != LINE_LENGTH - 1 && this.DISCS[posX + 1, posY] == opponentDiscColor)
                for (var x = posX + 2; x < LINE_LENGTH; x++)
                {
                    if (this.DISCS[x, posY] == currentDiscColor)
                    {
                        for (var xx = x - 1; xx > posX; xx--)
                            flipped[xx, posY] = true;
                        break;
                    }
                    else if (this.DISCS[x, posY] == DiscColor.Null)
                        break;
                }

            // left 
            if (posX != 0 && this.DISCS[posX - 1, posY] == opponentDiscColor)
                for (var x = posX - 2; x >= 0; x--)
                    if (this.DISCS[x, posY] == currentDiscColor)
                    {
                        for (var xx = x + 1; xx < posX; xx++)
                            flipped[xx, posY] = true;
                        break;
                    }
                    else if (this.DISCS[x, posY] == DiscColor.Null)
                        break;

            // downward
            if (posY != LINE_LENGTH - 1 && this.DISCS[posX, posY + 1] == opponentDiscColor)
                for (var y = posY + 2; y < LINE_LENGTH; y++)
                    if (this.DISCS[posX, y] == currentDiscColor)
                    {
                        for (var yy = y - 1; yy > posY; yy--)
                            flipped[posX, yy] = true;
                        break;
                    }
                    else if (this.DISCS[posX, y] == DiscColor.Null)
                        break;

            // upward
            if (posY != 0 && this.DISCS[posX, posY - 1] == opponentDiscColor)
                for (var y = posY - 2; y >= 0; y--)
                    if (this.DISCS[posX, y] == currentDiscColor)
                    {
                        for (var yy = y + 1; yy < posY; yy++)
                            flipped[posX, yy] = true;
                        break;
                    }
                    else if (this.DISCS[posX, y] == DiscColor.Null)
                        break;

            (int x, int y) pos = (posX, posY);
            // diagonal(upper left to lower right)
            if (pos.x != LINE_LENGTH - 1 && pos.y != LINE_LENGTH - 1 && this.DISCS[pos.x + 1, pos.y + 1] == opponentDiscColor)
                for ((int x, int y) p = (pos.x + 2, pos.y + 2); p.x - LINE_LENGTH != 0 && p.y - LINE_LENGTH != 0; p = (p.x + 1, p.y + 1))
                    if (this.DISCS[p.x, p.y] == currentDiscColor)
                    {
                        for ((int x, int y) pp = (p.x - 1, p.y - 1); pp.x - posX != 0 && pp.y - posY != 0; pp = (pp.x - 1, pp.y - 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                        break;

            // diagonal(lower right to upper left)
            if (pos.x != 0 && pos.y != 0 && this.DISCS[pos.x - 1, pos.y - 1] == opponentDiscColor)
                for ((int x, int y) p = (pos.x - 2, pos.y - 2); p.x != -1 && p.y != -1; p = (p.x - 1, p.y - 1))
                    if (this.DISCS[p.x, p.y] == currentDiscColor)
                    {
                        for ((int x, int y) pp = (p.x + 1, p.y + 1); pp.x - pos.x != 0 && pp.y - pos.y != 0; pp = (pp.x + 1, pp.y + 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                        break;

            // diagonal(upper right to lower left)
            if (pos.x != 0 && pos.y != LINE_LENGTH - 1 && this.DISCS[pos.x - 1, pos.y + 1] == opponentDiscColor)
                for ((int x, int y) p = (pos.x - 2, pos.y + 2); p.x != -1 && p.y - LINE_LENGTH != 0; p = (p.x - 1, p.y + 1))
                    if (this.DISCS[p.x, p.y] == currentDiscColor)
                    {
                        for ((int x, int y) pp = (p.x + 1, p.y - 1); pp.x - pos.x != 0 && pp.y - pos.y != 0; pp = (pp.x + 1, pp.y - 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                        break;

            // diagonal(lower left to upper right)
            if (pos.x != LINE_LENGTH - 1 && pos.y != 0 && this.DISCS[pos.x + 1, pos.y - 1] == opponentDiscColor)
                for ((int x, int y) p = (pos.x + 2, pos.y - 2); p.x - LINE_LENGTH != 0 && p.y != -1; p = (p.x + 1, p.y - 1))
                    if (this.DISCS[p.x, p.y] == currentDiscColor)
                    {
                        for ((int x, int y) pp = (p.x - 1, p.y + 1); pp.x - pos.x != 0 && pp.y - pos.y != 0; pp = (pp.x - 1, pp.y + 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == DiscColor.Null)
                        break;
            return flipped;
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * LINE_LENGTH;
        }
    }
}

