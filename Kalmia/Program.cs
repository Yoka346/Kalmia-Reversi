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
            GTP.Mainloop(new RandomMoveEngine(), GTPCoordinateRule.Othello);
        }
    }
}
