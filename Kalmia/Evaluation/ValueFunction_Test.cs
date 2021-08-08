using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public static class ValueFunction_Test
    {
        public static void Mainloop(ValueFunction valueFunc)
        {
            var board = new Board(Color.Black, InitialBoardState.Cross);
            var featureBoard = new FeatureBoard(board);

            while(board.GetGameResult(Color.Black) == GameResult.NotOver)
            {
                Console.WriteLine(board);
                Console.WriteLine($"value = {valueFunc.F(featureBoard)}");

                while (true)
                {
                    Console.Write("next move = ");
                    var move = new Move(board.Turn, Console.ReadLine());
                    if (!board.GetNextMoves().Contains(move))
                    {
                        Console.WriteLine("illegal move");
                        continue;
                    }
                    board.Update(move);
                    featureBoard.SetBoard(board);
                    break;
                }
            }
            Console.WriteLine("done.");
        }
    }
}
