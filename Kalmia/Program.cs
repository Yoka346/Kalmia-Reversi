using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

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
        const string LOG_DIR_PATH = "log//";
        const string GTP_LOG_FILE_NAME = "gtp{0}.txt";
        const string THOUGHT_LOG_FILE_NAME = "thought_log{0}.txt";

        static void Main()
        {
            // StartEngine();
            // StartLearning();
            StartEndGameBenchmark();
        }

        static void StartEngine()
        {
            var config = new KalmiaConfig();
            config.SearchCount = 300000000;
            config.LatencyCentiSec = 2;
            config.SelectMoveStochastically = false;
            config.OpenningMoveNum = 15;
            config.UseMaxTimeForMove = false;
            config.ValueFuncParamFile = @"C:\Users\admin\source\repos\Kalmia\Params\kalmia_value_func.dat";
            GTP.Mainloop(new KalmiaEngine(config), GTPCoordinateRule.Chess);
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

        static void StartEndGameBenchmark()
        {
            var solver = new MateSolver(256 * 1024 * 1024);
            EndGameBenchmark.TestMateSolver(solver, @"C:\Users\admin\source\repos\Kalmia\FFOEndgame\end43.pos");
        }

        //static void Main(string[] args)
        //{
        //    SetCurrentDirectry();
        //    if (args.Length == 0)
        //        args = new string[] { "--level", "5" };

        //    if (args.Length != 2 || args[0] != "--level")
        //    {
        //        Console.WriteLine("invalid option.");
        //        return;
        //    }

        //    var config = SelectLevel(args[1]);
        //    if (config == null)
        //        return;
        //    (var gtpLogPath, var thoughtLogPath) = CreateFiles();
        //    var engine = new KalmiaEngine_Old(config.Value, thoughtLogPath);
        //    GTP.Mainloop(engine, GTPCoordinateRule.Chess, gtpLogPath);
        //}

        //static void SetCurrentDirectry()
        //{
        //    var assembly = Assembly.GetEntryAssembly();
        //    Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly.Location));
        //}

        //static (string gtpLogPath, string thoughtLogPath) CreateFiles()
        //{
        //    if (!Directory.Exists(LOG_DIR_PATH))
        //        Directory.CreateDirectory(LOG_DIR_PATH);

        //    var gtpLogPath = LOG_DIR_PATH + GTP_LOG_FILE_NAME;
        //    var i = 0;
        //    while (File.Exists(string.Format(gtpLogPath, i)))
        //        i++;
        //    gtpLogPath = string.Format(gtpLogPath, i);

        //    var thoughtLogPath = LOG_DIR_PATH + THOUGHT_LOG_FILE_NAME;
        //    i = 0;
        //    while (File.Exists(string.Format(thoughtLogPath, i)))
        //        i++;
        //    thoughtLogPath = string.Format(thoughtLogPath, i);
        //    return (gtpLogPath, thoughtLogPath);
        //}

        //static KalmiaConfig? SelectLevel(string level)
        //{
        //    const string LEVEL_CONFIG_DIR = "level_config/";

        //    var path = $"{LEVEL_CONFIG_DIR}level{level}.json";
        //    if (File.Exists(path))
        //        return new KalmiaConfig(File.ReadAllText(path));
        //    Console.WriteLine("invalid level.");
        //    return null;
        //}
    }
}
