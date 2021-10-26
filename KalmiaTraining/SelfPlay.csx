#r "../Kalmia/bin/x64/Release/netcoreapp3.1/Kalmia.dll"
//#r "../Kalmia/bin/x64/Debug/netcoreapp3.1/Kalmia.dll"

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime;
using System.Reflection;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;
using Kalmia.MCTS;

/*
* Helpers
*/

static class KalmiaSelfPlayConfig
{
    public static int NumOfActors { get; } = Environment.ProcessorCount;
    public static int NumOfSamplingMoves { get; } = 60;  // exclude pass
    public static int PlayoutNum { get; } = 3200;
    public static int SearchThreadNum { get; } = 1;

    // UCB
    public static float UCBFactorInit { get; } = 0.25f;
    public static float UCBFactorBase { get; } = 19652.0f;

    // ReplayBuffer
    public static int ReplayBufferSize { get; } = 500000;
}

class ReplayBuffer : IDisposable
{
    StreamWriter sw;

    public int Count { get; private set; }
    public ReplayBuffer(string path)
    {
        this.sw = new StreamWriter(path);
        this.sw.WriteLine("current_player_board,opponent_player_board,next_move,value");
    }

    public void Dispose()
    {
        this.sw.Dispose();
    }

    public void AddEpisode(List<(Bitboard board, Move move, float value)> game)
    {
        lock (this.sw)
        {
            foreach (var n in game)
            {
                ulong currentPlayerBoard, opponentPlayerBoard;
                if (n.move.Color == Color.Black)
                    (currentPlayerBoard, opponentPlayerBoard) = (n.board.CurrentPlayer, n.board.OpponentPlayer);
                else
                    (currentPlayerBoard, opponentPlayerBoard) = (n.board.OpponentPlayer, n.board.CurrentPlayer);
                this.sw.WriteLine($"{currentPlayerBoard},{opponentPlayerBoard},{n.move.Pos},{n.value}");
            }
            this.sw.Flush();
            this.Count++;
        }
    }
}


/*
* Main
*/

const string REPLAY_BUFFER_FILE_PATH = @"";
const string VALUE_FUNC_PARAM_FILE_PATH = @"";

var valueFunc = new ValueFunction(VALUE_FUNC_PARAM_FILE_PATH);
var trees = new UCT[KalmiaSelfPlayConfig.NumOfActors];


var replayBuffer = new ReplayBuffer(REPLAY_BUFFER_FILE_PATH);

void RunEpisode(int threadID)
{
    var gameHistory = new List<(Bitboard board, Move move, float value)>(Board.SQUARE_NUM);
    var tree = trees[threadID];

    var board = new Board(Color.Black, InitialBoardState.Cross);
    GameResult result;
    while ((result = board.GetGameResult(Color.Black)) == GameResult.NotOver)
    {

    }
}