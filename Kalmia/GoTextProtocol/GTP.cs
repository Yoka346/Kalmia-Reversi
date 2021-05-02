using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.GoTextProtocol
{
    public static class GTP
    {
        const string VERSION = "1.0";
        const int BOARD_SIZE = 8;
        const char BLACK_CHAR = 'X';
        const char WHITE_CHAR = 'O';
        const char BLANK_CHAR = '.';

        static IGTPEngine engine; 
        static readonly ReadOnlyDictionary<string, Action<int, string[]>> COMMANDS;
        static Board board = new Board(Color.Black, InitialBoardState.Cross);
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
            if(args.Length == 0)
                GTPFailure(id, "invalid option");
            else
            {
                var isInt = int.TryParse(args[0], out int size);
                if (engine.SetBoardSize(size))
                    GTPSuccess(id);
                else
                    GTPFailure(id, "board size must be integer");
            }
        }

        static void ExecuteClearBoardCommand(int id, string[] args)
        {

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
    }
}
