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
        const string LOG_DIR_PATH = "log//";
        const string GTP_LOG_FILE_NAME = "gtp{0}.txt";
        const string THOUGHT_LOG_FILE_NAME = "thought_log{0}.txt";

        static void Main(string[] args)
        {
            var options = ExtractOptions(args);
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

            GTPCoordinateRule coordRule;
            if (options.ContainsKey("coordrule"))
            {
                if (options["coordrule"].Length == 0)
                {
                    Console.WriteLine("Error: Specify coordinate rule. The coordinate rule is \"go\" or \"othello\"");
                    return;
                }

                var coordRuleStr = options["coordrule"][0];
                if (coordRuleStr == "go")
                    coordRule = GTPCoordinateRule.Go;
                else if (coordRuleStr == "othello")
                    coordRule = GTPCoordinateRule.Othello;
                else
                {
                    Console.WriteLine("Error: Specify valid coordinate rule. The available coordinate rules are \"go\" or \"othello\"");
                    return;
                }
            }
            else
                coordRule = GTPCoordinateRule.Go;

            if (options.ContainsKey("mode"))
            {
                if(options["mode"].Length == 0)
                {
                    Console.WriteLine("Error: Specify program mode.");
                    return;
                }

                var modeStr = options["mode"][0];
                switch (modeStr)
                {
                    case "gtp":
                        var kalmiaLogPath = string.Empty;
                        if (options.ContainsKey("kalmialogpath"))
                            if (options["kalmialogpath"].Length == 0)
                            {
                                Console.WriteLine("Error: Specify Kalmia's log file path.");
                                return;
                            }
                        StartEngine(config, coordRule, kalmiaLogPath);
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
                            value.Add(args[j].ToLower());
                        else
                            break;
                    options.Add(key, value.ToArray());
                    i = j - 1;
                }
            return options;
        }

        static void StartEngine(KalmiaConfig config, GTPCoordinateRule coordRule, string logFilePath)
        {
            if(logFilePath == string.Empty)
                GTP.Mainloop(new KalmiaEngine(config), coordRule);
            else
            {
                if (!File.Exists(logFilePath))
                {
                    Console.WriteLine($"Error: File \"{logFilePath}\" does not exist. If the specified path contains some spaces, close it by \" \".");
                    return;
                }
                GTP.Mainloop(new KalmiaEngine(config, logFilePath), coordRule);
            }
        }

        static void StartLearning()
        {
            var valueFunc = new ValueFunction("Kalmia", 1, 4);
            var optimizer = new ValueFuncOptimizer(valueFunc);
            //var valueFunc = new ValueFunction("Kalmia", 1, 4);
            //var optimizer = new ValueFuncOptimizer(valueFunc);

            Console.WriteLine("Loading data set.");
            optimizer.LoadTrainData(@"C:\Users\admin\source\repos\Kalmia\TrainData\FFO\train_data.csv", false);
            optimizer.LoadTestData(@"C:\Users\admin\source\repos\Kalmia\TrainData\FFO\test_data.csv");
            Console.WriteLine("done.");

            optimizer.StartOptimization(1000, @"C:\Users\admin\source\repos\Kalmia\ValueFuncOptimization\LatentFactor");
            //optimizer.StartOptimization(1000, @"C:\Users\admin\source\repos\Kalmia\ValueFuncOptimization\Linear");
        }

        static void StartEndGameBenchmark(IEndGameSolver solver, string problemsPath)
        {
            
            EndGameBenchmark.Solve(solver, @"C:\Users\admin\source\repos\Kalmia\FFOEndgame\end41.pos");
        }
    }
}
