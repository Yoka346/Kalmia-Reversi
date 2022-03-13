//#define DEVELOP

using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Kalmia.Engines;
using Kalmia.EndGameSolver;
using Kalmia.GoTextProtocol;
using Kalmia.Evaluation;
using Kalmia.Learning;

namespace Kalmia
{
    class Program
    {
        const string LOG_DIR_PATH = "log/";
        const string GTP_LOG_DIR_PATH = $"{LOG_DIR_PATH}/gtp/";
        const string KALMIA_LOG_DIR_PATH = $"{LOG_DIR_PATH}/kalmia/";
        const string GTP_LOG_FILE_NAME = "gtp{0}.log";
        const string KALMIA_LOG_FILE_NAME = "kalmia_thought{0}.log";

        static void Main(string[] args)
        {
#if DEVELOP
            DevTest();
#else
            CheckFiles();
            var options = ExtractOptions(args);
            RunAsSpecifiedMode(options);
#endif
        }

#if DEVELOP
        static void DevTest()
        {
            var solver = new FinalDiscDifferenceSolver(256 * 1024 * 1024);
            EndGameBenchmark.Solve(solver, @"C:\Users\admin\source\repos\Kalmia\FFOEndgame\end40.pos");
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
                    Console.WriteLine("Error: Specify config file path.");
                    return;
                }

                var path = options["configfile"][0];
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Error: File \"{path}\" does not exist. If the specified path contains some spaces, close it by \" \".");
                    return;
                }

                config = JsonSerializer.Deserialize<KalmiaConfig>(File.ReadAllText(options["configfile"][0]));
            }
            else
                config = new KalmiaConfig();

            if (options.ContainsKey("mode"))
            {
                if (options["mode"].Length == 0)
                {
                    Console.WriteLine("Error: Specify program mode.");
                    return;
                }

                var modeStr = options["mode"][0].ToLower();
                switch (modeStr)
                {
                    case "gtp":
                        StartEngine(config);
                        return;

                    case "matesolverbenchmark":
                        if (options.ContainsKey("matesolverbenchmark"))
                        {
                            if (options["matesolverbenchmark"].Length == 0)
                            {
                                Console.WriteLine("Error: Specify end game problems directory path.");
                                return;
                            }

                            var mateSolver = new MateSolver(config.EndgameSolverMemorySize);
                            StartEndGameBenchmark(mateSolver, options["matesolverbenchmark"][0]);
                        }
                        else
                        {
                            Console.WriteLine("Error: Specify end game problems directory path by \"matesolverbenchmark\" option");
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
            EndGameBenchmark.Solve(solver, @"C:\Users\admin\source\repos\Kalmia\FFOEndgame\end41.pos");
        }
    }
}
