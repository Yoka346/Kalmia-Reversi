using System;
using System.Collections.Generic;
using System.Text;

namespace Kalmia
{
    public class InvalidMoveRecordException : WTHORFileException
    {
        public InvalidMoveRecordException() : base("Move record in WTHOR file was invalid.") { }
    }
}
