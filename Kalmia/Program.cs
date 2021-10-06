using System;
using System.IO;
using System.Reflection;

using Kalmia.Engines;
using Kalmia.GoTextProtocol;

namespace Kalmia
{
    class Program
    {
        const string LOG_DIR_PATH = "log//";
        const string GTP_LOG_FILE_NAME = "gtp{0}.txt";
        const string THOUGHT_LOG_FILE_NAME = "thought_log{0}.txt";

        static void Main(string[] args)
        {
            DllInfo.PrintDllVersion();
            //var policy = new PolicyFuncEngine(new Evaluation.PolicyFunction(@"D:\PolicyFunctionOptimize\optimized_param.dat"));
            //GTP.Mainloop(policy);

            SetCurrentDirectry();
            var config = new KalmiaConfig();
            config.EnablePondering = false;
            config.ReuseSubTree = true;
            config.SimulationCount = 3200;
            config.Temperature = 1.0f;
            config.ThreadNum = 8;
            config.UCBFactor = 0.25f;
            config.ValueFuncParamsFilePath = @"C:\Users\admin\source\repos\Kalmia\Params\kalmia_value_func.dat";
            var config1 = config;
            config1.ValueFuncParamsFilePath = @"C:\Users\admin\source\repos\Kalmia\Params\optimized_param.dat";
            var kalmia = new KalmiaEngine(config, "log0.txt");
            var kalmiaNew = new KalmiaEngine(config1, "log1.txt");
            var game = new EngineGame(kalmiaNew, kalmia);
            game.Start(400, true, "gamelog.txt");

            //if (args.Length == 0)
            //    args = new string[] { "--level", "5" };

            //if (args.Length != 2 || args[0] != "--level")
            //{
            //    Console.WriteLine("invalid option.");
            //    return;
            //}

            //var config = SelectLevel(args[1]);
            //if (config == null)
            //    return;
            //(var gtpLogPath, var thoughtLogPath) = CreateFiles();
            //var engine = new KalmiaEngine(config.Value, thoughtLogPath);
            //GTP.Mainloop(engine, GTPCoordinateRule.Chess, gtpLogPath);

            // Kalmia
            //SetCurrentDirectry();
            //if (args.Length == 0)
            //    args = new string[] { "--level", "5" };

            //if (args.Length != 2 || args[0] != "--level")
            //{
            //    Console.WriteLine("invalid option.");
            //    return;
            //}

            //var config = SelectLevel(args[1]);
            //if (config == null)
            //    return;
            //(var gtpLogPath, var thoughtLogPath) = CreateFiles();
            //var engine = new KalmiaEngine(config.Value, thoughtLogPath);
            //GTP.Mainloop(engine, GTPCoordinateRule.Chess, gtpLogPath);
        }

        static void SetCurrentDirectry()
        {
            var assembly = Assembly.GetEntryAssembly();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly.Location));
        }

        static (string gtpLogPath, string thoughtLogPath) CreateFiles()
        {
            if (!Directory.Exists(LOG_DIR_PATH))
                Directory.CreateDirectory(LOG_DIR_PATH);

            var gtpLogPath = LOG_DIR_PATH + GTP_LOG_FILE_NAME;
            var i = 0;
            while (File.Exists(string.Format(gtpLogPath, i)))
                i++;
            gtpLogPath = string.Format(gtpLogPath, i);

            var thoughtLogPath = LOG_DIR_PATH + THOUGHT_LOG_FILE_NAME;
            i = 0;
            while (File.Exists(string.Format(thoughtLogPath, i)))
                i++;
            thoughtLogPath = string.Format(thoughtLogPath, i);
            return (gtpLogPath, thoughtLogPath);
        }

        static KalmiaConfig? SelectLevel(string level)
        {
            const string LEVEL_CONFIG_DIR = "level_config/";

            var path = $"{LEVEL_CONFIG_DIR}level{level}.json";
            if (File.Exists(path))
                return new KalmiaConfig(File.ReadAllText(path));
            Console.WriteLine("invalid level.");
            return null;
        }
    }
}
