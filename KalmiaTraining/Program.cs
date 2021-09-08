using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace KalmiaTraining
{
    class Program
    {
        static void Main(string[] args)
        {

        }

        static void CreateTrainDataFile(string gameRecordDir, double testDataRate)
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

            using var testSw = new StreamWriter("test_data.csv");
            testSw.WriteLine(header);
            for (var i = 0; i < testDataNum; i++)
                WriteGameRecordAsCSVFormat(testSw, gameRecords[i].record, gameRecords[i].depth);

            using var trainSw = new StreamWriter("train_data.csv");
            trainSw.WriteLine(header);
            for (var i = testDataNum; i < gameRecords.Length; i++)
                WriteGameRecordAsCSVFormat(trainSw, gameRecords[i].record, gameRecords[i].depth);
        }

        static void WriteGameRecordAsCSVFormat(StreamWriter sw, WTHORGameRecord gameRecord, int depth)
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
                if (board.SideToMove == Color.Black)
                    sw.WriteLine($"{blackBoard},{whiteBoard},{nextMove},{(sbyte)res}");
                else
                    sw.WriteLine($"{whiteBoard},{blackBoard},{nextMove},{(sbyte)InvertGameResult(res)}");

                if (nextMove.Pos != BoardPosition.Null)
                    board.Update(moves[k++]);
                else
                    break;
            }
        }

        static void TrainValueFunction(string workDir, string trainDataPath, string testDataPath, int moveCountPerStage)
        {
            var model = new ValueFunction("kalmia", 0, moveCountPerStage);
            var weightDecay = (from i in Enumerable.Range(0, model.StageNum) select 0.1f * MathF.Pow(0.6f, i)).ToArray();
            var optimizer = new ValueFunctionOptimizer(model, trainDataPath, testDataPath);
            optimizer.Optimize(workDir, true, 10000, 0.1f, 0.5f, weightDecay, 10, 5);
        }

        static void TrainPolicyFunction(string workDir, string trainDataPath, string testDataPath, int moveCountPerStage)
        {
            var model = new PolicyFunction("kalmia", 0, moveCountPerStage);
            var optimizer = new PolicyFunctionOptimizer(model, testDataPath, trainDataPath);
            optimizer.Optimize(workDir, true, 2000, 3.0f, 0.5f, 0.01f, 10, 5);
        }

        static string[] SearchFilesByExtension(string dir, string extension)
        {
            var files = new List<string>();
            foreach (var file in Directory.GetFiles(dir))
                if (Path.GetExtension(file).ToLower() == extension)
                    files.Add(file);
            return files.ToArray();
        }

        static GameResult GetGameResult(int blackDiscCount)
        {
            const int DRAW_DISC_COUNT = Board.SQUARE_NUM / 2;
            if (blackDiscCount > DRAW_DISC_COUNT)
                return GameResult.Win;
            else if (blackDiscCount < DRAW_DISC_COUNT)
                return GameResult.Loss;
            return GameResult.Draw;
        }

        static GameResult InvertGameResult(GameResult result)
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
    }
}
