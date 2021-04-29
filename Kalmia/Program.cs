using System;
using System.Runtime;
using System.Runtime.Intrinsics;

namespace Kalmia
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(Vector128.Create(0x00000000F0F0F0F0UL));
        }

        static void PrintBoardLine(byte boardLine)
        {
            var mask = 1;
            for(var i = 0; i < 8; i++)
            {
                if ((boardLine & mask) == 0)
                    Console.Write("0");
                else
                    Console.Write("1");
                mask <<= 1;
            }
            Console.WriteLine();
        }
    }
}
