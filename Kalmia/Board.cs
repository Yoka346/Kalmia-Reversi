using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace Kalmia
{
    public enum InitialBoardState
    {
        Cross,
        Parallel
    }

    public class Board
    {
        public const int LINE_LENGTH = 8;
        public const int GRID_NUM = LINE_LENGTH * LINE_LENGTH;

        ulong currentPlayersBoard;
        ulong opponentPlayersBoard;

        public Color Turn { get; private set; }

        public Board(Color firstPlayer, InitialBoardState initState) : this(firstPlayer, 0UL, 0UL)
        {
            var secondPlayer = (Color)(-(int)firstPlayer);
            if(initState == InitialBoardState.Cross)
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

        public Board(Color firstPlayer, ulong firstPlayersBoard, ulong secondPlayersBoard)
        {
            this.Turn = firstPlayer;
            this.currentPlayersBoard = firstPlayersBoard;
            this.opponentPlayersBoard = secondPlayersBoard;
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
            Put(color, posX + LINE_LENGTH * posY);
        }

        public void Put(Color color, int pos)
        {
            var putPat = 1UL << pos;
            if (color == this.Turn)
                this.currentPlayersBoard |= putPat;
            else
                this.opponentPlayersBoard |= putPat;
        }

        ulong CalculateMobility()
        {
            throw new NotImplementedException();
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * LINE_LENGTH;
        }
    }
}
