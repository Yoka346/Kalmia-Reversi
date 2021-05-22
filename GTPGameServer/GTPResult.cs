using System;
using System.Collections.Generic;
using System.Text;

namespace GTPGameServer
{
    public enum GTPStatus
    {
        Waiting = 1,
        Success = 0,
        Failed = -1,
        Timeout = -2
    }

    public class GTPResult
    {
        public GTPStatus Status;
        public string Output;

        public GTPResult(GTPStatus status, string output)
        {
            this.Status = status;
            this.Output = output;
        }
    }
}
