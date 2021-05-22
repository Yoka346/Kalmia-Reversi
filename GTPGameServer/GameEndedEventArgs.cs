using System;
using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public class GameEndedEventArgs : EventArgs
    {
        public GameError Error { get; private set; }
        public string Message { get; private set; }
        public bool Draw { get; private set; }
        public string WinnerName { get; private set; }
        public Color WinnerColor { get; private set; }
        public int DiscDifference { get; private set; }

        public GameEndedEventArgs(GameError error, string message, bool draw, string winnerName, Color winnerColor, int discDifference)
        {
            this.Error = error;
            this.Message = message;
            this.Draw = draw;
            this.WinnerName = winnerName;
            this.WinnerColor = winnerColor;
            this.DiscDifference = discDifference;
        }
    }
}
