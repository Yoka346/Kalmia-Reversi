using System;
using System.Collections.Generic;
using System.Text;

namespace Kalmia
{
    public class WTHORFileException : Exception 
    {
        public WTHORFileException(string message) : base(message) { }
    }
}
