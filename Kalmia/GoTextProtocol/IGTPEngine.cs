using System;
using System.Collections.Generic;
using System.Text;
using Kalmia.Reversi;

namespace Kalmia.GoTextProtocol
{
    public interface IGTPEngine
    {
        void Quit();
        string GetName();
        string GetVersion();
        int GetBoardSize();
        bool SetBoardSize(int size);
        void ClearBoard();
        bool Play(Move move);
        string LoadSGF(string path);
        string LoadSGF(string path, int posX, int posY);
        string LoadSGF(string path, int moveNum);
        Move GenerateMove(Color color);
        Move RegGenerateMove(Color color);
        public Color GetColor(int posX, int posY);
        string ShowBoard();
        bool Undo();
        string GetFinalResult();
        Move[] GetLegalMoves();
        Color GetSideToMove();
        void SetTime(int mainTime, int byoYomiTime, int byoYomiStones);
        void SendTimeLeft(int timeLeft, int byoYomiStonesLeft);
    }
}
