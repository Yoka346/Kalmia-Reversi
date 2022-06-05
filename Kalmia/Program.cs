#define DEVELOP

using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using Kalmia.Engines;
using Kalmia.EndGameSolver;
using Kalmia.GoTextProtocol;
using Kalmia.Evaluation;
using Kalmia.Learning;
using Kalmia.Reversi;

namespace Kalmia
{
    class Program
    {
        // log
        const string LOG_DIR_PATH = "log/";
        const string GTP_LOG_DIR_PATH = $"{LOG_DIR_PATH}/gtp/";
        const string KALMIA_LOG_DIR_PATH = $"{LOG_DIR_PATH}/kalmia/";
        const string GTP_LOG_FILE_NAME = "gtp{0}.log";
        const string KALMIA_LOG_FILE_NAME = "kalmia_thought{0}.log";

        // difficulty
        const string DIFFICULTY_DIR_PATH = "difficulty/";

        static readonly TimeSpan REMOVE_LOG_FILE_SPAN = new(31, 0, 0, 0, 0);  // 1 month

        static void Main(string[] args)
        {
#if DEVELOP
            DevTest();
#else
            CheckFiles();
            RemoveOldLogFiles();
            var options = ExtractOptions(args);
            RunAsSpecifiedMode(options);
#endif
        }

#if DEVELOP
        static void DevTest()
        {
            using var sw = new StreamWriter("exp.txt");
            foreach (var n in Kalmia.Evaluation.ValueFunction.ToSymmetricFeatureIdx)
                sw.Write($"{n}\n");
            //var board = new FastBoard(DiscColor.Black, new Bitboard(4432742849556UL, 1184721665973494273UL));
            //var bf = new BoardFeature(board);
            //var vf = new ValueFunction(@"C:\Users\yu_ok\source\repos\Yoka346\Kalmia-Reversi\Params\kalmia_value_func.dat");
            //float sum = 0.0f;
            //Console.WriteLine(vf.F(bf));
            //const int DATA_NUM = 10000;
            //var sw = new StreamWriter("eval_func_test_data.csv");
            //var vf = new ValueFunction(@"C:\Users\yu_ok\source\repos\Yoka346\Kalmia-Reversi\Params\kalmia_value_func.dat");
            //for(var i = 0; i < DATA_NUM; i++)
            //{
            //    var board = CreateRandomBoard(Random.Shared, Random.Shared.Next(1, 61));
            //    sw.WriteLine($"{board.GetBitboard().CurrentPlayer},{board.GetBitboard().OpponentPlayer},{vf.F(new BoardFeature(board))}");
            //}
        }

        static FastBoard CreateRandomBoard(Random rand, int emptyCount)
        {
            var board = new FastBoard();
            Span<BoardCoordinate> positions = stackalloc BoardCoordinate[46];
            while (board.GetEmptyCount() != emptyCount && board.GetGameResult() == GameResult.NotOver)
            {
                var num = board.GetNextPositionCandidates(positions);
                board.Update(positions[rand.Next(num)]);
            }
            return board;
        }
#endif

        static void CheckFiles()
        {
            if (!Directory.Exists(LOG_DIR_PATH))
                Directory.CreateDirectory(LOG_DIR_PATH);

            if (!Directory.Exists(GTP_LOG_DIR_PATH))
                Directory.CreateDirectory(GTP_LOG_DIR_PATH);

            if (!Directory.Exists(KALMIA_LOG_DIR_PATH))
                Directory.CreateDirectory(KALMIA_LOG_DIR_PATH);
        }

        static void RemoveOldLogFiles()
        {
            foreach (var dir in new string[] { GTP_LOG_DIR_PATH, KALMIA_LOG_DIR_PATH })
                foreach (var file in Directory.GetFiles(dir))
                    if (Path.GetExtension(file) == ".log" && DateTime.Now - File.GetCreationTime(file) >= REMOVE_LOG_FILE_SPAN)
                        File.Delete(file);
        }

