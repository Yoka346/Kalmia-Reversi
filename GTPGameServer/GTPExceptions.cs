using System;

namespace GTPGameServer
{
    public class GTPErrorException : Exception
    {
        public GTPErrorException(string errMessage) : base($"The error message was sent from GTP process. The message is \"{errMessage}\"") { }
    }

    public class GTPTimeoutException : Exception
    {
        public GTPTimeoutException(string input, int timeout) : base($"The GTP process doesn't send the result of \"{input}\"  within {timeout}ms.") { }
    }

    public class GTPInvalidDataFormatException : Exception
    {
        public GTPInvalidDataFormatException(string recievedData) : base($"Invalid format data recieved.\nRecieved data : \"{recievedData}\"") { }
        public GTPInvalidDataFormatException(string message, string recievedData) : base($"{message}\nRecieved data : \"{recievedData}\"") { }
    }
}
