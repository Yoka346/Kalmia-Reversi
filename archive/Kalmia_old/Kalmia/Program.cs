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

#if DEVELOP
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

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
            using var sw = new StreamWriter("position_feature_update_test_data.csv");
            sw.Write("player,opponent,move0,move1");
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM-1; i++)
                sw.Write($",f0{i}");
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM - 1; i++)
                sw.Write($",f1{i}");
            sw.WriteLine();

            Span<Reversi.BoardPosition> moves = stackalloc Reversi.BoardPosition[48];
            var rand = new Random();
            for(int i = 0; i < 1000; i++)
            {
                var p = (ulong)rand.NextInt64();
                var o = (ulong)rand.NextInt64();
                p ^= p & o;
                var board = new Reversi.FastBoard(Reversi.DiscColor.Black, new Reversi.Bitboard(p, o));
                var bf = new BoardFeature(board);
                var num = board.GetNextPositionCandidates(moves);
                var move = moves[rand.Next(num)];
                var flipped = board.Update(move);
                bf.Update(move, flipped);

                board.GetNextPositionCandidates(moves);
                var move1 = moves[rand.Next(num)];
                flipped = board.Update(move1);
                var bf1 = new BoardFeature(bf);
                bf1.Update(move1, flipped);

                sw.Write($"{p},{o},{(int)move},{(int)move1}");
                for (var j = 0; j < bf.Features.Length - 1; j++)
                    sw.Write($",{bf.Features[j]}");
                for (var j = 0; j < bf1.Features.Length - 1; j++)
                    sw.Write($",{bf1.Features[j]}");

                sw.WriteLine();
            }

            static ulong CalculateFilippedDiscs_AVX2(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
            {
                var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
                var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
                var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

                var x = 1UL << pos;
                var x4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(x));
                var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
                var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
                var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
                var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

                var flipLeft = Avx2.And(Avx2.ShiftLeftLogicalVariable(x4, shift), maskedO4);
                var flipRight = Avx2.And(Avx2.ShiftRightLogicalVariable(x4, shift), maskedO4);
                flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
                flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
                flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
                flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
                flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
                flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

                var outflankLeft = Avx2.And(p4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift));
                var outflankRight = Avx2.And(p4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
                flipLeft = Avx2.AndNot(Avx2.CompareEqual(outflankLeft, Vector256<ulong>.Zero), flipLeft);
                flipRight = Avx2.AndNot(Avx2.CompareEqual(outflankRight, Vector256<ulong>.Zero), flipRight);
                var flip4 = Avx2.Or(flipLeft, flipRight);
                var flip2 = Sse2.Or(Avx2.ExtractVector128(flip4, 0), Avx2.ExtractVector128(flip4, 1));
                flip2 = Sse2.Or(flip2, Sse2.UnpackHigh(flip2, flip2));
                return Sse2.X64.ConvertToUInt64(flip2);
            }

            ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
            {
                var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
                var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
                var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

                var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
                var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
                var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
                var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

                var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, shift));
                var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, shift));
                flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
                flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
                flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
                flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
                flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
                flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

                var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, shift);
                mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
                var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
                mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
                return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
            }
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
