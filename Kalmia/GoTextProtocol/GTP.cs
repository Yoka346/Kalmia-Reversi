using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.GoTextProtocol
{
    public enum GTPCoordinateRule
    {
        Chess,  //  A B C D E F G H         Chess style
                // 8 . . . . . . . . 
                // 7 . . . . . . . . 
                // 6 . . . . . . . . 
                // 5 . . . . . . . . 
                // 4 . . . . . . . . 
                // 3 . . . . . . . . 
                // 2 . . . . . . . . 
                // 1 . . . . . . . . 

        Othello //   A B C D E F G H        Othello(Japanese game almost same as Reversi) style
                // 1 . . . . . . . . 
                // 2 . . . . . . . . 
                // 3 . . . . . . . . 
                // 4 . . . . . . . . 
                // 5 . . . . . . . . 
                // 6 . . . . . . . . 
                // 7 . . . . . . . . 
                // 8 . . . . . . . . 
    }

    public static class GTP
    {
        const string VERSION = "2.0";
        const GTPCoordinateRule DEFAULT_COORDINATE_RULE = GTPCoordinateRule.Chess;

        static GTPCoordinateRule CoordinateRule;
        static GTPEngine Engine;
        static readonly ReadOnlyDictionary<string, Action<int, string[]>> COMMANDS;
        static bool Quit = false;
        static StreamWriter Logger;

        static GTP()
        {
            COMMANDS = new ReadOnlyDictionary<string, Action<int, string[]>>(InitCommands());
        }

        static Dictionary<string, Action<int, string[]>> InitCommands()
        {
            var commands = new Dictionary<string, Action<int, string[]>>();
            commands.Add("protocol_version", ExecuteProtocolVersionCommand);
            commands.Add("name", ExecuteNameCommand);
            commands.Add("version", ExecuteVersionCommand);
            commands.Add("known_command", ExecuteKnownCommandCommand);
            commands.Add("list_commands", ExecuteListCommandsCommand);
            commands.Add("quit", ExecuteQuitCommand);
            commands.Add("boardsize", ExecuteBoardSizeCommand);
            commands.Add("clear_board", ExecuteClearBoardCommand);
            commands.Add("komi", ExecuteKomiCommand);
            commands.Add("play", ExecutePlayCommand);
            commands.Add("genmove", ExecuteGenMoveCommand);
            commands.Add("undo", ExecuteUndoCommand);
            commands.Add("time_settings", ExecuteTimeSettingsCommand);
            commands.Add("time_left", ExecuteTimeLeftCommand);
            commands.Add("set_game", ExecuteSetGameCommand);
            commands.Add("list_games", ExecuteListGamesCommand);
            commands.Add("loadsgf", LoadSGFCommand);
            commands.Add("color", ExecuteColorCommand);
            commands.Add("reg_genmove", ExecuteRegGenMoveCommand);
            commands.Add("showboard", ExecuteShowBoardCommand);

            // version 1 commands(legacy)
            commands.Add("black", ExecuteBlackCommand);
            commands.Add("playwhite", ExecutePlayWhiteCommand);
            commands.Add("genmove_black", ExecuteGenMoveBlackCommand);
            commands.Add("genmove_white", ExecuteGenMoveWhiteCommand);

            // gogui-rules commands
            commands.Add("gogui-rules_game_id", ExecuteRulesGameIDCommand);
            commands.Add("gogui-rules_board", ExecuteShowBoardCommand);
            commands.Add("gogui-rules_board_size", ExecuteRulesBoardSizeCommand);
            commands.Add("gogui-rules_legal_moves", ExecuteRulesLegalMovesCommand);
            commands.Add("gogui-rules_side_to_move", ExecuteRulesSideToMoveCommand);
            commands.Add("gogui-rules_final_result", ExecuteRulesFinalResult);
            return commands;
        }

        public static void Mainloop(GTPEngine engine)
        {
            Mainloop(engine, null);
        }

        public static void Mainloop(GTPEngine engine, GTPCoordinateRule coordRule)
        {
            Mainloop(engine, coordRule, null);
        }

        public static void Mainloop(GTPEngine engine, string logFilePath)
        {
            Mainloop(engine, DEFAULT_COORDINATE_RULE, logFilePath);
        }

        public static void Mainloop(GTPEngine engine, GTPCoordinateRule coordRule, string logFilePath)
        {
            CoordinateRule = coordRule;
            Engine = engine;
            if (logFilePath != null)
            {
                Logger = new StreamWriter(logFilePath);
            }
            else
                Logger = new StreamWriter(Stream.Null);

            int id = 0;
            while (!Quit)
            {
                var cmdName = string.Empty;
                string[] args = null;
                try
                {
                    var input = Console.ReadLine();
                    Logger.WriteLine($"[{DateTime.Now}] Input: {input}");
                    Logger.Flush();
                    cmdName = ParseCommand(input, out id, out args);
                    COMMANDS[cmdName](id, args); 
                }
                catch (KeyNotFoundException)
                {
                    if (Engine.GetOriginalCommands().Contains(cmdName))
                        GTPSuccess(id, Engine.ExecuteOriginalCommand(cmdName, args));
                    else
                        GTPFailure(id, "unknown command");
                }
            }
            Quit = false;
            Engine = null;
            Logger.Close();
        }

        static void GTPSuccess(int id, string msg = "")
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            var output = $"{idStr} {msg}";
            Logger.WriteLine($"[{DateTime.Now}] Status: Success  Output: {output}");
            Console.Write($"={output}\n\n");
            Console.Out.Flush();
            Logger.Flush();
        }

        static void GTPFailure(int id, string msg)
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            var output = $"{idStr} {msg}";
            Logger.WriteLine($"[{DateTime.Now}] Status: Failed  Output: {output}");
            Console.Write($"?{output}\n\n");
            Console.Out.Flush();
            Logger.Flush();
        }

        static void ExecuteProtocolVersionCommand(int id, string[] args)
        {
            GTPSuccess(id, VERSION);
        }

        static void ExecuteNameCommand(int id, string[] args)
        {
            GTPSuccess(id, Engine.GetName());
        }

        static void ExecuteVersionCommand(int id, string[] args)
        {
            GTPSuccess(id, Engine.GetVersion());
        }

        static void ExecuteKnownCommandCommand(int id, string[] args)
        {
            if(args.Length == 0)
            {
                GTPFailure(id, "invalid option");
                return;
            }
            GTPSuccess(id, COMMANDS.Keys.Contains(args[0]).ToString().ToLower());
        }

        static void ExecuteListCommandsCommand(int id, string[] args)
        {
            var sb = new StringBuilder();
            foreach (var cmd in COMMANDS.Keys)
                sb.Append(cmd + "\n");
            sb.Remove(sb.Length - 1, 1);    // Remove last "\n"
            GTPSuccess(id, sb.ToString());
        }

        static void ExecuteQuitCommand(int id, string[] args)
        {
            Engine.Quit();
            Quit = true;
            GTPSuccess(id);
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

                if (Engine.SetBoardSize(size))
                    GTPSuccess(id);
                else
                    GTPFailure(id, "unacceptable size");
            }
        }

        static void ExecuteClearBoardCommand(int id, string[] args)
        {
            Engine.ClearBoard();
            GTPSuccess(id);
        }

        static void ExecuteKomiCommand(int id, string[] args)
        {
            // Komi is a proper name that means handicap rule in Go(Chinese game).
            // In Reversi there is no komi rule, however I implemented this method for compatibility with GTP.
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }    
            GTPSuccess(id);     // Do nothing of it.
        }

        static void ExecutePlayCommand(int id, string[] args)
        {
            if (args.Length < 2)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            var color = ParseColor(args[0]);
            if (color == StoneColor.Empty)
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

            if (!Engine.Play(new Move(color, coord)))
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
                return;
            }

            var color = ParseColor(args[0]);
            if (color == StoneColor.Empty)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id,  MoveToString(Engine.GenerateMove(color)));
        }

        static void ExecuteUndoCommand(int id, string[] args)
        {
            if (!Engine.Undo())
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
                return;
            }
            Engine.SetTime(mainTime, byoYomiTime, byoYomiStones);
            GTPSuccess(id);
        }

        static void ExecuteTimeLeftCommand(int id, string[] args)   // The unit of time is second.
        {
            if(args.Length < 3)
            {
                GTPFailure(id, "invalid option");
                return;
            }

            var color = ParseColor(args[0]);
            if(color == StoneColor.Empty)
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
                return;
            }
            Engine.SendTimeLeft(timeLeft, byoYomiStonesLeft);
        }

        static void ExecuteSetGameCommand(int id, string[] args)
        {
            if(args.Length == 0)
            {
                GTPFailure(id, "invali option");
                return;
            }

            if(args[0].ToLower() != "othello")
            {
                GTPFailure(id, "unsurpported game");
                return;
            }
            GTPSuccess(id);
        }

        static void ExecuteListGamesCommand(int id, string[] args)
        {
            GTPSuccess(id, "Othello");
        }

        static void LoadSGFCommand(int id, string[] args)
        {
            GTPFailure(id, "not supported");
        }

        static void ExecuteColorCommand(int id, string[] args)
        {
            if(args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }
            var move = new Move(StoneColor.Black, args[0]);
            var color = Engine.GetColor(move.PosX, move.PosY);
            if(color == StoneColor.Empty)
            {
                GTPSuccess(id, "empty");
                return;
            }
            GTPSuccess(id, (color == StoneColor.Black) ? "black" : "white");
        }

        static void ExecuteRegGenMoveCommand(int id, string[] args)
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
            }

            var color = ParseColor(args[0]);
            if (color == StoneColor.Empty)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id, MoveToString(Engine.RegGenerateMove(color)));
        }

        static void ExecuteShowBoardCommand(int id, string[] args)
        {
            GTPSuccess(id, $"\n {Engine.ShowBoard()}");
        }

        // version 1 commands
        static void ExecuteBlackCommand(int id, string[] args)
        {
            if(args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }
            ExecutePlayCommand(id, new string[] { "black", args[0] });
        }

        static void ExecutePlayWhiteCommand(int id, string[] args)
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }
            ExecutePlayCommand(id, new string[] { "white", args[0] });
        }

        static void ExecuteGenMoveBlackCommand(int id, string[] args)
        {
            ExecuteGenMoveCommand(id, new string[] { "black" });
        }

        static void ExecuteGenMoveWhiteCommand(int id, string[] args)
        {
            ExecuteGenMoveCommand(id, new string[] { "white" });
        }

        // gogui-rule commands
        static void ExecuteRulesGameIDCommand(int id, string[] args)
        {
            GTPSuccess(id, "Othello");
        }

        static void ExecuteRulesBoardSizeCommand(int id, string[] args)
        {
            GTPSuccess(id, Engine.GetBoardSize().ToString());
        }

        static void ExecuteRulesLegalMovesCommand(int id, string[] args)
        {
            var sb = new StringBuilder();
            foreach (var move in Engine.GetLegalMoves())
                sb.Append($"{move} ");
            GTPSuccess(id, sb.ToString());
        }

        static void ExecuteRulesSideToMoveCommand(int id, string[] args)
        {
            GTPSuccess(id, (Engine.GetSideToMove() == StoneColor.Black) ? "black" : "white");
        }

        static void ExecuteRulesFinalResult(int id, string[] args)
        {
            GTPSuccess(id, Engine.GetFinalResult());
        }

        static string ParseCommand(string cmd, out int id, out string[] args)
        {
            var splitedCmd = cmd.ToLower().Split(' ');
            var hasID = int.TryParse(splitedCmd[0], out id);
            int cmdNameIdx;
            if (hasID)
                cmdNameIdx = 1;
            else
            {
                id = -1;
                cmdNameIdx = 0;
            }

            args = new string[splitedCmd.Length - cmdNameIdx - 1];
            Array.Copy(splitedCmd, cmdNameIdx + 1, args, 0, args.Length);
            return splitedCmd[cmdNameIdx];
        }

        static StoneColor ParseColor(string str)
        {
            str = str.ToLower();
            if (str == "b" || str == "black")
                return StoneColor.Black;
            else if (str == "w" || str == "white")
                return StoneColor.White;
            return StoneColor.Empty;
        }

        static string MoveToString(Move move)
        {
            var str = move.ToString();
            if (str == "PASS" || CoordinateRule == GTPCoordinateRule.Othello)
                return str;
            var sb = new StringBuilder(str);
            sb.Remove(1, 1);
            sb.Append((Board.BOARD_SIZE - 1) - move.PosY + 1);
            return sb.ToString();
        }

        static bool ParseCoordinate(string str, out (int posX, int posY) coord)
        {
            str = str.ToLower();
            if (str == "pass")
            {
                coord = ((int)BoardPosition.Pass, 0);
                return true;
            }
            coord.posX = str[0] - 'a';
            var isInt = int.TryParse(str[1].ToString(), out coord.posY);
            coord.posY -= 1;
            if (CoordinateRule == GTPCoordinateRule.Chess)
                coord.posY = (Board.BOARD_SIZE - 1) - coord.posY;
            return isInt;
        }
    }
}
