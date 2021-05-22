using System;
using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public class PlayerMovedEventArgs : EventArgs
    {
        public string PlayerName { get; private set; }
        public Move Move { get; private set; }

        public PlayerMovedEventArgs(string playerName, Move move)
        {
            this.PlayerName = playerName;
            this.Move = move;
        }
    }
}
