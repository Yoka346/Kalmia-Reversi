namespace Kalmia
{
    public class InvalidMoveRecordException : WTHORFileException
    {
        public InvalidMoveRecordException() : base("Move record in WTHOR file was invalid.") { }
    }
}