        static Dictionary<string, string[]> ExtractOptions(string[] args)
        {
            var options = new Dictionary<string, string[]>();
            for (var i = 0; i < args.Length; i++)
                if (Regex.IsMatch(args[i], "^--"))
                {
                    var key = args[i].Remove(0, 2).ToLower();
                    var value = new List<string>();
                    int j;
                    for (j = i + 1; j < args.Length; j++)
                        if (!Regex.IsMatch(args[j], "^--"))
                            value.Add(args[j]);
                        else
                            break;
                    options.Add(key, value.ToArray());
                    i = j - 1;
                }
            return options;
        }

        static void RunAsSpecifiedMode(Dictionary<string, string[]> options)
        {
            KalmiaConfig config;
            if (options.ContainsKey("configfile"))
            {
                if (options["configfile"].Length == 0)
                {
                    Console.Error.WriteLine("Error: Specify config file path.");
                    return;
                }

                var path = options["configfile"][0];
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"Error: File \"{path}\" does not exist. If the specified path contains some spaces, close the path with \" \".");
                    return;
                }

                config = JsonSerializer.Deserialize<KalmiaConfig>(File.ReadAllText(options["configfile"][0]));
            }
            else if (options.ContainsKey("difficulty"))
            {
                if(options["difficulty"].Length == 0)
                {
                    Console.Error.WriteLine("Error: Specify the difficulty(easy, normal, proffesional, superhuman, or unlimited). ");
                    return;
                }

                var difficulty = options["difficulty"][0].ToLower();
                var path = $"{DIFFICULTY_DIR_PATH}{difficulty}.json";
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"Error: \"{difficulty}\" is an invalid difficulty.");
                    return;
                }
                config = JsonSerializer.Deserialize<KalmiaConfig>(File.ReadAllText(path));
            }
            else
                config = new KalmiaConfig();


            if (options.ContainsKey("mode"))
            {
                if (options["mode"].Length == 0)
                {
                    Console.Error.WriteLine("Error: Specify program mode.");
                    return;
                }

                var modeStr = options["mode"][0].ToLower();
                switch (modeStr)
                {
                    case "gtp":
                        StartEngine(config);
                        return;

                    case "ruler":
                        StartRuler();
                        return;

                    case "matesolverbenchmark":
                        if (options.ContainsKey("matesolverbenchmark"))
                        {
                            if (options["matesolverbenchmark"].Length == 0)
                            {
                                Console.Error.WriteLine("Error: Specify end game problems directory path.");
                                return;
                            }

                            var mateSolver = new MateSolver(config.EndgameSolverMemorySize);
                            StartEndGameBenchmark(mateSolver, options["matesolverbenchmark"][0]);
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: Specify end game problems directory path by \"matesolverbenchmark\" option");
                            return;
                        }
                        return;
                }
            }
        }

        static string CreateFilePath(string dir, string nameTemp)
        {
            string path;
            var i = 0;
            do
                path = $"{dir}{string.Format(nameTemp, i++)}";
            while (File.Exists(path));
            return path;
        }

        static void StartEngine(KalmiaConfig config)
        {
            GTP.Mainloop(new KalmiaEngine(config, 
                         CreateFilePath(KALMIA_LOG_DIR_PATH, KALMIA_LOG_FILE_NAME)), 
                         CreateFilePath(GTP_LOG_DIR_PATH, GTP_LOG_FILE_NAME));
        }

        static void StartRuler()
        {
            GTP.Mainloop(new ReversiRuler());
        }

        static void StartLearning()
        {
            var valueFunc = new ValueFunction("Kalmia", 1, 4);
            var optimizer = new ValueFuncOptimizer(valueFunc);

            Console.WriteLine("Loading data set.");
            optimizer.LoadTrainData(@"C:\Users\admin\source\repos\Kalmia\TrainData\FFO\train_data.csv", false);
            optimizer.LoadTestData(@"C:\Users\admin\source\repos\Kalmia\TrainData\FFO\test_data.csv");
            Console.WriteLine("done.");

            optimizer.StartOptimization(1000, @"C:\Users\admin\source\repos\Kalmia\ValueFuncOptimization\");
        }

        static void StartEndGameBenchmark(IEndGameSolver solver, string problemsPath)
        {
            throw new NotImplementedException();
        }
    }
}
