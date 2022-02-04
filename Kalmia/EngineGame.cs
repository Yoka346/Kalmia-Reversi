using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.GoTextProtocol;

namespace Kalmia
{
    public class EngineGame
    {
        GTPEngine engine0;
        GTPEngine engine1;

        public EngineGame(GTPEngine engine0, GTPEngine engine1)
        {
            this.engine0 = engine0;
            this.engine1 = engine1;
        }

        public void Start(int count, bool switchSideToMove, string logFilePath)
        {
            var logger = new Logger(logFilePath, System.Console.OpenStandardOutput());
            (var engine0WinCount, var engine1WinCount, var drawCount) = (0, 0, 0);
            for(var gameNum = 0; gameNum < count; gameNum++)
            {
                logger.WriteLine($"game {gameNum + 1}");
                engine0.ClearBoard();
                engine1.ClearBoard();
                (var currentEngine, var opponentEngine) = (switchSideToMove && gameNum % 2 == 1) ? (engine1, engine0) : (engine0, engine1);
                var board = new Board(DiscColor.Black, InitialBoardState.Cross);
                GameResult result;
                while((result = board.GetGameResult(DiscColor.Black)) == GameResult.NotOver)
                {
                    var move = currentEngine.GenerateMove(board.SideToMove);
                    if (!board.IsLegalMove(move))
                    {
                        logger.WriteLine($"Error!! {currentEngine} played {move}, but it is illegal.");
                        logger.WriteLine($"Suspend game.");
                        return;
                    }
                    board.Update(move);
                    opponentEngine.Play(move);
                    var tmp = currentEngine;
                    currentEngine = opponentEngine;
                    opponentEngine = tmp;
                }

                GTPEngine winner;
                switch (result)
                {
                    case GameResult.Win:
                        if (switchSideToMove && gameNum % 2 == 1)
                        {
                            winner = engine1;
                            engine1WinCount++;
                        }
                        else
                        {
                            winner = engine0;
                            engine0WinCount++;
                        }
                        break;

                    case GameResult.Loss:
                        if (switchSideToMove && gameNum % 2 == 1)
                        {
                            winner = engine0;
                            engine0WinCount++;
                        }
                        else
                        {
                            winner = engine1;
                            engine1WinCount++;
                        }
                        break;

                    default:
                        winner = null;
                        drawCount++;
                        break;
                }

                if (winner != null)
                    logger.WriteLine($"{winner} wins.");
                else
                    logger.WriteLine($"draw.");

                logger.WriteLine($"{engine0} wins {engine0WinCount} times.");
                logger.WriteLine($"{engine1} wins {engine1WinCount} times.");
                logger.WriteLine($"game was drawn {drawCount} times.\n");
            }
            logger.WriteLine("[WIN_RATE]"); 
            logger.WriteLine($"{engine0}: {(engine0WinCount + drawCount * 0.5) * 100.0 / count} %");
            logger.WriteLine($"{engine1}: {(engine1WinCount + drawCount * 0.5) * 100.0 / count} %");
        }
    }
}
