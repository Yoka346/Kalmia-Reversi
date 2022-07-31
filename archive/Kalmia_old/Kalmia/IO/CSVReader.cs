using System;
using System.Collections.Generic;
using System.IO;

namespace Kalmia.IO
{
    public class CSVReader : IDisposable
    {
        StreamReader sr;
        readonly string[] HEADERS;

        public ReadOnlySpan<string> Headers { get { return this.HEADERS; } }

        public CSVReader(string path)
        {
            this.sr = new StreamReader(path);
            this.HEADERS = this.sr.ReadLine().Split(',');
        }

        ~CSVReader()
        {
            this.sr.Close();
        }

        public void Dispose()
        {
            this.sr.Close();
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            Dispose();
        }

        public int Peek()
        {
            return this.sr.Peek();
        }

        public Dictionary<string, string> ReadRow()
        {
            var itemsDict = new Dictionary<string, string>();
            var items = this.sr.ReadLine().Split(',');
            for (var i = 0; i < items.Length; i++)
                itemsDict.Add(this.HEADERS[i], items[i]);
            return itemsDict;
        }
    }
}
