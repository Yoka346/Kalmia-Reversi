using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.IO
{
    public struct WTHORHeader
    {
        public const int SIZE = 16;

        public DateTime FileCreationTime { get; set; }
        public int NumberOfGames { get; set; }
        public int NumberOfRecords { get; set; }
        public int GameYear { get; set; }
        public int BoardSize { get; set; }
        public int GameType { get; set; }
        public int Depth { get; set; }

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

        public void WriteToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            var data = new byte[SIZE];
            data[0] = (byte)(this.FileCreationTime.Year / 100);
            data[1] = (byte)(this.FileCreationTime.Year % 100);
            data[2] = (byte)this.FileCreationTime.Month;
            data[3] = (byte)this.FileCreationTime.Day;
            Buffer.BlockCopy(BitConverter.GetBytes(this.NumberOfGames), 0, data, 4, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(this.NumberOfRecords), 0, data, 8, sizeof(ushort));
            Buffer.BlockCopy(BitConverter.GetBytes(this.GameYear), 0, data, 10, sizeof(ushort));
            data[12] = (byte)this.BoardSize;
            data[13] = (byte)this.GameType;
            data[14] = (byte)this.Depth;
            fs.Write(data);
        }
    }

    public struct WTHORGameRecord
    {
        public string TornamentName { get; set; }
        public string BlackPlayerName { get; set; }
        public string WhitePlayerName { get; set; }
        public int BlackDiscCount { get; set; }
        public int BestBlackDiscCount { get; set; }
        public ReadOnlyCollection<Move> MoveRecord { get; set; }

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
        const string CHAR_ENCODING = "ISO-8859-1";
        const int PLAYER_NAME_SIZE = 20;
        const int TORNAMENT_NAME_SIZE = 26;
        const int GAME_INFO_SIZE = 68;

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

        public WTHORFile(WTHORHeader jouHeader, WTHORHeader trnHeader, WTHORHeader wtbHeader, string[] players, string[] tornaments, WTHORGameRecord[] gameRecords)
        {
            this.JouHeader = jouHeader;
            this.TrnHeader = trnHeader;
            this.WtbHeader = wtbHeader;
            this.Players = new ReadOnlyCollection<string>((string[])players.Clone());
            this.Tornaments = new ReadOnlyCollection<string>((string[])tornaments.Clone());
            this.GameRecords = new ReadOnlyCollection<WTHORGameRecord>((WTHORGameRecord[])gameRecords.Clone());
        }

        public void SaveToFiles(string jouFilePath, string trnFilePath, string wtbFilePath)
        {
            this.JouHeader.WriteToFile(jouFilePath);
            this.TrnHeader.WriteToFile(trnFilePath);
            this.WtbHeader.WriteToFile(wtbFilePath);

            using var jouFs = new FileStream(jouFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            using var trnFs = new FileStream(trnFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            jouFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            trnFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            var encoding = Encoding.GetEncoding(CHAR_ENCODING);
            for(var i = 0; i < this.JouHeader.NumberOfRecords; i++)
            {
                var buffer = encoding.GetBytes(this.Players[i]);
                if (buffer.Length > PLAYER_NAME_SIZE)
                    jouFs.Write(buffer.AsSpan(0, PLAYER_NAME_SIZE));
                else
                    jouFs.Write(buffer);
            }

            for(var i = 0; i < this.TrnHeader.NumberOfRecords; i++)
            {
                var buffer = encoding.GetBytes(this.Tornaments[i]);
                if (buffer.Length > TORNAMENT_NAME_SIZE)
                    trnFs.Write(buffer.AsSpan(0, TORNAMENT_NAME_SIZE));
                else
                    trnFs.Write(buffer);
            }

            using var wtbFs = new FileStream(wtbFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            wtbFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            foreach(var gameRecord in this.GameRecords)
            {
                var data = new byte[GAME_INFO_SIZE];
                Buffer.BlockCopy(BitConverter.GetBytes(Players.IndexOf(gameRecord.TornamentName)), 0, data, 0, sizeof(ushort));
                Buffer.BlockCopy(BitConverter.GetBytes(Players.IndexOf(gameRecord.BlackPlayerName)), 0, data, 2, sizeof(ushort));
                Buffer.BlockCopy(BitConverter.GetBytes(Players.IndexOf(gameRecord.WhitePlayerName)), 0, data, 4, sizeof(ushort));
                data[6] = (byte)gameRecord.BlackDiscCount;
                data[7] = (byte)gameRecord.BestBlackDiscCount;
                var i = 8;
                foreach(var move in gameRecord.MoveRecord)
                {
                    if (move.Coord == BoardCoordinate.Pass)
                        continue;
                    var x = (byte)((int)move.Coord % Board.BOARD_SIZE + 1);
                    var y = (byte)((int)move.Coord / Board.BOARD_SIZE + 1);
                    data[i++] = (byte)(x * 10 + y);
                }
                wtbFs.Write(data);
            }
        }

        void LoadPlayersAndTornaments(string jouPath, string trnPath, out string[] players, out string[] tornaments)
        {
            var recordNum = this.JouHeader.NumberOfRecords;
            players = new string[recordNum];
            tornaments = new string[recordNum];
            var playerNameBytes = new byte[PLAYER_NAME_SIZE];
            var tornamentNameBytes = new byte[TORNAMENT_NAME_SIZE];
            var encoding = Encoding.GetEncoding(CHAR_ENCODING);
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
            var gameRecords = new WTHORGameRecord[this.WtbHeader.NumberOfGames];
            var buffer = new byte[GAME_INFO_SIZE * this.WtbHeader.NumberOfGames];
            using var wtbFs = new FileStream(wtbPath, FileMode.Open, FileAccess.Read);
            wtbFs.Seek(WTHORHeader.SIZE, SeekOrigin.Begin);
            wtbFs.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < buffer.Length; i+=GAME_INFO_SIZE)
            {
                var buff = buffer.AsSpan(i, GAME_INFO_SIZE);
                gameRecords[i / GAME_INFO_SIZE] = new WTHORGameRecord(this.Tornaments[BitConverter.ToUInt16(buff.Slice(0, sizeof(ushort)))],
                                                 this.Players[BitConverter.ToUInt16(buff.Slice(2, sizeof(ushort)))],
                                                 this.Players[BitConverter.ToUInt16(buff.Slice(4, sizeof(ushort)))],
                                                 buff[6], buff[7], CreateMoveRecord(buff.Slice(8, 60)));
            }
            
            return gameRecords;
        }

        List<Move> CreateMoveRecord(Span<byte> data)
        {
            var moveRecord = new List<Move>();
            var board = new Board(DiscColor.Black);
            foreach (var d in data)
            {
                if (d == 0)
                    break;
                if (board.GetNextMoves()[0].Coord == BoardCoordinate.Pass)     // because pass is not described in WTHOR file, check if current board can be passed
                                                                                                   // and if so add pass to move record.
                {
                    var pass = new Move(board.SideToMove, BoardCoordinate.Pass);
                    board.Update(pass);
                    moveRecord.Add(pass);
                }

                var move = new Move(board.SideToMove, d % 10 - 1, d / 10 - 1);
                if (!board.IsLegalMove(move))
                    throw new InvalidMoveRecordException();
                board.Update(move);
                moveRecord.Add(move);
            }
            return moveRecord;
        }

    }
}
