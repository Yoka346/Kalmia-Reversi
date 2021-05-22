using System;
using System.Collections.Generic;
using System.Text;

namespace GTPGameServer
{
    public class GTPRequest
    {
        public int ID { get; private set; }
        public string Input { get; private set; }
        public int AcceptedTime { get; private set; }
        public int Timeout { get; private set; }
        GTPResult result;

        public GTPRequest(int id, string input, GTPResult result, int acceptedTime, int timeout)
        {
            this.ID = id;
            this.Input = input;
            this.result = result;
            this.AcceptedTime = acceptedTime;
            this.Timeout = timeout;
        }

        public GTPResult GetResult()
        {
            while (this.result.Status == GTPStatus.Waiting)
            {
                if (this.Timeout != -1 && Environment.TickCount - this.AcceptedTime >= this.Timeout)
                {
                    this.result.Status = GTPStatus.Timeout;
                    return this.result;
                }
                System.Threading.Thread.Sleep(1);
            }
            return this.result;
        }
    }
}
