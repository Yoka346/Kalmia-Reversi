using Kalmia;
using static Kalmia.Move;
using static KalmiaTest.BoardForTest;

namespace KalmiaTest
{
    class BoardForTest  // Low speed implementation of board. This is only used for test.
    {
        public const int LINE_LENGTH = 8;
        public const int GRID_NUM = LINE_LENGTH * LINE_LENGTH;

        readonly Color[,] DISCS = new Color[LINE_LENGTH, LINE_LENGTH];

        public Color Turn { get; private set; }

        public BoardForTest(Color firstPlayer, InitialBoardState initState)
        {
            var secondPlayer = (Color)(-(int)firstPlayer);
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
        }

        public Color[,] GetDiscsArray()
        {
            return (Color[,])DISCS.Clone();
        }

        public void SwitchTurn()
        {
            this.Turn = (Color)(-(int)this.Turn);
        }

        public void Put(Color color, string pos)
        {
            Put(color, StringToPos(pos));
        }

        public void Put(Color color, int posX, int posY)
        {
            this.DISCS[posX, posY] = color;
        }

        public void Put(Color color, int pos)
        {
            Put(color, pos % LINE_LENGTH, pos / LINE_LENGTH);
        }

        public void Update(Move move)
        {
            var posX = move.Pos % LINE_LENGTH;
            var posY = move.Pos / LINE_LENGTH;
            var flip = CalculateFlippedDiscs(posX, posY);

            if(move.Pos != PASS)
            {
                this.DISCS[move.Pos % LINE_LENGTH, move.Pos / LINE_LENGTH] = this.Turn;
                for (var x = 0; x < flip.GetLength(0); x++)
                    for (var y = 0; y < flip.GetLength(1); y++)
                        this.DISCS[x, y] = this.Turn;
            }
            SwitchTurn();
        }

        public int GetNextMoves(Move[] moves)
        {
            var mobility = CalculateMobility();
            var moveNum = 0;
            for (var x = 0; x < mobility.GetLength(0); x++)
                for (var y = 0; y < mobility.GetLength(1); y++)
                    if (mobility[x, y])
                        moves[moveNum++] = new Move(this.Turn, x + y * LINE_LENGTH);

            if (moveNum == 0)
                moves[0] = new Move(this.Turn, PASS);

            return moveNum;
        }

