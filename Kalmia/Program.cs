using System;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.Intrinsics;
using System.Runtime.Serialization.Formatters.Binary;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;
using Kalmia.Engines;
using Kalmia.Evaluation;
using Kalmia.IO;
using Kalmia.Learning;

namespace Kalmia
{
    class Program
    {
        static void Main(string[] args)
        {
            //var coordRule = (args.Length > 0 && args[0].ToLower() == "othello") ? GTPCoordinateRule.Othello : GTPCoordinateRule.Chess;
            //GTP.Mainloop(new RandomMoveEngine(), coordRule, $"gtplog{Environment.TickCount}.txt");
            //GTP.Mainloop(new MonteCarloEngine(10000), coordRule, $"gtplog{Environment.TickCount}.txt");
            //GTP.Mainloop(new MCTSEngine(3200000, 8, $"mcts_log{Environment.TickCount}.txt"), coordRule, $"gtplog{Environment.TickCount}.txt");

            Console.WriteLine("loading");
            var valueFunc = new ValueFunction(@"C:\Users\admin\source\repos\Kalmia\Params\edax_eval.dat");
            //ValueFunction_Test.Mainloop(valueFunc);
            Console.WriteLine("saving");
            valueFunc.SaveToFile("edax", 2, "Edax.dat");
        }
    }
}
