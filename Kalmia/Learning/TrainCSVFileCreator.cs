using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Kalmia.IO;
using Kalmia.Reversi;

namespace Kalmia.Learning
{
    public static class TrainDataCSVFile
    {
        /// <summary>
        /// Creates training data and test data from game record which is written by WTHOR format. 
        /// </summary>
        /// <param name="gameRecrodDir">Directory path of game record. This directory must be include one trn file and one jou file and wtb files.</param>
        /// <param name="outDir">Directory path of training data csv file.</param>
        /// <param name="testDataProportion">The proportion of test data to whole data.</param>
        /// <param name="minMoveNum">The data whose move num more than or equals this argument will be sampled. If this argument is 0, whole data of a game will be sampled.</param>
        public static void CreateTrainDataFromWTHORFiles(string gameRecrodDir, string outDir, double testDataProportion, int minMoveNum = 0)
        {
            var trnFile = SearchFilesWithExtension(gameRecrodDir, ".trn").FirstOrDefault();
            var jouFile = SearchFilesWithExtension(gameRecrodDir, ".jou").FirstOrDefault();

            if (trnFile == default)
                throw new FileNotFoundException($"There are no trn files in \"{gameRecrodDir}\".");

            if(jouFile == default)
                throw new FileNotFoundException($"There are no jou files in \"{gameRecrodDir}\".");

            var wtbFiles = SearchFilesWithExtension(gameRecrodDir, ".wtb");
            var gameRecordFiles = new WTHORFile[wtbFiles.Length];
            for (var i = 0; i < gameRecordFiles.Length; i++)
                gameRecordFiles[i] = new WTHORFile(jouFile, trnFile, wtbFiles[i]);

            var j = 0;
            var gameRecords = new (int depth, WTHORGameRecord record)[gameRecordFiles.Sum(n => n.GameRecords.Count)];
            foreach(var file in gameRecordFiles)
            {
                var depth = file.WtbHeader.Depth;
                foreach (var record in file.GameRecords)
                    gameRecords[j++] = (depth, record);
            }

            var rand = new Random();
            rand.Shuffle(gameRecords);

            var testDataNum = (int)(gameRecords.Length * testDataProportion);
            var header = "current_player_board,opponent_player_board,next_move,result";
            using var testDataSw = new StreamWriter($"{outDir}/test_data.csv");
            testDataSw.WriteLine(header);
            for(var i = 0; i < testDataNum; i++)
                WriteGameRecordAsCSVFormat(testDataSw, gameRecords[i].record, gameRecords[i].depth, minMoveNum);

            using var trainSw = new StreamWriter($"{outDir}/train_data.csv");
            trainSw.WriteLine(header);
            for (var i = testDataNum; i < gameRecords.Length; i++)
                WriteGameRecordAsCSVFormat(trainSw, gameRecords[i].record, gameRecords[i].depth, minMoveNum);
        }

        static string[] SearchFilesWithExtension(string dir, string extension)
        {
            var files = new List<string>();
            foreach (var file in Directory.GetFiles(dir))
                if (Path.GetExtension(file).ToLower() == extension)
                    files.Add(file);
            return files.ToArray();
        }
        
        static void WriteGameRecordAsCSVFormat(StreamWriter sw, WTHORGameRecord gameRecord, int depth, int moveCountThreshold)
        {
            var moves = gameRecord.MoveRecord;
            var bestResult = GetGameResult(gameRecord.BestBlackDiscCount);
            var result = GetGameResult(gameRecord.BlackDiscCount);
            var board = new Board(DiscColor.Black);
            var k = 0;
            while (true)
            {
                var blackBoard = board.GetBitboard(DiscColor.Black);
                var whiteBoard = board.GetBitboard(DiscColor.White);
                var res = (board.GetEmptyCount() >= depth) ? bestResult : result;
                var nextMove = (k != moves.Count) ? moves[k] : new Move(DiscColor.Black, BoardCoordinate.Null);

                if (k >= moveCountThreshold)
                {
                    if (board.SideToMove == DiscColor.Black)
                        sw.WriteLine($"{blackBoard},{whiteBoard},{nextMove},{(sbyte)res}");
                    else
                        sw.WriteLine($"{whiteBoard},{blackBoard},{nextMove},{(sbyte)InvertGameResult(res)}");
                }

                if (nextMove.Coord != BoardCoordinate.Null)
                    board.Update(moves[k++]);
                else
                    break;
            }
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
