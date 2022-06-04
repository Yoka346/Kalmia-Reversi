using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;

namespace Kalmia.Engines
{
    /// <summary>
    /// Provides reversi ruler for GoGui(https://github.com/Remi-Coulom/gogui).
    /// </summary>
    public class ReversiRuler : GTPEngine 
    {
        const string _NAME = "ReversiRuler";
        const string _VERSION = "0.0";

        public ReversiRuler() : base(_NAME, _VERSION) { }

        public override void Quit() { }

        public override void ClearBoard() => this.board = new Board(DiscColor.Black);

        public override bool SetBoardSize(int size) => size == Board.BOARD_SIZE;

        public override Move GenerateMove(DiscColor color) => throw new NotImplementedException();

        public override Move RegGenerateMove(DiscColor color) => throw new NotImplementedException();

        public override void SetTime(int mainTime, int byoYomiTime, int byoYomiStones) => throw new NotImplementedException();

        public override void SendTimeLeft(DiscColor color, int timeLeft, int byoYomiStonesLeft) => throw new NotImplementedException();
    }
}
