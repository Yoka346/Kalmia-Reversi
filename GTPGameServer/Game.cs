using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public enum GameError
    {
        Success = 0,
        InvalidMove = -1,
        GTPError = -2,
        Suspended = -3
    }

    public struct TotalGameResult
    {
        public int GameNum;
        public int BlackWinCount;
        public int WhiteWinCount;
        public int DrawCount;

        public TotalGameResult(int gameNum, int blackWinCount, int whiteWinCount, int drawCount)
        {
            this.GameNum = gameNum;
            this.BlackWinCount = blackWinCount;
            this.WhiteWinCount = whiteWinCount;
            this.DrawCount = drawCount;
        }
    }

    public delegate void PlayerMovedEventHandler(PlayerMovedEventArgs e);
    public delegate void BoardChangedEventHandler(BoardChangedEventArgs e);
    public delegate void GameStartedEventHandler(GameStartedEventArgs e);
    public delegate void GameEndedEventHandler(GameEndedEventArgs e);

    public class Game
    {
        Board board;
        Task<TotalGameResult> mainloopTask;
        Player blackPlayer;
        Player whitePlayer;
        Player currentPlayer;
        Player opponentPlayer;

        public bool IsNowPlaying { get; private set; } = false;

        public event PlayerMovedEventHandler PlayerMoved;
        public event BoardChangedEventHandler BoardChanged;
        public event GameStartedEventHandler GameStarted;
        public event GameEndedEventHandler GameEnded;

        public Game()
        {
            this.IsNowPlaying = false;
        }

        public bool Start(Player blackPlayer, Player whitePlayer, int gameNum, bool switchTrun)
        {
            if (blackPlayer.Turn != Color.Black || whitePlayer.Turn != Color.White)
                return false;
            this.blackPlayer = new Player(blackPlayer);
            this.whitePlayer = new Player(whitePlayer);
            this.blackPlayer.Start();
            this.whitePlayer.Start();
            this.IsNowPlaying = true;
            this.mainloopTask = MainloopAsync(gameNum, switchTrun);
            return true;
        }

        public TotalGameResult Stop()    
        {
            this.IsNowPlaying = false;
            this.blackPlayer.Stop();
            this.whitePlayer.Stop();
            this.mainloopTask.ConfigureAwait(false);
            return this.mainloopTask.Result;
        }

        void SwitchPlayers()
        {
            var tmp = this.currentPlayer;
            this.currentPlayer = this.opponentPlayer;
            this.opponentPlayer = tmp;
        }

        async Task<TotalGameResult> MainloopAsync(int gameNum, bool switchTurn)
        {
            var blackWinCount = 0;
            var whiteWinCount = 0;
            var drawCount = 0;
            var ret = await Task.Run(() =>
            {
                try
                {
                    for (var gameID = 0; gameID < gameNum; gameID++)
                    {
                        this.board = new Board(Color.Black, InitialBoardState.Cross);
                        this.currentPlayer = this.blackPlayer;
                        this.opponentPlayer = this.whitePlayer;
                        this.currentPlayer.ClearBoard();
                        this.opponentPlayer.ClearBoard();
                        GameStarted(new GameStartedEventArgs(gameID, this.currentPlayer.Name, this.opponentPlayer.Name));
                        GameResult result = GameResult.NotOver;
                        while (this.IsNowPlaying)
                        {
                            result = this.board.GetGameResult(Color.Black);
                            if (result != GameResult.NotOver)
                                break;
                            var nextMove = this.currentPlayer.GetNextMove();
                            if (!this.board.IsLegalMove(nextMove))
                            {
                                GameEnded(new GameEndedEventArgs(GameError.InvalidMove, nextMove.ToString(), false, string.Empty, Color.Empty, 0));
                                return new TotalGameResult(0, 0, 0, 0);
                            }
                            this.board.Update(nextMove);
                            this.PlayerMoved(new PlayerMovedEventArgs(this.currentPlayer.Name, nextMove));
                            this.BoardChanged(new BoardChangedEventArgs(this.board.GetDiscsArray(), this.board.ToString()));
                            this.opponentPlayer.Play(nextMove);
                            SwitchPlayers();
                        }

                        var discDifference = Math.Abs(this.board.GetDiscCount(Color.Black) - this.board.GetDiscCount(Color.White));
                        if (result == GameResult.Win)
                        {
                            GameEnded(new GameEndedEventArgs(GameError.Success, $"Black wins by {discDifference}points.", false, this.blackPlayer.Name, Color.Black, discDifference));
                            if (switchTurn && gameID % 2 == 0)
                                blackWinCount++;
                            else
                                whiteWinCount++;
                        }
                        else if (result == GameResult.Lose)
                        {
                            GameEnded(new GameEndedEventArgs(GameError.Success, $"White wins by {discDifference}points.", false, this.whitePlayer.Name, Color.White, discDifference));
                            if (switchTurn && gameID % 2 == 0)
                                whiteWinCount++;
                            else
                                blackWinCount++;
                        }
                        else
                        {
                            GameEnded(new GameEndedEventArgs(GameError.Success, $"Draw.", true, string.Empty, Color.Empty, 0));
                            drawCount++;
                        }

                        if (switchTurn)
                        {
                            this.blackPlayer.SwitchTrun();
                            this.whitePlayer.SwitchTrun();
                            var tmp = this.blackPlayer;
                            this.blackPlayer = this.whitePlayer;
                            this.whitePlayer = tmp;
                        }
                    }
                    this.IsNowPlaying = false;
                    this.blackPlayer.Stop();
                    this.whitePlayer.Stop();
                    return new TotalGameResult(gameNum, blackWinCount, whiteWinCount, drawCount);
                }
                catch
                {
                    this.IsNowPlaying = false;
                    this.blackPlayer.Stop();
                    this.whitePlayer.Stop();
                    throw;
                }
            });
            return ret;
        }
    }
}
