using System;
using System.IO;

namespace Kalmia.IO
{
    public class Logger : IDisposable
    {
        FileStream logFs;
        StreamWriter logSw;
        bool outputToStdOut;

        public bool IsDisposed { get; private set; } = false;

        public Logger(string path, bool outputToStdOut)
        {
            this.logFs = new FileStream(path, FileMode.Create, FileAccess.Write);
            this.logSw = new StreamWriter(this.logFs);
            this.outputToStdOut = outputToStdOut;
        }

        public void Dispose()
        {
            this.logSw.Close();
            this.logFs.Close();
            this.IsDisposed = true;
            GC.SuppressFinalize(this);
        }

        public void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public void WriteLine(object obj)
        {
            WriteLine(obj.ToString());
        }

        public void WriteLine(string str)
        {
            this.logSw.WriteLine(str);
            if (this.outputToStdOut)
                Console.WriteLine(str);
            this.logSw.Flush();
        }

        public void Write(string str)
        {
            this.logSw.Write(str);
            if (this.outputToStdOut)
                Console.Write(str);
            this.logSw.Flush();
        }
    }
}
