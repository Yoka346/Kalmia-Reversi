using System;
using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public class BoardChangedEventArgs : EventArgs
    {
        public string BoardString { get; private set; }
        Color[,] discs;

        public BoardChangedEventArgs(Color[,] discs, string boardStr)
        {
            this.BoardString = boardStr;
            this.discs = discs;
        }

        public Color[,] GetDiscs()
        {
            return (Color[,])this.discs.Clone();
        }
    }
}