        bool[,] CalculateMobility()
        {
            var currentColor = this.Turn;
            var opponentColor = (Color)(-(int)this.Turn);
            var mobility = new bool[LINE_LENGTH, LINE_LENGTH];
            for (var posX = 0; posX < mobility.GetLength(0); posX++)
                for (var posY = 0; posY < mobility.GetLength(1); posY++)
                {
                    if (this.DISCS[posX, posY] != Color.Blank)
                        continue;

                    // right 
                    if (posX != LINE_LENGTH - 1 && this.DISCS[posX + 1, posY] == opponentColor)
                        for (var x = posX + 2; x < LINE_LENGTH; x++)
                            if (this.DISCS[x, posY] == currentColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[x, posY] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // left 
                    if (posX != 0 && this.DISCS[posX - 1, posY] == opponentColor)
                        for (var x = posX - 2; x < LINE_LENGTH; x--)
                            if (this.DISCS[x, posY] == currentColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[x, posY] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // downward
                    if (posY != LINE_LENGTH - 1 && this.DISCS[posX, posY + 1] == opponentColor)
                        for (var y = posY + 2; y < LINE_LENGTH; y++)
                            if (this.DISCS[posX, y] == currentColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[posX, y] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // upward
                    if (posY != 0 && this.DISCS[posX, posY - 1] == opponentColor)
                        for (var y = posY - 2; y < LINE_LENGTH; y--)
                            if (this.DISCS[posX, y] == currentColor)
                            {
                                mobility[posX, posY] = true;
                                break;
                            }
                            else if (this.DISCS[posX, y] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    (int x, int y) pos = (posX, posY);
                    // diagonal(upper left to lower right)
                    if (pos != (LINE_LENGTH - 1, LINE_LENGTH - 1) && this.DISCS[pos.x + 1, pos.y + 1] == opponentColor)
                        for ((int x, int y) p = (pos.x + 2, pos.y + 2); (p.x - LINE_LENGTH, p.y - LINE_LENGTH) != (0, 0); p = (p.x + 1, p.y + 1))
                            if (this.DISCS[p.x, p.y] == currentColor)
                            {
                                mobility[p.x, p.y] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(lower right to upper left)
                    if (pos != (0, 0) && this.DISCS[pos.x - 1, pos.y - 1] == opponentColor)
                        for ((int x, int y) p = (pos.x - 2, pos.y - 2); (p.x, p.y) != (-1, -1); p = (p.x - 1, p.y - 1))
                            if (this.DISCS[p.x, p.y] == currentColor)
                            {
                                mobility[p.x, p.y] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(upper right to lower left)
                    if (pos != (0, LINE_LENGTH - 1) && this.DISCS[pos.x - 1, pos.y + 1] == opponentColor)
                        for ((int x, int y) p = (pos.x - 2, pos.y + 2); (p.x, p.y - LINE_LENGTH) != (-1, 0); p = (p.x - 1, p.y + 1))
                            if (this.DISCS[p.x, p.y] == currentColor)
                            {
                                mobility[p.x, p.y] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == Color.Blank)
                                break;

                    if (mobility[posX, posY])
                        continue;

                    // diagonal(lower left to upper right)
                    if (pos != (LINE_LENGTH - 1, 0) && this.DISCS[pos.x + 1, pos.y - 1] == opponentColor)
                        for ((int x, int y) p = (pos.x + 2, pos.y - 2); (p.x - LINE_LENGTH, p.y) != (0, -1); p = (p.x + 1, p.y - 1))
                            if (this.DISCS[p.x, p.y] == currentColor)
                            {
                                mobility[p.x, p.y] = true;
                                break;
                            }
                            else if (this.DISCS[p.x, p.y] == Color.Blank)
                                break;
                }
            return mobility;
        }

        bool[,] CalculateFlippedDiscs(int posX, int posY)
        {
            var currentColor = this.Turn;
            var opponentColor = (Color)(-(int)this.Turn);
            var flipped = new bool[LINE_LENGTH, LINE_LENGTH];
            if (this.DISCS[posX, posY] == Color.Blank)
                return flipped;

            // right 
            if (posX != LINE_LENGTH - 1 && this.DISCS[posX + 1, posY] == opponentColor)
                for (var x = posX + 2; x < LINE_LENGTH; x++)
                    if (this.DISCS[x, posY] == currentColor)
                    {
                        for (var xx = x - 1; x > posX; x--)
                            flipped[xx, posY] = true;
                        break;
                    }
                    else if (this.DISCS[x, posY] == Color.Blank)
                        break;

            // left 
            if (posX != 0 && this.DISCS[posX - 1, posY] == opponentColor)
                for (var x = posX - 2; x < LINE_LENGTH; x--)
                    if (this.DISCS[x, posY] == currentColor)
                    {
                        for (var xx = x + 1; xx < posX; xx++)
                            flipped[xx, posY] = true;
                        break;
                    }
                    else if (this.DISCS[x, posY] == Color.Blank)
                        break;

            // downward
            if (posY != LINE_LENGTH - 1 && this.DISCS[posX, posY + 1] == opponentColor)
                for (var y = posY + 2; y < LINE_LENGTH; y++)
                    if (this.DISCS[posX, y] == currentColor)
                    {
                        for (var yy = y - 1; yy > posY; yy--)
                            flipped[posX, yy] = true;
                        break;
                    }
                    else if (this.DISCS[posX, y] == Color.Blank)
                        break;

            // upward
            if (posY != 0 && this.DISCS[posX, posY - 1] == opponentColor)
                for (var y = posY - 2; y < LINE_LENGTH; y--)
                    if (this.DISCS[posX, y] == currentColor)
                    {
                        for (var yy = y + 1; yy < posY; yy++)
                            flipped[posX, yy] = true;
                        break;
                    }
                    else if (this.DISCS[posX, y] == Color.Blank)
                        break;

            (int x, int y) pos = (posX, posY);
            // diagonal(upper left to lower right)
            if (pos != (LINE_LENGTH - 1, LINE_LENGTH - 1) && this.DISCS[pos.x + 1, pos.y + 1] == opponentColor)
                for ((int x, int y) p = (pos.x + 2, pos.y + 2); (p.x - LINE_LENGTH, p.y - LINE_LENGTH) != (0, 0); p = (p.x + 1, p.y + 1))
                    if (this.DISCS[p.x, p.y] == currentColor)
                    {
                        for ((int x, int y) pp = (p.x - 1, p.y - 1); (pp.x - p.x, pp.y - p.y) != (0, 0); pp = (pp.x - 1, pp.y - 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == Color.Blank)
                        break;

            // diagonal(lower right to upper left)
            if (pos != (0, 0) && this.DISCS[pos.x - 1, pos.y - 1] == opponentColor)
                for ((int x, int y) p = (pos.x - 2, pos.y - 2); (p.x, p.y) != (-1, -1); p = (p.x - 1, p.y - 1))
                    if (this.DISCS[p.x, p.y] == currentColor)
                    {
                        for ((int x, int y) pp = (p.x + 1, p.y + 1); (pp.x - p.x, pp.y - p.y) != (0, 0); pp = (pp.x + 1, pp.y + 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == Color.Blank)
                        break;

            // diagonal(upper right to lower left)
            if (pos != (0, LINE_LENGTH - 1) && this.DISCS[pos.x - 1, pos.y + 1] == opponentColor)
                for ((int x, int y) p = (pos.x - 2, pos.y + 2); (p.x, p.y - LINE_LENGTH) != (-1, 0); p = (p.x - 1, p.y + 1))
                    if (this.DISCS[p.x, p.y] == currentColor)
                    {
                        for ((int x, int y) pp = (p.x + 1, p.y - 1); (pp.x - p.x, pp.y - p.y) != (0, 0); pp = (pp.x + 1, pp.y - 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == Color.Blank)
                        break;

            // diagonal(lower left to upper right)
            if (pos != (LINE_LENGTH - 1, 0) && this.DISCS[pos.x + 1, pos.y - 1] == opponentColor)
                for ((int x, int y) p = (pos.x + 2, pos.y - 2); (p.x - LINE_LENGTH, p.y) != (0, -1); p = (p.x + 1, p.y - 1))
                    if (this.DISCS[p.x, p.y] == currentColor)
                    {
                        for ((int x, int y) pp = (p.x - 1, p.y + 1); (pp.x - p.x, pp.y - p.y) != (0, 0); pp = (pp.x - 1, pp.y + 1))
                            flipped[pp.x, pp.y] = true;
                        break;
                    }
                    else if (this.DISCS[p.x, p.y] == Color.Blank)
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

