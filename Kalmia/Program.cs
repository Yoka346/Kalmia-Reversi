using System;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.Intrinsics;
using System.Runtime.Serialization.Formatters.Binary;

using Kalmia.GoTextProtocol;
using Kalmia.Reversi;
using Kalmia.Engines;
using Kalmia.Evaluate;
using Kalmia.IO;

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
