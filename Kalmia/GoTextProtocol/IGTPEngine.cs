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
        bool SetBoardSize(int size);
        void ClearBoard(InitialBoardState initPos);
        void Play(Color color, int posX, int posY);
        void Put(Color color, int posX, int posY);
        List<(int x, int y)> SetHandicap(int num);
        string LoadSGF(string path);
        string LoadSGF(string path, int posX, int posY);
        string LoadSGF(string path, int moveNum);
        string GenerateMove(Color color);
        string RegGenerateMove(Color color);
        void Undo();
        void SetTime(int mainTime, int countdownTime, int countdownNum);
        void SendTimeLeft(int timeLeft, int countdownNumLeft);
        string GetFinalScore();
        Color GetColor(int posX, int posY);
        string ExecuteOriginalCommand(string command, string[] args);   //args contains command's name.
        string[] GetOriginalCommands();
    }
}
