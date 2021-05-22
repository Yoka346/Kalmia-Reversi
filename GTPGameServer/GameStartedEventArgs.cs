using System;
using System.Collections.Generic;
using System.Text;

namespace GTPGameServer
{
    public class GameStartedEventArgs : EventArgs
    {
        public int GameID { get; private set; }
        public string BlackPlayerName { get; private set; }
        public string WhitePlayerName { get; private set; }

        public GameStartedEventArgs(int gameID, string blackPlayerName, string whitePlayerName)
        {
            this.GameID = gameID;
            this.BlackPlayerName = blackPlayerName;
            this.WhitePlayerName = whitePlayerName;
        }
    }
}
