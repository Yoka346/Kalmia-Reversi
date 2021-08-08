using Kalmia.Reversi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Kalmia.IO
{
    public class WTHORHeader
    {
        public const int SIZE = 16;

        public DateTime FileCreationTime { get; }
        public int NumberOfGames { get; }
        public int NumberOfRecords { get; }
        public int GameYear { get; }
        public int BoardSize { get; }
        public int GameType { get; }
        public int Depth { get; }

        public WTHORHeader(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var data = new byte[SIZE];
            fs.Read(data, 0, data.Length);
            try
            {
                this.FileCreationTime = new DateTime(data[0] * 100 + data[1], data[2], data[3]);
            }
            catch (ArgumentOutOfRangeException)
            {
                this.FileCreationTime = new DateTime();
            }
            this.NumberOfGames = BitConverter.ToInt32(data.AsSpan(4, sizeof(int)));
            this.NumberOfRecords = BitConverter.ToUInt16(data.AsSpan(8, sizeof(ushort)));
            this.GameYear = BitConverter.ToUInt16(data.AsSpan(10, sizeof(ushort)));
            this.BoardSize = data[12];
            this.GameType = data[13];
            this.Depth = data[14];
        }
    }

    public class WTHORGameRecord
    {
        public string TornamentName { get; }
        public string BlackPlayerName { get; }
        public string WhitePlayerName { get; }
        public int BlackDiscCount { get; }
        public int BestBlackDiscCount { get; }
        public ReadOnlyCollection<Move> MoveRecord { get; }

        public WTHORGameRecord(string tornamentName, string blackPlayerName, string whitePlayerName, int blackDiscCount, int bestBlackDiscCount, IList<Move> moveRecord)
        {
            this.TornamentName = tornamentName;
            this.BlackPlayerName = blackPlayerName;
            this.WhitePlayerName = whitePlayerName;
            this.BlackDiscCount = blackDiscCount;
            this.BestBlackDiscCount = bestBlackDiscCount;
            this.MoveRecord = new ReadOnlyCollection<Move>(moveRecord);
        }
    }

    public class WTHORFile
    {
        public WTHORHeader JouHeader { get; }
        public WTHORHeader TrnHeader { get; }
        public WTHORHeader WtbHeader { get; }

        public ReadOnlyCollection<string> Players { get; }
        public ReadOnlyCollection<string> Tornaments { get; }
        public ReadOnlyCollection<WTHORGameRecord> GameRecords { get; }

        public WTHORFile(string jouPath, string trnPath, string wtbPath)
        {
            this.JouHeader = new WTHORHeader(jouPath);
            this.TrnHeader = new WTHORHeader(trnPath);
            this.WtbHeader = new WTHORHeader(wtbPath);
            LoadPlayersAndTornaments(jouPath, trnPath, out string[] players, out string[] tornaments);
            this.Players = new ReadOnlyCollection<string>(players);
            this.Tornaments = new ReadOnlyCollection<string>(tornaments);
            this.GameRecords = new ReadOnlyCollection<WTHORGameRecord>(LoadGameRecords(wtbPath));
        }

        void LoadPlayersAndTornaments(string jouPath, string trnPath, out string[] players, out string[] tornaments)
        {
            const int PLAYER_NAME_SIZE = 20;
            const int TORNAMENT_NAME_SIZE = 26;

            var recordNum = this.JouHeader.NumberOfRecords;
            players = new string[recordNum];
            tornaments = new string[recordNum];
            var playerNameBytes = new byte[PLAYER_NAME_SIZE];
            var tornamentNameBytes = new byte[TORNAMENT_NAME_SIZE];
            var encoding = Encoding.GetEncoding("ISO-8859-1");
            using var jouFs = new FileStream(jouPath, FileMode.Open, FileAccess.Read);
            using var trnFs = new FileStream(trnPath, FileMode.Open, FileAccess.Read);
            jouFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            trnFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            for(var i = 0; i < this.JouHeader.NumberOfRecords; i++)
            {
                jouFs.Read(playerNameBytes, 0, playerNameBytes.Length);
                trnFs.Read(tornamentNameBytes, 0, tornamentNameBytes.Length);
                players[i] = encoding.GetString(playerNameBytes);
                tornaments[i] = encoding.GetString(tornamentNameBytes);
            }
        }

        WTHORGameRecord[] LoadGameRecords(string wtbPath)
        {
            const int GAME_INFO_SIZE = 68;

            var gameRecords = new WTHORGameRecord[this.WtbHeader.NumberOfGames];
            var buffer = new byte[GAME_INFO_SIZE];
            using var wtbFs = new FileStream(wtbPath, FileMode.Open, FileAccess.Read);
            wtbFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            for (var i = 0; i < gameRecords.Length; i++)
            {
                wtbFs.Read(buffer, 0, buffer.Length);
                gameRecords[i] = new WTHORGameRecord(this.Tornaments[BitConverter.ToUInt16(buffer.AsSpan(0, sizeof(ushort)))],
                                                 this.Players[BitConverter.ToUInt16(buffer.AsSpan(2, sizeof(ushort)))],
                                                 this.Players[BitConverter.ToUInt16(buffer.AsSpan(4, sizeof(ushort)))],
                                                 buffer[6], buffer[7], createMoveRecord(buffer.AsSpan(8, 60)));
            }
            
            return gameRecords;

            List<Move> createMoveRecord(Span<byte> data)
            {
                var moveRecord = new List<Move>();
                var board = new Board(Color.Black, InitialBoardState.Cross); 
                foreach (var d in data)
                {
                    if (d == 0)
                        break;
                    if (board.GetNextMoves().Where(m => m.Pos == BoardPosition.Pass).Count() == 1)     // because pass is not described in WTHOR file, check if current board can be passed
                                                                                                        // and if so add pass to move record.
                    {
                        var pass = new Move(board.Turn, BoardPosition.Pass);
                        board.Update(pass);
                        moveRecord.Add(pass);
                    }

                    var move = new Move(board.Turn, d % 10 - 1, d / 10 - 1);
                    if (!board.IsLegalMove(move))
                        throw new InvalidMoveRecordException();
                    board.Update(move);
                    moveRecord.Add(move);
                }
                return moveRecord;
            }
        }
    }
}
