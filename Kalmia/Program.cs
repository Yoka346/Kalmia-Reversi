using System;
using System.IO;
using System.Runtime;
using System.Runtime.Intrinsics;
using Kalmia.GoTextProtocol;
using Kalmia.Engines;

namespace Kalmia
{
    class Program
    {
        static void Main(string[] args)
        {
            var coordRule = (args.Length > 0 && args[0].ToLower() == "othello") ? GTPCoordinateRule.Othello : GTPCoordinateRule.Chess;
            //GTP.Mainloop(new RandomMoveEngine(), coordRule, $"gtplog{Environment.TickCount}.txt");
            //GTP.Mainloop(new MonteCarloEngine(10000), coordRule, $"gtplog{Environment.TickCount}.txt");
            GTP.Mainloop(new MCTSEngine(320000, 8, $"mcts_log{Environment.TickCount}.txt"), coordRule, $"gtplog{Environment.TickCount}.txt");
        }
    }
}
