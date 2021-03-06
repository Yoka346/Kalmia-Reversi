using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

using Kalmia.Reversi;
using Kalmia.IO;

namespace Kalmia.GoTextProtocol
{
    public static class GTP
    {
        const string VERSION = "2.0";

        static GTPEngine Engine;
        static readonly ReadOnlyDictionary<string, Action<int, string[]>> COMMANDS;
        static bool Quit = false;
        static Logger Logger;

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

            // additional commands
            commands.Add("convert_sgf_to_coordinates", ExecuteConvertSGFToCoordinatesCommand);

            return commands;
        }

        public static void Mainloop(GTPEngine engine)
        {
            Mainloop(engine, string.Empty);
        }

        public static void Mainloop(GTPEngine engine, string logFilePath)
        {
            Engine = engine;
            Logger = new Logger(logFilePath);

            int id = 0;
            while (!Quit)
            {
                var cmdName = string.Empty;
                string[] args = null;
                var input = Console.ReadLine();
                Logger.WriteLine($"[{DateTime.Now}] Input: {input}");
                cmdName = ParseCommand(input, out id, out args);
                try
                {
                    COMMANDS[cmdName](id, args);
                }
                catch (KeyNotFoundException)
                {
                    if (Engine.GetOriginalCommands().Contains(cmdName))
                        GTPSuccess(id, Engine.ExecuteOriginalCommand(cmdName, args));
                    else
                        GTPFailure(id, "unknown command");
                }
                catch (GTPException ex)
                {
                    GTPFailure(id, ex.Message);
                }
#if RELEASE
                catch (Exception ex)
                {
                    GTPFailure(id, ex.Message);
                    Logger.WriteLine($"[ERROR_DETAIL]\n{ex}\n");
                    break;
                }
#endif
                Logger.Flush();
            }
            Quit = false;
            Engine = null;
            Logger.Dispose();
        }

        public static Move ConvertCoordinateRule(Move move)
        {
            if (move == Move.Null)
                return move;
            return new Move(move.Color, ConvertCoordinateRule(move.Pos));
        }

        public static BoardPosition ConvertCoordinateRule(BoardPosition pos)
        {
            if (pos == BoardPosition.Pass || pos == BoardPosition.Null)
                return pos;

            (var posX, var posY) = ((int)pos % Board.BOARD_SIZE, (int)pos / Board.BOARD_SIZE);
            posY = (Board.BOARD_SIZE - 1) - posY;
            return (BoardPosition)(posX + posY * Board.BOARD_SIZE);
        }

        static void GTPSuccess(int id, string msg = "")
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            var output = $"{idStr} {msg}";
            Logger.WriteLine($"[{DateTime.Now}] Status: Success  Output: {output}");
            Console.Write($"={output}\n\n");
            Console.Out.Flush();
        }

        static void GTPFailure(int id, string msg)
        {
            var idStr = (id != -1) ? id.ToString() : string.Empty;
            var output = $"{idStr} {msg}";
            Logger.WriteLine($"[{DateTime.Now}] Status: Failed  Output: {output}");
            Console.Write($"?{output}\n\n");
            Console.Out.Flush();
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
            if (color == DiscColor.Null)
            {
                GTPFailure(id, "invalid color");
                return;
            }

            var isSuccess = TryParseCoordinate(args[1], out (int posX, int posY) coord);
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
            if (color == DiscColor.Null)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id,  Engine.GenerateMove(color).ToString());
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
            if(color == DiscColor.Null)
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
            Engine.SendTimeLeft(color, timeLeft, byoYomiStonesLeft);
            GTPSuccess(id);
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
            if(args.Length < 1 || !File.Exists(args[0]))
            {
                GTPFailure(id, "invalid path.");
                return;
            }

            if (args.Length == 1)
            {
                GTPSuccess(id, Engine.LoadSGF(args[0]));
            }

            if (args.Length >= 2)
            {
                if (int.TryParse(args[1], out int result))
                    GTPSuccess(id, Engine.LoadSGF(args[0], result));
                else
                {
                    if(!TryParseCoordinate(args[1], out (int posX, int posY) coord))
                    {
                        GTPFailure(id, "invalid coordinate.");
                        return;
                    }
                    GTPSuccess(id, Engine.LoadSGF(args[0], coord.posX, coord.posY));
                }
            }
        }

        static void ExecuteColorCommand(int id, string[] args)
        {
            if(args.Length < 1)
            {
                GTPFailure(id, "invalid option");
                return;
            }
            var move = new Move(DiscColor.Black, args[0]);
            var color = Engine.GetColor(move.PosX, move.PosY);
            if(color == DiscColor.Null)
            {
                GTPSuccess(id, "empty");
                return;
            }
            GTPSuccess(id, (color == DiscColor.Black) ? "black" : "white");
        }

        static void ExecuteRegGenMoveCommand(int id, string[] args)
        {
            if (args.Length < 1)
            {
                GTPFailure(id, "invalid option");
            }

            var color = ParseColor(args[0]);
            if (color == DiscColor.Null)
            {
                GTPFailure(id, "invalid color");
                return;
            }
            GTPSuccess(id, Engine.RegGenerateMove(color).ToString());
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
            GTPSuccess(id, (Engine.GetSideToMove() == DiscColor.Black) ? "black" : "white");
        }

        static void ExecuteRulesFinalResult(int id, string[] args)
        {
            GTPSuccess(id, Engine.GetFinalResult());
        }

        // additional commands
        static void ExecuteConvertSGFToCoordinatesCommand(int id, string[] args)
        {
            if(args.Length == 0 || !File.Exists(args[0]))
            {
                GTPFailure(id, "invalid path.");
                return;
            }

            var board = new Board(DiscColor.Black, InitialBoardState.Cross);
            var node = SGFFile.LoadSGFFile(args[0]);
            var coordinates = new StringBuilder();
            while (true)
            {
                var hasMove = node.HasMove(board.SideToMove);
                if (!hasMove && node.HasMove(board.Opponent))
                {
                    board.SwitchSideToMove();
                    hasMove = true;
                }

                if (hasMove)
                {
                    var sgfCoord = node.GetMove(board.SideToMove);
                    var move = new Move(board.SideToMove, SGFFile.SGFCoordinateToBoardPos(sgfCoord));
                    if (!board.Update(move))
                        throw new GTPException("specified SGF file contains invalid move.");
                    if (move.Pos != BoardPosition.Pass)
                        coordinates.Append(move);
                }

                if (node.ChildNodes.Count == 0)
                    break;
                node = node.ChildNodes[0];
            }
            GTPSuccess(id, coordinates.ToString());
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

        static DiscColor ParseColor(string str)
        {
            str = str.ToLower();
            if (str == "b" || str == "black")
                return DiscColor.Black;
            else if (str == "w" || str == "white")
                return DiscColor.White;
            return DiscColor.Null;
        }

        static bool TryParseCoordinate(string str, out (int posX, int posY) coord)
        {
            str = str.ToLower();
            if (str == "pass")
            {
                coord = ((int)BoardPosition.Pass, 0);
                return true;
            }

            if (str.Length != 2)
            {
                coord = (-1, -1);
                return false;
            }

            coord.posX = str[0] - 'a';
            var isInt = int.TryParse(str[1].ToString(), out coord.posY);
            coord.posY -= 1;
            return isInt;
        }
    }
}
