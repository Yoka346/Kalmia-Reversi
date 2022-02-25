using System;
using System.IO;

namespace Kalmia.IO
{
    public class Logger : IDisposable
    {
        FileStream logFs;
        StreamWriter logSw;
        StreamWriter subSw;

        public bool IsDisposed { get; private set; } = false;

        public Logger(string path, Stream subOutStream = null)
        {
            if (path == string.Empty)
            {
                this.logFs = null;
                this.logSw = new StreamWriter(Stream.Null);
            }
            else
            {
                this.logFs = new FileStream(path, FileMode.Create, FileAccess.Write);
                this.logSw = new StreamWriter(this.logFs);
            }
            this.subSw = new StreamWriter(subOutStream ?? Stream.Null);
        }

        public void Dispose()
        {
            this.logSw.Dispose();
            if(this.logFs is not null)
                this.logFs.Dispose();
            if (this.subSw is not null)
                this.subSw.Dispose();
            this.IsDisposed = true;
        }

        public void Flush()
        {
            this.logSw.Flush();
            if (this.subSw is not null)
                this.subSw.Flush();
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
            if (this.subSw is not null)
                this.subSw.WriteLine(str);
        }

        public void Write(string str)
        {
            this.logSw.Write(str);
            if (this.subSw is not null)
                this.subSw.Write(str);
        }
    }
}
