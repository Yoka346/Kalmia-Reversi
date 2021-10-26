#r "../Kalmia/bin/x64/Release/netcoreapp3.1/Kalmia.dll"
//#r "../Kalmia/bin/x64/Debug/netcoreapp3.1/Kalmia.dll"

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

/*
 * Helper
 */
void CreateTrainDataFile(string gameRecordDir, string outDir, double testDataRate, int moveCountThreshold = 0)      // move count threshold means exculude data before {moveCountThreshold},
                                                                                                                    // for example, moveCountThreshold = 10, data from move 0 to move 9 will be excluded.  
{
    var trnFiles = SearchFilesByExtension(gameRecordDir, ".trn");
    var jouFiles = SearchFilesByExtension(gameRecordDir, ".jou");
    if (trnFiles.Length != 1 || jouFiles.Length != 1)
    {
        Console.WriteLine("trn file or jou file must be one in training data directory.");
        return;
    }

    var wtbFiles = SearchFilesByExtension(gameRecordDir, ".wtb");
    var gameRecordFiles = new WTHORFile[wtbFiles.Length];
    for (var i = 0; i < gameRecordFiles.Length; i++)
        gameRecordFiles[i] = new WTHORFile(jouFiles[0], trnFiles[0], wtbFiles[i]);

    var j = 0;
    var gameRecords = new (int depth, WTHORGameRecord record)[gameRecordFiles.Sum(n => n.GameRecords.Count)];
    foreach (var file in gameRecordFiles)
    {
        var depth = file.WtbHeader.Depth;
        foreach (var record in file.GameRecords)
            gameRecords[j++] = (depth, record);
    }

    var rand = new Xorshift();
    rand.Shuffle(gameRecords);
    var testDataNum = (int)(gameRecords.Length * testDataRate);
    var header = "current_player_board,opponent_player_board,next_move,result";

    using var testSw = new StreamWriter($"{outDir}/test_data.csv");
    testSw.WriteLine(header);
    for (var i = 0; i < testDataNum; i++)
        WriteGameRecordAsCSVFormat(testSw, gameRecords[i].record, gameRecords[i].depth, moveCountThreshold);

    using var trainSw = new StreamWriter($"{outDir}/train_data.csv");
    trainSw.WriteLine(header);
    for (var i = testDataNum; i < gameRecords.Length; i++)
        WriteGameRecordAsCSVFormat(trainSw, gameRecords[i].record, gameRecords[i].depth, moveCountThreshold);
}

void CreateWTHORFileFromTextFile(string path, string jouPath, string trnPath, string wtbPath)
{
    var gameRecords = new List<WTHORGameRecord>();
    using var sr = new StreamReader(path);
    while (sr.Peek() != -1)
    {
        var movesStr = sr.ReadLine().Replace("\n", string.Empty);
        var board = new Board(Color.Black, InitialBoardState.Cross);
        var moves = new List<Move>();
        var invalid = false;
        for (var i = 0; i < movesStr.Length; i += 2)
        {
            var move = (board.GetNextMoves()[0].Pos != BoardPosition.Pass) ? new Move(board.SideToMove, movesStr[i..(i + 2)])
                                                                           : new Move(board.SideToMove, BoardPosition.Pass);
            if (!board.Update(move))
            {
                invalid = true;
                break;
            }
            moves.Add(move);
        }

        if (!invalid)
            gameRecords.Add(new WTHORGameRecord("unknown", "unknown", "unknown", board.GetDiscCount(Color.Black), 0, moves));
    }

    (var jouHeader, var trnHeader, var wtbHeader) = (new WTHORHeader(), new WTHORHeader(), new WTHORHeader());
    jouHeader.FileCreationTime = trnHeader.FileCreationTime = wtbHeader.FileCreationTime = DateTime.Now;
    jouHeader.NumberOfRecords = trnHeader.NumberOfRecords = 1;
    wtbHeader.NumberOfGames = gameRecords.Count;
    wtbHeader.BoardSize = Board.BOARD_SIZE;
    wtbHeader.Depth = Board.SQUARE_NUM;

    new WTHORFile(jouHeader, trnHeader, wtbHeader, new string[] { "unknown" }, new string[] { "unknown" }, gameRecords.ToArray()).SaveToFiles(jouPath, trnPath, wtbPath);
}

void MergeTextFiles(string[] pathes, string outPath)
{
    using var sw = new StreamWriter(outPath);
    foreach (var path in pathes)
    {
        using var sr = new StreamReader(path);
        while (sr.Peek() != -1)
            sw.WriteLine(sr.ReadLine());
    }
}

void WriteGameRecordAsCSVFormat(StreamWriter sw, WTHORGameRecord gameRecord, int depth, int moveCountThreshold)
{
    var moves = gameRecord.MoveRecord;
    var bestResult = GetGameResult(gameRecord.BestBlackDiscCount);
    var result = GetGameResult(gameRecord.BlackDiscCount);
    var board = new Board(Color.Black, InitialBoardState.Cross);
    var k = 0;
    while (true)
    {
        var blackBoard = board.GetBitboard(Color.Black);
        var whiteBoard = board.GetBitboard(Color.White);
        var res = (board.GetEmptyCount() >= depth) ? bestResult : result;
        var nextMove = (k != moves.Count) ? moves[k] : new Move(Color.Black, BoardPosition.Null);

        if (k >= moveCountThreshold)
        {
            if (board.SideToMove == Color.Black)
                sw.WriteLine($"{blackBoard},{whiteBoard},{nextMove},{(sbyte)res}");
            else
                sw.WriteLine($"{whiteBoard},{blackBoard},{nextMove},{(sbyte)InvertGameResult(res)}");
        }

        if (nextMove.Pos != BoardPosition.Null)
            board.Update(moves[k++]);
        else
            break;
    }
}

string[] SearchFilesByExtension(string dir, string extension)
{
    var files = new List<string>();
    foreach (var file in Directory.GetFiles(dir))
        if (Path.GetExtension(file).ToLower() == extension)
            files.Add(file);
    return files.ToArray();
}


GameResult GetGameResult(int blackDiscCount)
{
    const int DRAW_DISC_COUNT = Board.SQUARE_NUM / 2;
    if (blackDiscCount > DRAW_DISC_COUNT)
        return GameResult.Win;
    else if (blackDiscCount < DRAW_DISC_COUNT)
        return GameResult.Loss;
    return GameResult.Draw;
}

GameResult InvertGameResult(GameResult result)
{
    switch (result)
    {
        case GameResult.Win:
            return GameResult.Loss;

        case GameResult.Loss:
            return GameResult.Win;

        default:
            return result;
    }
}


/*
 * Create Training Data Main
 */
const string GAME_RECORD_DIR = @"C:\Users\admin\source\repos\Kalmia\TrainData\GameRecords\FFO";
const string OUT_DIR = @"C:\Users\admin\source\repos\Kalmia\TrainData\FFO";
const float TEST_DATA_RATE = 0.05f;

CreateTrainDataFile(GAME_RECORD_DIR, OUT_DIR, TEST_DATA_RATE);