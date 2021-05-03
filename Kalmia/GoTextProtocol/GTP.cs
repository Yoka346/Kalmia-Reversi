using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.GoTextProtocol
{
    public static class GTP
    {
        const string VERSION = "2.0";
        const int BOARD_SIZE = 8;
        const char BLACK_CHAR = 'X';
        const char WHITE_CHAR = 'O';
        const char BLANK_CHAR = '.';

        static IGTPEngine engine;
        static readonly ReadOnlyDictionary<string, Action<int, string[]>> COMMANDS;
        static bool quit = false;

        static GTP()
        {

        }

        public static void Mainloop(IGTPEngine engine)
        {
            int id = 0;
            while (!quit)
            {
                try
                {
                    COMMANDS[ParseCommand(Console.ReadLine(), out id, out string[] args)](id, args); ;
                }
                catch (KeyNotFoundException)
                {
                    GTPFailure(id, "unknown command");
                }
            }
            quit = false;
        }

        static void GTPSuccess(int id, string msg = "")
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            Console.Write($"={id}\n {msg}\n\n");
            Console.Out.Flush();
        }

        static void GTPFailure(int id, string msg)
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            Console.Write($"?{id}\n {msg}\n\n");
            Console.Out.Flush();
        }

        static void ExecuteProtocolVersionCommand(int id, string[] args)
        {
            GTPSuccess(id, VERSION);
        }

        static void ExecuteQuitCommand(int id, string[] args)
        {
            quit = true;
        }

        static void ExecuteNameCommand(int id, string[] args)
        {
            GTPSuccess(id, engine.GetVersion());
        }

        static void ExecuteVersionCommand(int id, string[] args)
        {
            GTPSuccess(id, engine.GetVersion());
        }

        static void ExecuteBoardSizeCommand(int id, string[] args)
        {
            if (args.Length == 0)
                GTPFailure(id, "invalid option");
            else
            {
                var isInt = int.TryParse(args[0], out int size);
                if (!isInt)
                    GTPFailure(id, "board size must be integer");

                if (engine.SetBoardSize(size))
                    GTPSuccess(id);
                else
                    GTPFailure(id, "unacceptable size");
            }
        }

        static void ExecuteClearBoardCommand(int id, string[] args)
        {
            engine.ClearBoard();
            GTPSuccess(id);
        }

        static void ExecuteKomiCommand(int id, string[] args)   // There are few people setting komi in reversi.
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            double komi;    // Komi is a proper name that means handicap rule in Go(Chinese game).
                            // In Reversi there is no komi rule, however I implemented that rule for compatibility with GTP.
            var isDouble = double.TryParse(args[0], out komi);
            if (!isDouble)
                GTPFailure(id, "komi must be real number");
            engine.SetKomi(komi);
            GTPSuccess(id);
        }

        static void ExecutePlayCommand(int id, string[] args)
        {
            if (args.Length < 2)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            var color = ParseColor(args[0]);
            if (color == Color.Blank)
            {
                GTPFailure(id, "invalid color");
                return;
            }

            var isSuccess = ParseCoordinate(args[1], out (int posX, int posY) coord);
            if (!isSuccess)
            {
                GTPFailure(id, "invalid corrdinate");
                return;
            }

            if (!engine.Play(color, new Move(color, coord)))
            {
                GTPFailure(id, "invalid move");
                return;
            }
            GTPSuccess(id);
        }

        static void ExecuteGenMoveCommand(int id, string[] args)
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
            }

            var color = ParseColor(args[0]);
            if (color == Color.Blank)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id, engine.GenerateMove(color));
        }

        static void ExecuteUndoCommand(int id, string[] args)
        {
            if (!engine.Undo())
            {
                GTPFailure(id, "cannot undo");
                return;
            }
            GTPSuccess(id);
        }

        static void ExecuteTimeSettingsCommand(int id, string[] args)   // The unit of time is second.
        {
            if (args.Length < 3)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            int mainTime, byoYomiTime, byoYomiStones;   
            try
            {
                mainTime = int.Parse(args[0]);  
                byoYomiTime = int.Parse(args[1]);   // Byo yomi is a proper name that means countdown rule in Go(Chinese game) or Shogi(Japanese game similar to chess).
                byoYomiStones = int.Parse(args[2]);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException) 
            {
                GTPFailure(id, "time and byo yomi stones must be integer");
            }
        }

        static void ExecuteTimeLeftCommand(int id, string[] args)   // The unit of time is second.
        {
            if(args.Length < 3)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            var color = ParseColor(args[0]);
            if(color == Color.Blank)
            {
                GTPFailure(id, "invalid color");
                return;
            }

            int timeLeft;
            int byoYomiStonesLeft;
            try
            {
                timeLeft = int.Parse(args[1]);
                byoYomiStonesLeft = int.Parse(args[2]);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                GTPFailure(id, "time and byo yomi stones must be integer");
            }
        }

        static void ExecuteListGamesCommand(int id, string[] args)
        {
            GTPSuccess(id, "Othello");
        }

        static void LoadSGFCommand(int id, string[] args)
        {
            GTPFailure(id, "not supported");
        }

        static void ExecuteReggenMoveCommand(int id, string[] args)
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
            }

            var color = ParseColor(args[0]);
            if (color == Color.Blank)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id, engine.RegGenerateMove(color));
        }

        static void ExecuteShowBoardCommand(int id, string[] args)
        {
            GTPSuccess(id, engine.ShowBoard());
        }

        static string ParseCommand(string cmd, out int id, out string[] args)
        {
            var splitedCmd = cmd.ToLower().Split(' ');
            var hasID = int.TryParse(splitedCmd[0], out id);
            var cmdNameIdx = hasID ? 1 : 0;
            args = new string[splitedCmd.Length - cmdNameIdx - 1];
            Array.Copy(splitedCmd, cmdNameIdx + 1, args, 0, args.Length);
            return splitedCmd[cmdNameIdx];
        }

        static Color ParseColor(string str)
        {
            str = str.ToLower();
            if (str == "b" || str == "black")
                return Color.Black;
            else if (str == "w" || str == "white")
                return Color.White;
            return Color.Blank;
        }

        static bool ParseCoordinate(string str, out (int posX, int posY) coord)
        {
            str = str.ToLower();
            if (str == "pass")
            {
                coord = (Move.PASS, 0);
                return true;
            }
            coord.posX = str[0] - 'a';
            return int.TryParse(str[1].ToString(), out coord.posY);
        }
    }
}
