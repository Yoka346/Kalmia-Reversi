using System;

namespace Kalmia.GoTextProtocol
{
    public class GTPException : Exception 
    {
        public GTPException(string message) : base(message) { }
    }
}
