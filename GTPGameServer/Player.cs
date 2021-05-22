using GTPGameServer.Reversi;

namespace GTPGameServer
{
    public class Player
    {
        public Color Turn { get; private set; }
        public string Name { get; private set; }

        GTPProcess engineProcess;

        public Player(Color turn, string name, GTPProcessStartInfo procStartInfo)
        {
            this.Turn = turn;
            this.Name = name;
            this.engineProcess = new GTPProcess(procStartInfo);
        }

        public Player(Player player)
        {
            this.Turn = player.Turn;
            this.Name = player.Name;
            this.engineProcess = new GTPProcess(player.engineProcess.ProcessStartInfo);
        }

        public void SwitchTrun()
        {
            this.Turn = (Color)(-(int)this.Turn);
        }

        public void Start()
        {
            engineProcess.Start();
        }

        public void Stop()
        {
            if(!engineProcess.HasExisted)
                engineProcess.SendCommand("quit", 3000).GetResult();
            engineProcess.Kill();
        }

        public void ClearBoard()
        {
            SendCommandToEngine($"clear_board", 3000);
        }

        public void Play(Move move)
        {
            var colorStr = move.Color == Color.Black ? "b" : "w";
            SendCommandToEngine($"play {colorStr} {move}", 3000);
        }

        public Move GetNextMove()
        {
            var colorStr = this.Turn == Color.Black ? "b" : "w";
            var res = SendCommandToEngine($"genmove {colorStr}");
            return new Move(this.Turn, res);
        }

        string SendCommandToEngine(string cmd, int timeout = -1)
        {
            var req = this.engineProcess.SendCommand(cmd, timeout);
            var res = req.GetResult();
            if (res.Status != GTPStatus.Success)
                if (res.Status == GTPStatus.Failed)
                    throw new GTPErrorException(res.Output);
                else
                    throw new GTPTimeoutException(req.Input, req.Timeout);
            return res.Output;
        }
    }
}
