using System;
using System.IO;
using System.Text.Json;
using System.Runtime.Serialization;
using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public class GameInfo
    {
        public string BlackPlayerName { get; set; } = string.Empty;
        public string BlackPlayerPath { get; set; } = string.Empty;
        public string BlackPlayerArgs { get; set; } = string.Empty;
        public string BlackPlayerWorkDir { get; set; } = string.Empty;
        public string WhitePlayerName { get; set; } = string.Empty;
        public string WhitePlayerPath { get; set; } = string.Empty;
        public string WhitePlayerArgs { get; set; } = string.Empty;
        public string WhitePlayerWorkDir { get; set; } = string.Empty;
        public int GameNum { get; set; } = 0;
        public bool SwitchTurn { get; set; } = false;
    }

    class Program
    {
        static int GameNum = 0;
        static int BlackPlayerWinCount = 0;
        static int WhitePlayerWinCount = 0;
        static int DrawCount = 0;

        static void Main(string[] args)
        {
            if (args.Length < 1)
                Console.WriteLine("Specify the path of game information file.");
            LoadGameInfoFromFile(args[0], out var blackPlayer, out var whitePlayer, out var gameNum, out var switchTrun);
            GameMain(blackPlayer, whitePlayer, gameNum, switchTrun);
        }

        static void LoadGameInfoFromFile(string path, out Player blackPlayer, out Player whitePlayer, out int gameNum, out bool switchTurn)
        {
            using var sr = new StreamReader(path);
            var gameInfo = (GameInfo)JsonSerializer.Deserialize(sr.ReadToEnd(), typeof(GameInfo));
            blackPlayer = new Player(Color.Black, gameInfo.BlackPlayerName,
                                     new GTPProcessStartInfo(gameInfo.BlackPlayerPath, gameInfo.BlackPlayerArgs, gameInfo.BlackPlayerWorkDir));
            whitePlayer = new Player(Color.White, gameInfo.WhitePlayerName,
                                     new GTPProcessStartInfo(gameInfo.WhitePlayerPath, gameInfo.WhitePlayerArgs, gameInfo.WhitePlayerWorkDir));
            gameNum = gameInfo.GameNum;
            switchTurn = gameInfo.SwitchTurn;
        }

        static void GameMain(Player blackPlayer, Player whitePlayer, int gameNum, bool switchTurn)
        {
            GameNum = gameNum;
            var currentBlackPlayer = blackPlayer;
            var currentWhitePlayer = whitePlayer;
            var game = new Game();
            game.PlayerMoved += Game_PlayerMoved;
            game.BoardChanged += Game_BoardChanged;
            game.GameStarted += Game_GameStarted;
            game.GameEnded += Game_GameEnded;
            game.Start(currentBlackPlayer, currentWhitePlayer, gameNum, switchTurn);
            while (game.IsNowPlaying)
            {
                // Write some codes that is executed while game is active.
                System.Threading.Thread.Sleep(1);
            }
            game.Stop();
            ShowFinalResult();
        }

        static void ShowFinalResult()
        {
            Console.WriteLine($"\n***Final Result***" +
                              $"\nBlack wins {BlackPlayerWinCount}times." +
                              $"\nWhite wins {WhitePlayerWinCount}times." +
                              $"\nGame was drawn {DrawCount}times.\n" +
                              $"\nBlack player's win rate = {(BlackPlayerWinCount + DrawCount * 0.5) / GameNum}%" +
                              $"\nWhite player's win rate = {(WhitePlayerWinCount + DrawCount * 0.5) / GameNum}%");
        }

        static void Game_GameStarted(GameStartedEventArgs e)
        {
            Console.WriteLine($"\nGame {e.GameID}");
            Console.WriteLine($"Black player = \"{e.BlackPlayerName}\"");
            Console.WriteLine($"White player = \"{e.WhitePlayerName}\"");
        }

        static void Game_GameEnded(GameEndedEventArgs e)
        {
            if (e.Error == GameError.Success)
            {
                Console.WriteLine($"Game over.\n{e.Message}");
                if (e.WinnerColor == Color.Black)
                    BlackPlayerWinCount++;
                else if (e.WinnerColor == Color.White)
                    WhitePlayerWinCount++;
                else
                    DrawCount++;
            }
        }

        static void Game_BoardChanged(BoardChangedEventArgs e)
        {
            Console.WriteLine($"{e.BoardString}\n");
        }

        static void Game_PlayerMoved(PlayerMovedEventArgs e)
        {
            Console.WriteLine($"{e.PlayerName} {e.Move.Color} {e.Move}");
        }
    }
}
