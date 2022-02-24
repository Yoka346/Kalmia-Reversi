using System;
using System.IO;

using Kalmia.Reversi;

namespace Kalmia.EndGameSolver
{
    public static class EndGameBenchmark
    {
        public static int Solve(IEndGameSolver solver, string ffoPosFilePath)
        {
            if (solver is MateSolver)
                return TestMateSolver((MateSolver)solver, ffoPosFilePath);
            throw new NotImplementedException();
        }

        static int TestMateSolver(MateSolver solver, string ffoPosFilePath)
        {
            var testCase = LoadFFOTestCase(ffoPosFilePath);
            Console.WriteLine(testCase.Label);
            Console.WriteLine($"Side to move is {testCase.InitialBoard.SideToMove}");
            solver.SolveBestMove(testCase.InitialBoard, int.MaxValue, out GameResult result, out _);
            Console.WriteLine($"Done.\nGame result is {result}");
            Console.WriteLine($"ellapsed = {solver.SearchEllapsedMilliSec}[ms]");
            Console.WriteLine($"{solver.Nps}[nps]");
            Console.WriteLine($"internal_node_count = {solver.InternalNodeCount}");
            Console.WriteLine($"leaf_node_count = {solver.LeafNodeCount}");
            return solver.SearchEllapsedMilliSec;
        }

        static FFOTestCase LoadFFOTestCase(string path)
        {
            using var sr = new StreamReader(path);
            var board = InterpretFFOBoardString(sr.ReadLine());
            var colorText = sr.ReadLine().ToLower();
            if (colorText == "white")
                board.SwitchSideToMove();
            return new FFOTestCase(sr.ReadLine(), board);
        }

        static FastBoard InterpretFFOBoardString(string ffoStr)
        {
            const char BLACK = 'X';
            const char WHITE = 'O';
            const char EMPTY = '-';

            var board = new FastBoard();
            for (var i = 0; i < ffoStr.Length; i++)
            {
                var color = ffoStr[i] switch
                {
                    BLACK => DiscColor.Black,
                    WHITE => DiscColor.White,
                    EMPTY => DiscColor.Null,
                    _ => throw new FormatException("Invalid FFO string.")
                };

                if (color != DiscColor.Null)
                    board.PutStoneWithoutFlip(color, (BoardPosition)i);
            }
            return board;
        }

        struct FFOTestCase
        {
            public string Label { get; }
            public FastBoard InitialBoard { get; }

            public FFOTestCase(string label, FastBoard initBoard)
            {
                this.Label = label;
                this.InitialBoard = initBoard;
            }
        }
    }
}
